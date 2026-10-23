using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models.Common;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Services.Interfaces;

namespace Staybnb.Services
{
    public class CheckInService : ICheckInService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileStorageService _files;
        private readonly IBookingService _bookings;
        private readonly IActivityLogService _activityLog;
        private readonly INotificationService _notifications;

        public CheckInService(
            ApplicationDbContext context,
            IFileStorageService files,
            IBookingService bookings,
            IActivityLogService activityLog,
            INotificationService notifications)
        {
            _context = context;
            _files = files;
            _bookings = bookings;
            _activityLog = activityLog;
            _notifications = notifications;
        }

        private IQueryable<GuestCheckIn> CheckInsWithDetails() =>
            _context.GuestCheckIns
                .Include(c => c.Documents)
                .Include(c => c.CheckInProcess)
                .Include(c => c.Booking).ThenInclude(b => b.Guest)
                .Include(c => c.Booking).ThenInclude(b => b.Property).ThenInclude(p => p.Images)
                .Include(c => c.Booking).ThenInclude(b => b.Property).ThenInclude(p => p.Host);

        // =====================================================================
        // GUEST SIDE
        // =====================================================================

        public Task<GuestCheckIn?> GetForGuestAsync(int bookingId, string guestId) =>
            CheckInsWithDetails().FirstOrDefaultAsync(c => c.BookingId == bookingId && c.Booking.GuestId == guestId);

        public async Task<ServiceResult<GuestCheckIn>> StartAsync(int bookingId, string guestId)
        {
            var existing = await GetForGuestAsync(bookingId, guestId);
            if (existing != null)
                return ServiceResult<GuestCheckIn>.Ok(existing);

            var booking = await _context.Bookings
                .Include(b => b.Property).ThenInclude(p => p.CheckInProcess)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestId == guestId);

            if (booking == null)
                return ServiceResult<GuestCheckIn>.Fail("Booking not found.");

            if (booking.Status != BookingStatus.Approved)
                return ServiceResult<GuestCheckIn>.Fail("Online check-in opens once the Host has approved your booking.");

            var process = booking.Property.CheckInProcess;
            if (process == null)
            {
                // Older properties may not have a process yet: create the default one
                process = new CheckInProcess { PropertyId = booking.PropertyId, Title = $"Check-in for {booking.Property.Title}" };
                process.SetSteps(CheckInProcess.DefaultSteps());
                _context.CheckInProcesses.Add(process);
                await _context.SaveChangesAsync();
            }

            var checkIn = new GuestCheckIn
            {
                BookingId = booking.Id,
                CheckInProcessId = process.Id,
                Status = CheckInStatus.InProgress,
                CurrentStep = 1,
                CreatedAt = DateTime.UtcNow
            };
            _context.GuestCheckIns.Add(checkIn);
            await _context.SaveChangesAsync();

            return ServiceResult<GuestCheckIn>.Ok((await GetForGuestAsync(bookingId, guestId))!);
        }

        public async Task<ServiceResult> CompleteStepAsync(int bookingId, string guestId, int stepNumber,
            bool acknowledged, IFormFile? document, string? documentType)
        {
            var checkIn = await GetForGuestAsync(bookingId, guestId);
            if (checkIn == null)
                return ServiceResult.Fail("Check-in not found. Please start check-in from your booking.");

            if (checkIn.Booking.Status != BookingStatus.Approved)
                return ServiceResult.Fail("This booking is not open for check-in.");

            if (checkIn.Status is CheckInStatus.Submitted or CheckInStatus.Verified)
                return ServiceResult.Fail("Your check-in has already been submitted.");

            var steps = checkIn.CheckInProcess.GetSteps();
            var step = steps.FirstOrDefault(s => s.StepNumber == stepNumber);
            if (step == null)
                return ServiceResult.Fail("That check-in step does not exist.");

            if (stepNumber > checkIn.CurrentStep)
                return ServiceResult.Fail("Please complete the earlier steps first.");

            if (step.RequiresDocument)
            {
                var docType = DocumentTypeFor(step);
                var existingDoc = checkIn.Documents.FirstOrDefault(d => d.DocumentType == docType);
                var hasUsableDoc = existingDoc != null && existingDoc.Status != DocumentStatus.Rejected;

                if (document == null || document.Length == 0)
                {
                    if (!hasUsableDoc)
                        return ServiceResult.Fail($"Please upload your {docType} to continue.");
                }
                else
                {
                    var validation = _files.ValidateDocument(document);
                    if (!validation.IsValid)
                        return ServiceResult.Fail(validation.Error!);

                    var storedName = await _files.SaveGuestDocumentAsync(document);

                    // Replace an earlier (pending or rejected) upload for the same step
                    if (existingDoc != null)
                    {
                        _files.DeleteGuestDocument(existingDoc.FileName);
                        _context.GuestDocuments.Remove(existingDoc);
                    }

                    var label = string.IsNullOrWhiteSpace(documentType) ? docType : documentType.Trim();
                    _context.GuestDocuments.Add(new GuestDocument
                    {
                        GuestCheckInId = checkIn.Id,
                        DocumentType = docType,
                        OriginalFileName = Truncate($"{label}: {Path.GetFileName(document.FileName)}", 200),
                        FileName = storedName,
                        ContentType = document.ContentType,
                        Status = DocumentStatus.Pending,
                        UploadedAt = DateTime.UtcNow
                    });

                    await _activityLog.LogAsync(guestId, ActivityActions.DocumentUploaded, ActivityType.CheckIn,
                        $"{docType} uploaded for booking #{bookingId}.");
                }
            }
            else if (!acknowledged)
            {
                return ServiceResult.Fail("Please tick the box to confirm this step.");
            }

            // ---------- Move forward ----------
            var isLastStep = stepNumber >= steps.Max(s => s.StepNumber);
            if (!isLastStep)
            {
                checkIn.CurrentStep = Math.Max(checkIn.CurrentStep, stepNumber + 1);
                checkIn.Status = CheckInStatus.InProgress;
                await _context.SaveChangesAsync();
                return ServiceResult.Ok();
            }

            checkIn.Status = CheckInStatus.Submitted;
            checkIn.SubmittedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(guestId, ActivityActions.CheckInSubmitted, ActivityType.CheckIn,
                $"Online check-in submitted for booking #{bookingId} at '{checkIn.Booking.Property.Title}'.");

            await _notifications.CreateAsync(checkIn.Booking.Property.HostId,
                "Check-in Submitted",
                $"{checkIn.Booking.Guest.FullName} completed online check-in for '{checkIn.Booking.Property.Title}'. Please verify their documents.",
                NotificationType.CheckInSubmitted,
                $"/Host/ReviewCheckIn/{checkIn.Id}");

            return ServiceResult.Ok("Check-in submitted! The Host will verify your documents shortly.");
        }

        public async Task<ServiceResult> GoToStepAsync(int bookingId, string guestId, int stepNumber)
        {
            var checkIn = await GetForGuestAsync(bookingId, guestId);
            if (checkIn == null) return ServiceResult.Fail("Check-in not found.");

            if (checkIn.Status is CheckInStatus.Submitted or CheckInStatus.Verified)
                return ServiceResult.Fail("Your check-in has already been submitted.");

            if (stepNumber < 1 || stepNumber > checkIn.CurrentStep)
                return ServiceResult.Fail("You can't skip ahead.");

            checkIn.CurrentStep = stepNumber;
            await _context.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // =====================================================================
        // HOST SIDE
        // =====================================================================

        public Task<GuestCheckIn?> GetForHostAsync(int guestCheckInId, string hostId) =>
            CheckInsWithDetails().FirstOrDefaultAsync(c => c.Id == guestCheckInId && c.Booking.Property.HostId == hostId);

        public async Task<ServiceResult> VerifyDocumentAsync(int documentId, string hostId)
        {
            var document = await LoadDocumentForHostAsync(documentId, hostId);
            if (document == null) return ServiceResult.Fail("Document not found.");

            document.Status = DocumentStatus.Verified;
            document.ReviewedAt = DateTime.UtcNow;
            document.RejectionReason = null;
            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(hostId, ActivityActions.DocumentVerified, ActivityType.DocumentVerification,
                $"{document.DocumentType} for booking #{document.GuestCheckIn.BookingId} verified.");

            return ServiceResult.Ok($"{document.DocumentType} verified.");
        }

        public async Task<ServiceResult> RejectDocumentAsync(int documentId, string hostId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return ServiceResult.Fail("Please tell the guest why the document was rejected.");

            var document = await LoadDocumentForHostAsync(documentId, hostId);
            if (document == null) return ServiceResult.Fail("Document not found.");

            document.Status = DocumentStatus.Rejected;
            document.ReviewedAt = DateTime.UtcNow;
            document.RejectionReason = Truncate(reason.Trim(), 300);

            // Send the guest back to the step where this document is uploaded
            var checkIn = document.GuestCheckIn;
            var step = checkIn.CheckInProcess.GetSteps()
                .FirstOrDefault(s => s.RequiresDocument && DocumentTypeFor(s) == document.DocumentType);
            checkIn.Status = CheckInStatus.Rejected;
            checkIn.CurrentStep = step?.StepNumber ?? 1;
            checkIn.SubmittedAt = null;

            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(hostId, ActivityActions.DocumentRejected, ActivityType.DocumentVerification,
                $"{document.DocumentType} for booking #{checkIn.BookingId} rejected. Reason: {document.RejectionReason}");

            await _notifications.CreateAsync(checkIn.Booking.GuestId,
                "Document Needs Attention",
                $"Your {document.DocumentType} for '{checkIn.Booking.Property.Title}' was not accepted: {document.RejectionReason} Please upload a new one.",
                NotificationType.DocumentRejected,
                $"/CheckIn/Index/{checkIn.BookingId}");

            return ServiceResult.Ok("Document rejected. The guest has been asked to upload a new one.");
        }

        public async Task<ServiceResult> ApproveCheckInAsync(int guestCheckInId, string hostId)
        {
            var checkIn = await GetForHostAsync(guestCheckInId, hostId);
            if (checkIn == null) return ServiceResult.Fail("Check-in not found.");

            if (checkIn.Status != CheckInStatus.Submitted)
                return ServiceResult.Fail("The guest has not finished submitting their check-in yet.");

            var requiredTypes = checkIn.CheckInProcess.GetSteps()
                .Where(s => s.RequiresDocument)
                .Select(DocumentTypeFor)
                .Distinct()
                .ToList();

            foreach (var type in requiredTypes)
            {
                var doc = checkIn.Documents.FirstOrDefault(d => d.DocumentType == type);
                if (doc == null || doc.Status != DocumentStatus.Verified)
                    return ServiceResult.Fail($"Please verify the guest's {type} before completing check-in.");
            }

            checkIn.Status = CheckInStatus.Verified;
            checkIn.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Booking: Approved -> CheckedIn (logs + notifies the guest)
            var result = await _bookings.MarkCheckedInAsync(checkIn.BookingId, hostId);
            if (!result.Succeeded)
            {
                checkIn.Status = CheckInStatus.Submitted;
                checkIn.CompletedAt = null;
                await _context.SaveChangesAsync();
                return result;
            }

            return ServiceResult.Ok($"{checkIn.Booking.Guest.FullName} is checked in.");
        }

        // =====================================================================
        // SHARED
        // =====================================================================

        public Task<GuestDocument?> GetDocumentForViewerAsync(int documentId, string userId, bool isAdmin) =>
            _context.GuestDocuments
                .AsNoTracking()
                .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
                .FirstOrDefaultAsync(d => d.Id == documentId &&
                    (isAdmin
                     || d.GuestCheckIn.Booking.GuestId == userId
                     || d.GuestCheckIn.Booking.Property.HostId == userId));

        // =====================================================================
        // HELPERS
        // =====================================================================

        private Task<GuestDocument?> LoadDocumentForHostAsync(int documentId, string hostId) =>
            _context.GuestDocuments
                .Include(d => d.GuestCheckIn).ThenInclude(c => c.CheckInProcess)
                .Include(d => d.GuestCheckIn).ThenInclude(c => c.Booking).ThenInclude(b => b.Property)
                .FirstOrDefaultAsync(d => d.Id == documentId && d.GuestCheckIn.Booking.Property.HostId == hostId);

        /// <summary>The document type stored for a step, e.g. "ID or Passport".</summary>
        public static string DocumentTypeFor(CheckInStep step) =>
            Truncate(string.IsNullOrWhiteSpace(step.DocumentType) ? step.Title : step.DocumentType.Trim(), 100);

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    }
}
