using System.Net;
using System.Text.Json;
using AIbert.Api.Services;
using AIbert.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIbert.Api.Functions;

public class ThreadFunction
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly TableStorageService<ThreadEntity> _tableStorageService;

    public ThreadFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<ChatFunction>();
        _config = config;
        _tableStorageService = new TableStorageService<ThreadEntity>(config.GetValue<string>("StorageAccountConnectionString"), "threads");
    }

    [Function("GetThreads")]
    public async Task<HttpResponseData> GetThreadsAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Thread")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        var threads = await _tableStorageService.GetEntitiesAsync();

        await response.WriteStringAsync(JsonSerializer.Serialize(threads));

        return response;
    }

    [Function("GetThread")]
    public async Task<HttpResponseData> GetThreadAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Thread/{threadId}")] HttpRequestData req, string threadId)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        var thread = (await _tableStorageService.SearchEntitiesAsync(x => x.RowKey == threadId)).SingleOrDefault();

        if (thread == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        await response.WriteStringAsync(JsonSerializer.Serialize(thread));

        return response;
    }

    [Function(nameof(ClearThread))]
    public async Task<HttpResponseData> ClearThread([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "Thread/{threadId}")] HttpRequestData req, string threadId)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);

        await _tableStorageService.DeleteRow(threadId, threadId);

        return response;
    }

    [Function(nameof(CreateThread))]
    public async Task<HttpResponseData> CreateThread([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Thread")] HttpRequestData req)
    {
        var dataToBeSaved = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<IEnumerable<string>>(dataToBeSaved);

        if (data == null || data.Any(x => string.IsNullOrWhiteSpace(x)))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Location", $"/api/Thread/{ChatThread.GetThreadIdFromUsers(data)}");
        await response.WriteStringAsync(ChatThread.GetThreadIdFromUsers(data));

        return response;
    }
}
