namespace Staybnb.Services.Interfaces
{
    /// <summary>
    /// Saves uploaded files.
    ///  - Property / profile images  -> wwwroot/uploads/...   (public, shown in the gallery)
    ///  - Guest ID / passport files  -> App_Data/PrivateUploads/documents (private, NOT reachable by URL;
    ///                                  only streamed through an authorised controller action)
    /// </summary>
    public interface IFileStorageService
    {
        FileValidationResult ValidateImage(IFormFile? file);

        FileValidationResult ValidateDocument(IFormFile? file);

        /// <summary>Saves a public image and returns its URL, e.g. "/uploads/properties/3f2a....jpg".</summary>
        /// <param name="folder">"properties" or "profiles".</param>
        Task<string> SaveImageAsync(IFormFile file, string folder = ImageFolders.Properties);

        /// <summary>Deletes a public image by the URL that SaveImageAsync returned.</summary>
        void DeleteImage(string? imageUrl);

        /// <summary>Saves a private guest document and returns the stored file name.</summary>
        Task<string> SaveGuestDocumentAsync(IFormFile file);

        /// <summary>Opens a private guest document for streaming, or null if it does not exist.</summary>
        StoredDocument? OpenGuestDocument(string storedFileName);

        void DeleteGuestDocument(string storedFileName);
    }

    public static class ImageFolders
    {
        public const string Properties = "properties";
        public const string Profiles = "profiles";
    }

    public class FileValidationResult
    {
        public bool IsValid { get; init; }
        public string? Error { get; init; }

        public static FileValidationResult Valid() => new() { IsValid = true };
        public static FileValidationResult Invalid(string error) => new() { IsValid = false, Error = error };
    }

    /// <summary>A private file opened for download. The controller must dispose the stream (File() does this).</summary>
    public class StoredDocument
    {
        public required Stream Stream { get; init; }
        public required string ContentType { get; init; }
        public required string FileName { get; init; }
    }
}
