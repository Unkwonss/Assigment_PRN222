using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BusinessLayer.Interfaces;
using PRN222_assigment2.Models;

namespace PRN222_assigment2.Pages.Subscription
{
    [Authorize]
    public class MyHistoryModel : PageModel
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IUserService _userService;

        public MyHistoryModel(ISubscriptionService subscriptionService, IUserService userService)
        {
            _subscriptionService = subscriptionService;
            _userService = userService;
        }

        public UserSubscriptionViewModel ViewModel { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Challenge();
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound("Người dùng không tồn tại.");

            var usageMap = await _userService.GetWeeklyTokenUsageMapAsync(new List<int> { userId });
            int weeklyUsed = usageMap.ContainsKey(userId) ? usageMap[userId] : 0;

            var personalTx = await _subscriptionService.GetTransactionsByUserIdAsync(userId);

            ViewModel = new UserSubscriptionViewModel
            {
                User = user,
                WeeklyUsedTokens = weeklyUsed,
                PersonalTransactions = personalTx
            };

            return Page();
        }
    }
}
