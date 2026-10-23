using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// Streams private guest documents (ID / passport). These files live outside wwwroot,
    /// so this action is the ONLY way to open them - and only for the guest who uploaded it,
    /// the Host of the property, or an Admin.
    /// </summary>
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly ICheckInService _checkIns;
        private readonly IFileStorageService _files;
        private readonly UserManager<ApplicationUser> _userManager;

        public DocumentsController(ICheckInService checkIns, IFileStorageService files, UserManager<ApplicationUser> userManager)
        {
            _checkIns = checkIns;
            _files = files;
            _userManager = userManager;
        }

        // GET: /Documents/Open/5
        [HttpGet]
        public async Task<IActionResult> Open(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var isAdmin = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.SuperAdmin);

            var document = await _checkIns.GetDocumentForViewerAsync(id, userId, isAdmin);
            if (document == null)
                return NotFound(); // same answer for "missing" and "not yours"

            var stored = _files.OpenGuestDocument(document.FileName);
            if (stored == null)
                return NotFound();

            // Don't let browsers or proxies keep copies of identity documents
            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["X-Content-Type-Options"] = "nosniff";

            // No file name passed -> shown inline in the browser (PDF viewer / image)
            return File(stored.Stream, stored.ContentType);
        }
    }
}
