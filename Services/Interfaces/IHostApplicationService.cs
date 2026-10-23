using Staybnb.Models.Common;
using Staybnb.Models.Entities;
using Staybnb.Models.ViewModels.Host;

namespace Staybnb.Services.Interfaces
{
    /// <summary>
    /// The Guest -> Host lifecycle:
    ///   Submit  : Guest (or existing Host) sends property details + photos. Property is created INACTIVE.
    ///   Approve : Admin approves -> property activated, Host role ADDED, Guest role REMOVED.
    ///   Reject  : Admin rejects  -> property stays inactive, applicant notified.
    /// </summary>
    public interface IHostApplicationService
    {
        /// <summary>Returns the pending application if this Guest already has one (they may only have one at a time).</summary>
        Task<HostApplication?> GetPendingForGuestAsync(string userId);

        Task<ServiceResult<HostApplication>> SubmitAsync(string userId, HostApplicationViewModel model);

        Task<List<HostApplication>> GetForUserAsync(string userId);

        Task<ServiceResult> ApproveAsync(int applicationId, string adminId, string? adminNotes);

        Task<ServiceResult> RejectAsync(int applicationId, string adminId, string reason);
    }
}
