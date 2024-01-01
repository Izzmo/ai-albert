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
        //public async Task Run([TimerTrigger("0 */5 * * * *")] Microsoft.Azure.WebJobs.TimerInfo myTimer)
        public async Task<HttpResponseData> TimerTriggerAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "CheckChats")] HttpRequestData req)
        {
            //if (myTimer.ScheduleStatus is not null)
            //{
                //_logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");

                var timeCutoff = DateTimeOffset.UtcNow.AddSeconds(-30);
                var threads = await _messageHandler.GetAllThreads();
                foreach (var thread in threads)
                {
                    var lastChat = thread.chats.LastOrDefault();
                    if (lastChat?.userId != "AIbert" && lastChat?.timestamp < timeCutoff)
                    {
                        _logger.LogInformation("Thread {threadId} has not been updated in 30 seconds. Sending to AIbert.", thread.threadId);
                        var numChatsPrevious = thread.chats.Count;
                        await _chatGPT.ShouldRespond(thread);
                        
                        if (thread.chats.Count > numChatsPrevious)
                        {
                            var body = new
                            {
                                text = thread.chats.Last().message
                            };
                            var request = new HttpRequestMessage
                            {
                                Method = HttpMethod.Post,
                                RequestUri = new Uri($"https://hooks.slack.com/services/${_slackToken}"),
                                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
                            };

                            using var res = await _client.SendAsync(request);
                            res.EnsureSuccessStatusCode();
                        }
                    }
                }
            //}
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            return response;
        }
    }
}
