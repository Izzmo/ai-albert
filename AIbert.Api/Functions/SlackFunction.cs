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
        _logger.LogInformation(body);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        switch (GetEventType(body))
        {
            case SlackEventType.UrlVerification:
                await HandleChallenge(body, response);
                break;
            case SlackEventType.InstantMessage:
                await HandleInstantMessage(body, response);
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

        return data.Type switch
        {
            "url_verification" => SlackEventType.UrlVerification,
            "event_callback" => SlackEventType.InstantMessage
        };
    }

    private static async Task HandleChallenge(string body, HttpResponseData response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<ChallengeEvent>(body, options);

        await response.WriteStringAsync(data.Challenge);
    }

    private async Task HandleInstantMessage(string body, HttpResponseData response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<EventCallbackEvent>(body, options);

    }

    private enum SlackEventType
    {
        UrlVerification,
        InstantMessage,
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

    private class EventCallbackEvent
    {
        public InstantMessageEvent Event { get; set; }
    }

    private class InstantMessageEvent
    {
        public string Type { get; set; }
        public string Channel { get; set; }
        public string User { get; set; }
        public string Text { get; set; }
        public string Ts { get; set; }
        public string Event_Ts { get; set; }
        public string Channel_Type { get; set; }
    }
}
