using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDayApi.Services
{
    /// <summary>Stores uploaded images in Azure Blob Storage. Only the API talks to Azure.</summary>
    public interface IBlobStorageService
    {
        /// <summary>False when no Azure connection string has been configured.</summary>
        bool IsConfigured { get; }

        /// <summary>Uploads the stream and returns the public URL of the new blob.</summary>
        Task<string> UploadAsync(Stream content, string blobPath, string contentType, CancellationToken cancellationToken = default);

        /// <summary>Deletes a blob that was uploaded earlier. Does nothing if the URL is not ours.</summary>
        Task DeleteAsync(string? url, CancellationToken cancellationToken = default);
    }

    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient? _container;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _containerReady;

        public BlobStorageService(IConfiguration config)
        {
            var connectionString = config["AzureBlob:ConnectionString"];
            var containerName = config["AzureBlob:ContainerName"] ?? "raceday-images";

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                _container = new BlobContainerClient(connectionString, containerName);
            }
        }

        public bool IsConfigured => _container != null;

        public async Task<string> UploadAsync(Stream content, string blobPath, string contentType, CancellationToken cancellationToken = default)
        {
            if (_container == null)
            {
                throw new InvalidOperationException("Azure Blob Storage is not configured.");
            }

            await EnsureContainerAsync(cancellationToken);

            var blob = _container.GetBlobClient(blobPath);
            await blob.UploadAsync(
                content,
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
                cancellationToken);

            return blob.Uri.ToString();
        }

        public async Task DeleteAsync(string? url, CancellationToken cancellationToken = default)
        {
            if (_container == null || string.IsNullOrWhiteSpace(url)) return;

            try
            {
                var prefix = _container.Uri.ToString().TrimEnd('/') + "/";
                if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;

                var blobName = Uri.UnescapeDataString(url.Substring(prefix.Length));
                await _container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
            catch (Exception)
            {
                // Removing the old image is best-effort. A failure must not break the new upload.
            }
        }

        private async Task EnsureContainerAsync(CancellationToken cancellationToken)
        {
            if (_containerReady) return;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (!_containerReady)
                {
                    // Public read access on blobs lets the MVC site show the images by URL.
                    await _container!.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
                    _containerReady = true;
                }
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <summary>Checks that an uploaded file really is a small JPG, PNG or WEBP image.</summary>
    public static class ImageValidator
    {
        public const long MaxBytes = 5 * 1024 * 1024;

        private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

        public static async Task<(bool Ok, string? Error, string ContentType, string Extension)> ValidateAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return (false, "Please choose an image file to upload.", string.Empty, string.Empty);
            }

            if (file.Length > MaxBytes)
            {
                return (false, "The image is too large. The maximum size is 5 MB.", string.Empty, string.Empty);
            }

            var extension = Path.GetExtension(file.FileName);
            if (!ContentTypes.TryGetValue(extension, out var contentType))
            {
                return (false, "Only JPG, PNG or WEBP images are allowed.", string.Empty, string.Empty);
            }

            var header = new byte[12];
            int read;
            await using (var stream = file.OpenReadStream())
            {
                read = await stream.ReadAsync(header, 0, header.Length);
            }

            if (read < header.Length || !LooksLikeImage(header, contentType))
            {
                return (false, "That file is not a valid image.", string.Empty, string.Empty);
            }

            return (true, null, contentType, extension.ToLowerInvariant());
        }

        private static bool LooksLikeImage(byte[] h, string contentType)
        {
            return contentType switch
            {
                "image/jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
                "image/png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
                "image/webp" => h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46
                                && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50,
                _ => false
            };
        }
    }
}
