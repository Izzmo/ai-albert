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
    public class DeadlineFunction
    {
        private readonly ILogger _logger;
        private readonly MessageHandler _messageHandler;
        private readonly ChatGPT _chatGPT;
        private readonly string _slackToken;
        private static readonly HttpClient _client = new();

        public DeadlineFunction(ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<DeadlineFunction>();
            var tableStorageService = new TableStorageService<ThreadEntity>(config.GetValue<string>("StorageAccountConnectionString"), "threads");
            _messageHandler = new MessageHandler(loggerFactory, tableStorageService);
            _chatGPT = new ChatGPT(loggerFactory, config);
            _slackToken = config.GetValue<string>("SlackToken");
        }

        [Function("DeadlineFunction")]
        public async Task<HttpResponseData> DeadlineFunctionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "CheckDeadlines")] HttpRequestData req)
        {
            var timeCutoff = DateTimeOffset.UtcNow.AddHours(1);
            var threads = await _messageHandler.GetAllThreads();
            foreach (var thread in threads)
            {
                foreach(var promise in thread.promises)
                {
                    _logger.LogInformation("Checking thread {threadId}, Deadline: {deadline}", thread.threadId, promise.Deadline);
                    
                    var utcTime = DateTimeOffset.UtcNow;
                    var pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                    var pacificTimeNow = TimeZoneInfo.ConvertTime(utcTime, pacificTimeZone);

                    var promiseDeadline = DateTimeOffset.Parse(promise.Deadline);
                    if (promiseDeadline >= pacificTimeNow && promiseDeadline <= timeCutoff)
                    {
                        _logger.LogInformation("Thread {threadId} has a promise that is due soon. Sending to Slack.", thread.threadId);

                        var body = new
                        {
                            channel = thread.threadId,
                            text = $"Just a reminder that a promise is due soon! The promise is _{promise.Description}_. {promise.Promiser}, let us know when it's been fulfilled, or if we need to reschedule or cancel it."
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
