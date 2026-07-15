using System.Collections.Generic;
using BusinessLayer.DTOs;

namespace PRN222_assigment2.Models
{
    public class ManagePackagesViewModel
    {
        public IEnumerable<SubscriptionPackageDto> Packages { get; set; } = new List<SubscriptionPackageDto>();
        public IEnumerable<UserTransactionDto> Transactions { get; set; } = new List<UserTransactionDto>();
        public SubscriptionStatsDto Stats { get; set; } = null!;
    }
}
