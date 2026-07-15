using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using BusinessLayer.Interfaces;
using PRN222_assigment2.Hubs;
using Microsoft.Extensions.Logging;

namespace PRN222_assigment2.Pages.Subscription
{
    [Authorize]
    public class PaymentCallbackModel : PageModel
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IUserService _userService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<PaymentCallbackModel> _logger;

        public PaymentCallbackModel(
            ISubscriptionService subscriptionService,
            IUserService userService,
            IHubContext<ChatHub> hubContext,
            ILogger<PaymentCallbackModel> logger)
        {
            _subscriptionService = subscriptionService;
            _userService = userService;
            _hubContext = hubContext;
            _logger = logger;
        }

        public string Status { get; set; } = "Error";
        public string Message { get; set; } = "Mã giao dịch không hợp lệ.";

        public async Task OnGetAsync(
            string partnerCode,
            string orderId,
            string requestId,
            long amount,
            string orderInfo,
            string orderType,
            string transId,
            int resultCode,
            string message,
            string payType,
            string responseId,
            string extraData,
            string signature)
        {
            _logger.LogInformation("[MOMO CALLBACK] Received Redirect callback for OrderId={OrderId}, ResultCode={Code}", orderId, resultCode);

            if (!Guid.TryParse(orderId, out Guid transactionId))
            {
                Status = "Error";
                Message = "Mã giao dịch không hợp lệ.";
                return;
            }

            var transaction = await _subscriptionService.GetTransactionByIdAsync(transactionId);
            if (transaction == null)
            {
                Status = "Error";
                Message = "Giao dịch không tồn tại trên hệ thống.";
                return;
            }

            if (resultCode == 0)
            {
                var success = await _subscriptionService.ProcessSuccessfulSubscriptionAsync(transactionId);
                if (success)
                {
                    int userId = ParseUserIdFromExtraData(extraData);
                    if (userId > 0)
                    {
                        var user = await _userService.GetUserByIdAsync(userId);
                        if (user != null)
                        {
                            await _hubContext.Clients.All.SendAsync("ReceiveUserTokenUpdate", userId, user.WeeklyTokenLimit + user.PurchasedTokenBalance);
                        }
                    }

                    Status = "Success";
                    Message = $"Thanh toán thành công qua MoMo! Đơn hàng: {orderInfo}. Hạn mức token của bạn đã được cập nhật.";
                }
                else
                {
                    Status = "Error";
                    Message = "Thanh toán thành công nhưng có lỗi khi cấp phát token. Vui lòng liên hệ Admin.";
                }
            }
            else
            {
                await _subscriptionService.UpdateTransactionStatusAsync(transactionId, "Failed");
                Status = "Failed";
                Message = $"Thanh toán không thành công. Lý do: {message} (Mã lỗi: {resultCode})";
            }
        }

        private int ParseUserIdFromExtraData(string extraData)
        {
            try
            {
                if (string.IsNullOrEmpty(extraData)) return 0;
                var bytes = Convert.FromBase64String(extraData);
                var decoded = System.Text.Encoding.UTF8.GetString(bytes);
                if (int.TryParse(decoded, out int userId)) return userId;
            }
            catch
            {
                if (int.TryParse(extraData, out int userId)) return userId;
            }
            return 0;
        }
    }
}
