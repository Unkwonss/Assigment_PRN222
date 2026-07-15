using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BusinessLayer.Interfaces;
using BusinessLayer.DTOs;
using Microsoft.Extensions.Logging;

namespace PRN222_assigment2.Pages.Subscription
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IMomoService _momoService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ISubscriptionService subscriptionService, IMomoService momoService, ILogger<IndexModel> logger)
        {
            _subscriptionService = subscriptionService;
            _momoService = momoService;
            _logger = logger;
        }

        public IEnumerable<SubscriptionPackageDto> Packages { get; set; } = new List<SubscriptionPackageDto>();

        public async Task OnGetAsync()
        {
            Packages = await _subscriptionService.GetAllPackagesAsync();
        }

        public async Task<IActionResult> OnPostBuyAsync(int packageId)
        {
            try
             {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    return Challenge();
                }

                var package = await _subscriptionService.GetPackageByIdAsync(packageId);
                if (package == null)
                {
                    TempData["Error"] = "Gói đăng ký không tồn tại hoặc đã bị gỡ bỏ.";
                    return RedirectToPage();
                }

                var transaction = await _subscriptionService.CreateTransactionAsync(userId, packageId);

                string cleanPackageName = package.PackageName
                    .Replace("Gói Tuần Học Tập", "Goi Tuan Hoc Tap")
                    .Replace("Gói Tháng Đột Phá", "Goi Thang Dot Pha")
                    .Replace("Gói Siêu Cấp VIP", "Goi Sieu Cap VIP");
                string orderInfo = $"Mua {cleanPackageName} - {package.ExtraTokenAmount} Tokens";
                string extraData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId.ToString()));
                
                var (success, payUrl, message) = await _momoService.CreatePaymentUrlAsync(
                    transaction.TransactionId.ToString(),
                    orderInfo,
                    (long)package.Price,
                    extraData
                );

                if (success)
                {
                    _logger.LogInformation("[SUBSCRIPTION] Redirecting User {UserId} to MoMo payment URL for transaction {TxId}", userId, transaction.TransactionId);
                    return Redirect(payUrl);
                }

                _logger.LogWarning("[SUBSCRIPTION] Failed to create MoMo payment link for transaction {TxId}. Error: {Msg}", transaction.TransactionId, message);
                TempData["Error"] = $"Không thể kết nối cổng thanh toán MoMo: {message}";
                await _subscriptionService.UpdateTransactionStatusAsync(transaction.TransactionId, "Failed");
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình khởi tạo mua gói.");
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
                return RedirectToPage();
            }
        }
    }
}
