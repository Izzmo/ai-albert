using System.Net;
using System.Text.Json;
using AIbert.Api.Core;
using AIbert.Api.Services;
using AIbert.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIbert.Api.Functions;

public class SlackFunction
{
    private readonly ILogger _logger;
    private readonly MessageHandler _messageHandler;

    public SlackFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<SlackFunction>();
        var tableStorageService = new TableStorageService<ThreadEntity>(config.GetValue<string>("StorageAccountConnectionString"), "threads");
        _messageHandler = new MessageHandler(loggerFactory, tableStorageService);
    }

    [Function(nameof(HandleSlackEvent))]
    public async Task<HttpResponseData> HandleSlackEvent([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slack")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        _logger.LogInformation("Slack message received: {body}", body);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        switch (GetEventType(body))
        {
            case SlackMessageType.UrlVerification:
                await HandleChallenge(body, response);
                break;
            case SlackMessageType.InstantMessage:
                await HandleInstantMessage(body, response);
                break;
            default:
                break;
        }

        return response;
    }

    private SlackMessageType GetEventType(string body)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<EventType>(body, options);

        if (data == null)
        {
           _logger.LogWarning("Could not determine event type.");
            return SlackMessageType.Unknown;
        }

        if (data.Type == "url_verification")
        {
            return SlackMessageType.UrlVerification;
        }

        if (data.Type == "event_callback")
        {
            return SlackMessageType.InstantMessage;
        }

        return SlackMessageType.Unknown;
    }

    private static async Task HandleChallenge(string body, HttpResponseData response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<ChallengeEvent>(body, options);

        await response.WriteStringAsync(data?.Challenge ?? string.Empty);
    }

    private async Task HandleInstantMessage(string body, HttpResponseData response)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<EventCallbackEvent>(body, options);

        if (data == null)
        {
            _logger.LogError("Could not deserialize.");
            return;
        }

        var chat = data.Event.Text;
        var sender = data.Event.Bot_Id == null ? data.Event.User : data.Event.Bot_Profile.Name;
        var threadLookupId = data.Event.Channel;
        var date = DateTimeOffset.FromUnixTimeSeconds((long)decimal.Parse(data.Event.Ts));

        await _messageHandler.AddChatToThread(threadLookupId, sender, chat, date);
    }

    private enum SlackMessageType
    {
        Unknown,
        UrlVerification,
        InstantMessage,
    }

    private enum SlackEventType
    {
        message,
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
        public string SubType { get; set; }
        public string Channel { get; set; }
        public string User { get; set; }
        public string Bot_Id { get; set; }
        public Bot_Profile Bot_Profile { get; set; }
        public string Text { get; set; }
        public string Ts { get; set; }
        public string Event_Ts { get; set; }
        public string Channel_Type { get; set; }
    }

    private class Bot_Profile
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
