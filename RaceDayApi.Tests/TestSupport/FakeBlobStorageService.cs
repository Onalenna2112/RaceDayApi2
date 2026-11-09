using RaceDayApi.Services;

namespace RaceDayApi.Tests.TestSupport;

/// <summary>A stand-in for Azure Blob Storage used by the tests. It remembers uploads and returns a fake URL.</summary>
public class FakeBlobStorageService : IBlobStorageService
{
    public const string BaseUrl = "https://fake.blob.local/raceday-images/";

    public bool IsConfigured => true;

    public List<string> Uploaded { get; } = new();

    public Task<string> UploadAsync(Stream content, string blobPath, string contentType, CancellationToken cancellationToken = default)
    {
        Uploaded.Add(blobPath);
        return Task.FromResult(BaseUrl + blobPath);
    }

    public Task DeleteAsync(string? url, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
