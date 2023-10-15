using System.Net;
using System.Text.Json;
using AIbert.Api.Services;
using AIbert.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIbert.Api.Functions;

public class SettingsFunction
{
    private readonly ILogger _logger;
    private readonly BlobStorageService _blobStorageService;

    public SettingsFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<SettingsFunction>();
        _blobStorageService = new BlobStorageService(config.GetValue<string>("StorageAccountConnectionString"), "config");
    }

    [Function("GetSettings")]
    public async Task<HttpResponseData> GetSettingsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Settings")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);

        try
        {
            var prompt = await _blobStorageService.GetSystemPrompt() ?? string.Empty;
            var topP = await _blobStorageService.GetTopP();
            var temperature = await _blobStorageService.GetTemperature();
            Settings settings = new(prompt, topP, temperature);
            
            await response.WriteAsJsonAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with get prompt.");
        }

        return response;
    }

    [Function("PutSettings")]
    public async Task<HttpResponseData> UpdateSettingsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "Settings")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        var dataToBeSaved = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<Settings>(dataToBeSaved);

        if (data == null || string.IsNullOrWhiteSpace(data.SystemPrompt))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        try
        {
            await _blobStorageService.SaveSystemPrompt(data.SystemPrompt);
            await _blobStorageService.SaveTopP(data.TopP);
            await _blobStorageService.SaveTemperature(data.Temperature);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with save.");
        }

        return response;
    }
}
