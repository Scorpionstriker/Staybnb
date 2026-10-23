using Staybnb.Models.Common;
using Staybnb.Models.Entities;

namespace Staybnb.Services.Interfaces
{
    /// <summary>
    /// The multi-step online check-in:
    ///   Guest : Start -> complete each step (upload ID/passport where required) -> Submitted
    ///   Host  : verify / reject each document -> approve check-in -> booking becomes CheckedIn
    /// </summary>
    public interface ICheckInService
    {
        // ---------- Guest side ----------

        /// <summary>Loads the guest's check-in for a booking (with booking, property, process and documents).</summary>
        Task<GuestCheckIn?> GetForGuestAsync(int bookingId, string guestId);

        /// <summary>Creates the GuestCheckIn the first time an approved guest opens check-in.</summary>
        Task<ServiceResult<GuestCheckIn>> StartAsync(int bookingId, string guestId);

        /// <summary>
        /// Completes one step. For document steps, <paramref name="document"/> is required.
        /// Completing the last step submits the check-in to the Host.
        /// </summary>
        Task<ServiceResult> CompleteStepAsync(int bookingId, string guestId, int stepNumber,
            bool acknowledged, IFormFile? document, string? documentType);

        /// <summary>Lets the guest go back to an earlier step before submitting.</summary>
        Task<ServiceResult> GoToStepAsync(int bookingId, string guestId, int stepNumber);

        // ---------- Host side ----------

        /// <summary>Loads a check-in for review, only if it belongs to one of this Host's properties.</summary>
        Task<GuestCheckIn?> GetForHostAsync(int guestCheckInId, string hostId);

        Task<ServiceResult> VerifyDocumentAsync(int documentId, string hostId);

        Task<ServiceResult> RejectDocumentAsync(int documentId, string hostId, string reason);

        /// <summary>All documents verified -> check-in Verified and booking CheckedIn.</summary>
        Task<ServiceResult> ApproveCheckInAsync(int guestCheckInId, string hostId);

        // ---------- Shared ----------

        /// <summary>
        /// Returns the document if the user may see it: the guest who uploaded it,
        /// the Host of the property, or an Admin/SuperAdmin.
        /// </summary>
        Task<GuestDocument?> GetDocumentForViewerAsync(int documentId, string userId, bool isAdmin);
    }
}
