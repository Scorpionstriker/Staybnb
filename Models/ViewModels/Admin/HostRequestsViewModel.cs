using Staybnb.Models.Entities;
using Staybnb.Models.Enums;

namespace Staybnb.Models.ViewModels.Admin
{
    /// <summary>Admin list of host applications, filtered by status.</summary>
    public class HostRequestsViewModel
    {
        public ApplicationStatus? StatusFilter { get; set; }
        public string? Search { get; set; }

        public List<HostApplication> Applications { get; set; } = new();

        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int TotalCount => PendingCount + ApprovedCount + RejectedCount;
    }

    /// <summary>One application opened for review.</summary>
    public class ReviewApplicationViewModel
    {
        public HostApplication Application { get; set; } = null!;

        /// <summary>The applicant's current roles (e.g. "Guest" before approval).</summary>
        public IList<string> ApplicantRoles { get; set; } = new List<string>();

        /// <summary>How many properties the applicant already hosts (approved).</summary>
        public int ApplicantActiveListings { get; set; }
    }
}

