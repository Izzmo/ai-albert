using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AIbert.Api.Functions;

public record PromiseKeeper(Guid Id, string Name);
public record PromiseOld(Guid Id, string Description, DateTimeOffset Deadline, PromiseKeeper Promiser, PromiseKeeper Promisee);

public class PromiseFunction
{
    private readonly ILogger _logger;

    public PromiseFunction(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PromiseFunction>();
    }

    [Function("ListPromises")]
    public HttpResponseData ListPromises([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        PromiseKeeper p1 = new(Guid.NewGuid(), "Nick");
        PromiseKeeper p2 = new(Guid.NewGuid(), "Brian");
        List<PromiseOld> promises = new()
        {
            new (Guid.NewGuid(), "Respond to my email", DateTimeOffset.UtcNow.AddDays(1), p1, p2)
        };

        response.WriteString(JsonSerializer.Serialize(promises));

        return response;
    }

    [Function("PostPromise")]
    public async Task<HttpResponseData> PostPromiseAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        var dataToBeSaved = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<PromiseOld>(dataToBeSaved);

        if (data == null || data.Deadline <= DateTimeOffset.Now)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var response = req.CreateResponse(HttpStatusCode.Accepted);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        return response;
    }
}
