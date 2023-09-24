using Azure.Storage.Blobs;

namespace AIbert.Api.Services;

public class BlobStorageService
{
    private readonly BlobContainerClient _client;

    public BlobStorageService(string connectionString, string containerName)
    {
        _client = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<string> GetSystemPrompt()
    {
        BlobClient blobClient = _client.GetBlobClient("system-prompt.txt");
        using MemoryStream memoryStream = new();
        await blobClient.DownloadToAsync(memoryStream);
        return System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
    }
}
