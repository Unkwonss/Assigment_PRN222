using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BusinessLayer.Interfaces;
using BusinessLayer.DTOs;
using PRN222_assigment2.Models;

namespace PRN222_assigment2.Pages.Subscription
{
    [Authorize(Roles = "Admin")]
    public class ManagePackagesModel : PageModel
    {
        private readonly ISubscriptionService _subscriptionService;

        public ManagePackagesModel(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        public ManagePackagesViewModel ViewModel { get; set; } = new();

        public async Task OnGetAsync()
        {
            var packages = await _subscriptionService.GetAllPackagesAsync();
            var transactions = await _subscriptionService.GetAllTransactionsAsync();
            var stats = await _subscriptionService.GetSubscriptionStatsAsync();

            ViewModel = new ManagePackagesViewModel
            {
                Packages = packages,
                Transactions = transactions,
                Stats = stats
            };
        }

        public async Task<IActionResult> OnPostCreateOrUpdatePackageAsync(SubscriptionPackageDto package)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu nhập vào không hợp lệ.";
                return RedirectToPage();
            }

            if (package.PackageId > 0)
            {
                var success = await _subscriptionService.UpdatePackageAsync(package);
                if (success) TempData["Success"] = "Cập nhật gói thành công.";
                else TempData["Error"] = "Cập nhật gói thất bại.";
            }
            else
            {
                await _subscriptionService.CreatePackageAsync(package);
                TempData["Success"] = "Thêm mới gói thành công.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeletePackageAsync(int id)
        {
            var success = await _subscriptionService.DeletePackageAsync(id);
            if (success) TempData["Success"] = "Xóa gói thành công.";
            else TempData["Error"] = "Xóa gói thất bại.";

            return RedirectToPage();
        }
    }
}
