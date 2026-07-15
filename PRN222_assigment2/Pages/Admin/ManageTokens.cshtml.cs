using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using BusinessLayer.Interfaces;
using PRN222_assigment2.Models;
using PRN222_assigment2.Hubs;

namespace PRN222_assigment2.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ManageTokensModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly IHubContext<ChatHub> _hubContext;

        public ManageTokensModel(IUserService userService, IHubContext<ChatHub> hubContext)
        {
            _userService = userService;
            _hubContext = hubContext;
        }

        public List<UserTokenUsageViewModel> StudentTokenList { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? RoleFilter { get; set; }

        public async Task OnGetAsync()
        {
            var allUsers = await _userService.GetAllUsersAsync();
            var targetUsers = allUsers.Where(u => u.Role == "Student" || u.Role == "Teacher");

            if (!string.IsNullOrWhiteSpace(RoleFilter))
            {
                targetUsers = targetUsers.Where(u => u.Role.Equals(RoleFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLower();
                targetUsers = targetUsers.Where(u =>
                    (u.FullName != null && u.FullName.ToLower().Contains(term)) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)) ||
                    (u.Username != null && u.Username.ToLower().Contains(term))
                );
            }

            var userIds = targetUsers.Select(u => u.UserId).ToList();
            var weeklyUsage = await _userService.GetWeeklyTokenUsageMapAsync(userIds);

            StudentTokenList = targetUsers.Select(s => new UserTokenUsageViewModel
            {
                UserId = s.UserId,
                FullName = s.FullName,
                Email = s.Email,
                Username = s.Username,
                Role = s.Role,
                WeeklyTokenLimit = s.WeeklyTokenLimit,
                WeeklyTokenUsed = weeklyUsage.ContainsKey(s.UserId) ? weeklyUsage[s.UserId] : 0,
                PurchasedTokenBalance = s.PurchasedTokenBalance,
                PurchasedTokenExpiry = s.PurchasedTokenExpiry
            }).ToList();
        }

        public async Task<IActionResult> OnPostUpdateTokenLimitAsync(int userId, int newLimit)
        {
            if (newLimit < 0)
            {
                TempData["Error"] = "Hạn mức token không thể nhỏ hơn 0.";
                return RedirectToPage();
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user != null)
            {
                user.WeeklyTokenLimit = newLimit;
                await _userService.UpdateUserAsync(user);
                TempData["Success"] = $"Đã cập nhật hạn mức token của người dùng '{user.FullName}' thành {newLimit:N0} tokens.";
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Cập nhật hạn mức", $"Hạn mức token của '{user.FullName}' đã được cập nhật thành {newLimit:N0} tokens.", "info");
                await _hubContext.Clients.All.SendAsync("ReceiveUserTokenUpdate", userId, user.WeeklyTokenLimit + user.PurchasedTokenBalance);
            }
            else
            {
                TempData["Error"] = "Không tìm thấy người dùng.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAllTokenLimitsAsync(int newLimit)
        {
            if (newLimit < 0)
            {
                TempData["Error"] = "Hạn mức token không thể nhỏ hơn 0.";
                return RedirectToPage();
            }

            try
            {
                var allUsers = await _userService.GetAllUsersAsync();
                var targets = allUsers.Where(u => u.Role == "Student" || u.Role == "Teacher");

                foreach (var user in targets)
                {
                    user.WeeklyTokenLimit = newLimit;
                    await _userService.UpdateUserAsync(user);
                    await _hubContext.Clients.All.SendAsync("ReceiveUserTokenUpdate", user.UserId, user.WeeklyTokenLimit + user.PurchasedTokenBalance);
                }

                TempData["Success"] = $"Đã cập nhật hạn mức token mặc định của tất cả sinh viên & giáo viên thành {newLimit:N0} tokens.";
                await _hubContext.Clients.All.SendAsync("ReceiveSystemNotification", "Hạn mức chung", $"Hạn mức mặc định của tất cả sinh viên & giáo viên đã được thay đổi thành {newLimit:N0} tokens.", "warning");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi cập nhật hàng loạt: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}
