using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIbert.Api.Functions;

public class SlackFunction
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;

    public SlackFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<SlackFunction>();
        _config = config;
    }

    [Function(nameof(HandleSlackEvent))]
    public async Task<HttpResponseData> HandleSlackEvent([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        switch (GetEventType(body))
        {
            case SlackEventType.url_verification:
                await GetChallenge(body, response);
                break;
            default:
                break;
        }

        return response;
    }

    private static SlackEventType GetEventType(string body)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<EventType>(body, options);
        return Enum.Parse<SlackEventType>(data.Type);
    }

    private static async Task GetChallenge(string body, HttpResponseData response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<ChallengeEvent>(body, options);

        await response.WriteStringAsync(data.Challenge);
    }

    private enum SlackEventType
    {
        url_verification,
    }

    private class EventType
    {
        public string Type { get; set; } = string.Empty;
    }

    private class ChallengeEvent
    {
        public string Token { get; set; } = string.Empty;
        public string Challenge { get; set; } = string.Empty;
    }
}
