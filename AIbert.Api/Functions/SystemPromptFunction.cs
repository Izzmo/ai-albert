using System.Net;
using System.Text.Json;
using AIbert.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIbert.Api.Functions;

public class SystemPromptFunction
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly BlobStorageService _blobStorageService;
    private static readonly List<string> _history = new();

    public SystemPromptFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<SystemPromptFunction>();
        _config = config;
        _blobStorageService = new BlobStorageService(config.GetValue<string>("AzureWebJobsStorage"), "config");
    }

    [Function("GetSystemPrompt")]
    public async Task<HttpResponseData> GetSystemPromptAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "SystemPrompt")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        try
        {
            var prompt = await _blobStorageService.GetSystemPrompt() ?? string.Empty;
            response.WriteString(prompt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with get prompt.");
        }

        return response;
    }

    [Function("PutSystemPrompt")]
    public async Task<HttpResponseData> UpdateSystemPromptAsync([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "SystemPrompt")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        var dataToBeSaved = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<ChatInput>(dataToBeSaved);

        if (data == null || string.IsNullOrWhiteSpace(data.input))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        try
        {
            await _blobStorageService.SaveSystemPrompt(data.input);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with save.");
        }

        return response;
    }
}
