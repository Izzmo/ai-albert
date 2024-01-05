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

    public Task<string> GetInitialSystemPrompt()
        => Get("initial-system-prompt.txt");

    public Task<string> GetSystemPrompt()
        => Get("system-prompt.txt");

    public Task SaveInitialSystemPrompt(string newContent)
        => Save("initial-system-prompt.txt", newContent);
    
    public Task SaveSystemPrompt(string newContent)
        => Save("system-prompt.txt", newContent);

    public async Task<decimal> GetTopP()
        => decimal.Parse(await Get("topp.txt"));

    public Task SaveTopP(decimal newContent)
        => Save("topp.txt", newContent.ToString());

    public async Task<decimal> GetTemperature()
        => decimal.Parse(await Get("temperature.txt"));

    public Task SaveTemperature(decimal newContent)
        => Save("temperature.txt", newContent.ToString());

    private async Task<string> Get(string filename)
    {
        BlobClient blobClient = _client.GetBlobClient(filename);
        using MemoryStream memoryStream = new();
        await blobClient.DownloadToAsync(memoryStream);
        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    private async Task Save(string filename, string newContent)
    {
        BlobClient blobClient = _client.GetBlobClient(filename);
        byte[] content = Encoding.UTF8.GetBytes(newContent);
        using MemoryStream memoryStream = new(content);
        await blobClient.UploadAsync(memoryStream, true);
    }
}
