using Azure.Storage.Blobs;
using System.Text;

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

    public async Task SaveSystemPrompt(string newContent)
    {
        BlobClient blobClient = _client.GetBlobClient("system-prompt.txt");
        byte[] content = Encoding.UTF8.GetBytes(newContent);
        using MemoryStream memoryStream = new(content);
        await blobClient.UploadAsync(memoryStream, true);
    }
}
