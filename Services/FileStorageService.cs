using Microsoft.Extensions.Options;
using Staybnb.Models.Settings;
using Staybnb.Services.Interfaces;

namespace Staybnb.Services
{
    public class FileStorageService : IFileStorageService
    {
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] DocumentExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
        private static readonly string[] AllowedImageFolders = { ImageFolders.Properties, ImageFolders.Profiles };

        private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".pdf"] = "application/pdf"
        };

        private readonly IWebHostEnvironment _env;
        private readonly StaybnbSettings _settings;
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(IWebHostEnvironment env, IOptions<StaybnbSettings> settings, ILogger<FileStorageService> logger)
        {
            _env = env;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>wwwroot/uploads</summary>
        private string PublicUploadsRoot => Path.Combine(_env.WebRootPath, "uploads");

        /// <summary>App_Data/PrivateUploads/documents (outside wwwroot, so never served directly)</summary>
        private string PrivateDocumentsRoot => Path.Combine(_env.ContentRootPath, "App_Data", "PrivateUploads", "documents");

        // =====================================================================
        // VALIDATION
        // =====================================================================

        public FileValidationResult ValidateImage(IFormFile? file) =>
            Validate(file, ImageExtensions, _settings.MaxImageSizeMb, "image", "JPG, PNG or WEBP");

        public FileValidationResult ValidateDocument(IFormFile? file) =>
            Validate(file, DocumentExtensions, _settings.MaxDocumentSizeMb, "document", "PDF, JPG or PNG");

        private static FileValidationResult Validate(IFormFile? file, string[] allowedExtensions, int maxSizeMb, string kind, string allowedText)
        {
            if (file == null || file.Length == 0)
                return FileValidationResult.Invalid($"Please choose a file ({kind}) to upload.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return FileValidationResult.Invalid($"'{file.FileName}' is not allowed. Use {allowedText} files.");

            if (file.Length > maxSizeMb * 1024L * 1024L)
                return FileValidationResult.Invalid($"'{file.FileName}' is larger than {maxSizeMb} MB.");

            // Check the real file content, not just the name (stops e.g. an .exe renamed to .jpg)
            if (!HasValidSignature(file, extension))
                return FileValidationResult.Invalid($"'{file.FileName}' does not appear to be a valid {extension} file.");

            return FileValidationResult.Valid();
        }

        /// <summary>Compares the first bytes of the file with the known "magic numbers" for each type.</summary>
        private static bool HasValidSignature(IFormFile file, string extension)
        {
            var header = new byte[12];
            using (var stream = file.OpenReadStream())
            {
                var read = stream.Read(header, 0, header.Length);
                if (read < 4) return false;
            }

            return extension switch
            {
                ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
                ".webp" => header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                           && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P',
                ".pdf" => header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F',
                _ => false
            };
        }

        // =====================================================================
        // PUBLIC IMAGES
        // =====================================================================

        public async Task<string> SaveImageAsync(IFormFile file, string folder = ImageFolders.Properties)
        {
            if (!AllowedImageFolders.Contains(folder))
                throw new ArgumentException($"Unknown image folder '{folder}'.", nameof(folder));

            var validation = ValidateImage(file);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Error);

            var directory = Path.Combine(PublicUploadsRoot, folder);
            Directory.CreateDirectory(directory);

            // Never trust the uploaded name: generate a unique one
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(directory, fileName);

            await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/{folder}/{fileName}";
        }

        public void DeleteImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                return; // ignore seeded/external URLs

            try
            {
                var relative = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(Path.Combine(_env.WebRootPath, relative));

                // Safety: only ever delete inside wwwroot/uploads
                if (!fullPath.StartsWith(Path.GetFullPath(PublicUploadsRoot), StringComparison.OrdinalIgnoreCase))
                    return;

                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete image {ImageUrl}", imageUrl);
            }
        }

        // =====================================================================
        // PRIVATE GUEST DOCUMENTS
        // =====================================================================

        public async Task<string> SaveGuestDocumentAsync(IFormFile file)
        {
            var validation = ValidateDocument(file);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Error);

            Directory.CreateDirectory(PrivateDocumentsRoot);

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(PrivateDocumentsRoot, storedFileName);

            await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            {
                await file.CopyToAsync(stream);
            }

            return storedFileName;
        }

        public StoredDocument? OpenGuestDocument(string storedFileName)
        {
            var fullPath = GetSafeDocumentPath(storedFileName);
            if (fullPath == null || !File.Exists(fullPath))
                return null;

            var extension = Path.GetExtension(fullPath);
            return new StoredDocument
            {
                Stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read),
                ContentType = ContentTypes.TryGetValue(extension, out var type) ? type : "application/octet-stream",
                FileName = Path.GetFileName(fullPath)
            };
        }

        public void DeleteGuestDocument(string storedFileName)
        {
            try
            {
                var fullPath = GetSafeDocumentPath(storedFileName);
                if (fullPath != null && File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete guest document {FileName}", storedFileName);
            }
        }

        /// <summary>
        /// Prevents "path traversal" attacks such as "../../appsettings.json":
        /// only a bare file name inside the private documents folder is accepted.
        /// </summary>
        private string? GetSafeDocumentPath(string storedFileName)
        {
            if (string.IsNullOrWhiteSpace(storedFileName)) return null;

            var safeName = Path.GetFileName(storedFileName);
            if (safeName != storedFileName) return null;

            return Path.Combine(PrivateDocumentsRoot, safeName);
        }
    }
}
