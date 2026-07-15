using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using BusinessLayer.Interfaces;
using PRN222_assigment2.Hubs;
using Microsoft.Extensions.Logging;

namespace PRN222_assigment2.Pages.Subscription
{
    [IgnoreAntiforgeryToken]
    public class IpnCallbackModel : PageModel
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IUserService _userService;
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly ILogger<IpnCallbackModel> _logger;

        public IpnCallbackModel(
            ISubscriptionService subscriptionService,
            IUserService userService,
            IHubContext<ChatHub> hubContext,
            ILogger<IpnCallbackModel> logger)
        {
            _subscriptionService = subscriptionService;
            _userService = userService;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync([FromBody] JsonElement momoIpn)
        {
            _logger.LogInformation("[MOMO IPN] Received IPN callback");
            try
            {
                string orderId = momoIpn.GetProperty("orderId").GetString() ?? string.Empty;
                int resultCode = momoIpn.GetProperty("resultCode").GetInt32();
                string extraData = momoIpn.GetProperty("extraData").GetString() ?? string.Empty;

                _logger.LogInformation("[MOMO IPN] Parsing IPN for OrderId={OrderId}, ResultCode={Code}", orderId, resultCode);

                if (Guid.TryParse(orderId, out Guid transactionId))
                {
                    if (resultCode == 0)
                    {
                        var success = await _subscriptionService.ProcessSuccessfulSubscriptionAsync(transactionId);
                        int userId = ParseUserIdFromExtraData(extraData);
                        if (success && userId > 0)
                        {
                            var user = await _userService.GetUserByIdAsync(userId);
                            if (user != null)
                            {
                                await _hubContext.Clients.All.SendAsync("ReceiveUserTokenUpdate", userId, user.WeeklyTokenLimit + user.PurchasedTokenBalance);
                            }
                        }
                    }
                    else
                    {
                        await _subscriptionService.UpdateTransactionStatusAsync(transactionId, "Failed");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý IPN từ MoMo");
            }

            return new NoContentResult();
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
