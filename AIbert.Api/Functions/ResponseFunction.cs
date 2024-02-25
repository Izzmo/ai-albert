using AIbert.Api.Core;
using AIbert.Api.Services;
using AIbert.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AIbert.Api.Functions
{
    public class ResponseFunction
    {
        private readonly ILogger _logger;
        private readonly MessageHandler _messageHandler;
        private readonly ChatGPT _chatGPT;
        private readonly string _slackToken;
        private static readonly HttpClient _client = new();

        public ResponseFunction(ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<ResponseFunction>();
            var tableStorageService = new TableStorageService<ThreadEntity>(config.GetValue<string>("StorageAccountConnectionString"), "threads");
            _messageHandler = new MessageHandler(loggerFactory, tableStorageService);
            _chatGPT = new ChatGPT(loggerFactory, config);
            _slackToken = config.GetValue<string>("SlackToken");
        }

        [Function("ResponseFunction")]
        public async Task<HttpResponseData> TimerTriggerAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "CheckChats")] HttpRequestData req)
        {
            var timeCutoff = DateTimeOffset.UtcNow.AddSeconds(-30);
            var threads = await _messageHandler.GetAllThreads();
            foreach (var thread in threads)
            {
                _logger.LogInformation("Checking thread {threadId}", thread.threadId);

                var lastChat = thread.chats.LastOrDefault();
                if (lastChat?.userId != "AIbert" && lastChat?.timestamp < timeCutoff)
                {
                    _logger.LogInformation("Thread {threadId} has not been updated in 30 seconds. Sending to AIbert.", thread.threadId);
                    var numChatsPrevious = thread.chats.Count;
                    await _chatGPT.ShouldRespond(thread);
                        
                    if (thread.chats.Count > numChatsPrevious)
                    {
                        _logger.LogInformation("Found new chat, sending to Slack.");

                        var text = System.Web.HttpUtility.UrlEncode(thread.chats.Last().message);
                        var body = new
                        {
                            channel = thread.threadId,
                            text
                        };
                        var request = new HttpRequestMessage
                        {
                            Method = HttpMethod.Post,
                            Headers =
                            {
                                { HttpRequestHeader.ContentType.ToString(), "application/json; charset=utf-8" },
                                { HttpRequestHeader.Authorization.ToString(), $"Bearer {_slackToken}" }
                            },
                            RequestUri = new Uri($"https://slack.com/api/chat.postMessage"),
                            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                        };

                        try
                        {
                            using var res = await _client.SendAsync(request);
                            res.EnsureSuccessStatusCode();
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Error sending chat to Slack"); 
                        }
                    }
                }
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            return response;
        }
    }
}
