using System.Net;
using System.Text.Json;
using AIbert.Api.Services;
using AIbert.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.SemanticFunctions;

namespace AIbert.Api.Functions;

public record Answer(string promise, string promisor, string promiseHolder, string deadline, string response, string confirmed);
public record ChatMessage(Guid MessageId, DateTime Timestamp, string User, string Message)
{
    public override string ToString()
    {
        return $"{Timestamp:yyyy-MM-dd HH:mm:ss} {User}:: {Message}";
    }
}

public class ChatFunction
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly BlobStorageService _blobStorageService;
    private static readonly List<ChatMessage> _history = new();
    private static readonly List<Promise> _promises = new();
    private static Guid LastMessageAck = Guid.Empty;

    public ChatFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<ChatFunction>();
        _config = config;
        _blobStorageService = new BlobStorageService(config.GetValue<string>("StorageAccountConnectionString"), "config");
    }

    [Function("Chat")]
    public async Task<HttpResponseData> ChatAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Chat")] HttpRequestData req)
    {
        var dataToBeSaved = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<ChatInput>(dataToBeSaved);

        if (data == null || data.thread.Count == 0)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        try
        {
            _history.Clear();
            _history.AddRange(GetChatMessages(data.thread));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with chat.");
        }

        return response;
    }

    [Function("GetChat")]
    public async Task<HttpResponseData> GetChatAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Chat")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        ShouldRespond();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        await response.WriteStringAsync(JsonSerializer.Serialize(new ChatResponse(_history.Select(h => h.ToString()).ToList(), _promises)));

        return response;
    }

    [Function("ClearChat")]
    public static HttpResponseData ClearChat([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "Chat")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);

        _history.Clear();

        return response;
    }

    [Function("ClearPromises")]
    public static HttpResponseData ClearPromises([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "Promise")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);

        _promises.Clear();

        return response;
    }

    private async Task Chat()
    {
        var builder = new KernelBuilder();

        builder.WithOpenAIChatCompletionService(
                 "gpt-3.5-turbo",
                 _config.GetValue<string>("OpenAiKey"));

        var kernel = builder.Build();
        var prompt = await _blobStorageService.GetSystemPrompt() ?? string.Empty;
        var topP = await _blobStorageService.GetTopP();
        var temperature = await _blobStorageService.GetTemperature();
        string skPrompt = @$"{prompt}";

        var promptConfig = new PromptTemplateConfig
        {
            Completion =
            {
                MaxTokens = 2000,
                Temperature = (double)temperature,
                TopP = (double)topP,
            }
        };

        var promptTemplate = new PromptTemplate(skPrompt, promptConfig, kernel);
        var functionConfig = new SemanticFunctionConfig(promptConfig, promptTemplate);
        var ask = kernel.RegisterSemanticFunction("AIbert", "Chat", functionConfig);

        var context = kernel.CreateNewContext();

        context.Variables["history"] = JsonSerializer.Serialize(_history);

        var participants = new List<string>();
        _history.ForEach(t => participants.Add(t.User));
        context.Variables["participants"] = string.Join(",", participants.Distinct());

        try
        {
            var bot_answer = await ask.InvokeAsync(context);
            _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} AIbert:: {bot_answer}");
            var answer = JsonSerializer.Deserialize<Answer>(bot_answer.ToString());
            context.Variables.Update(string.Join("\n", _history, "\nAIbert: ", answer, "\n"));

            if (!string.IsNullOrEmpty(answer?.response))
                _history.Add(new ChatMessage(Guid.Empty, DateTime.Now, "AIbert", answer.response));

            if (answer?.confirmed.ToLower() == "true")
            {
                _promises.Add(new Promise(Guid.NewGuid(), answer.promise, answer.deadline, answer.promisor, answer.promiseHolder));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in parsing bot answer");
        }
        
        context.Variables["promises"] = $"Active Promises:\n{JsonSerializer.Serialize(_promises)}";
    }

    private bool TimeBufferHasBeenReached(ChatMessage? lastMessage)
    {
        if (lastMessage == null)
        {
            _logger.LogInformation("Not repsonding yet: no messages.");
            return false;
        }

        if (Guid.Empty == lastMessage.MessageId)
        {
            _logger.LogInformation("Not repsonding yet: Last message is AIbert.");
            return false;
        }

        if (LastMessageAck == lastMessage.MessageId)
        {
            _logger.LogInformation("Not repsonding yet: Last message has already been acknowledged.");
            return false;
        }

        _logger.LogInformation("{0} <= {1}", lastMessage.Timestamp, DateTime.Now.AddSeconds(-15));
        return lastMessage.Timestamp <= DateTime.Now.AddSeconds(-15);
    }

    private async Task ShouldRespond()
    {
        if (!TimeBufferHasBeenReached(_history.LastOrDefault()))
        {
            _logger.LogInformation("Not repsonding yet: Time buffer not passed yet.");
            return;
        }

        _logger.LogInformation("Acknowledging message: {0}", _history.Last().MessageId);
        LastMessageAck = _history.Last().MessageId;
        var builder = new KernelBuilder();

        builder.WithOpenAIChatCompletionService(
                 "gpt-3.5-turbo",
                 _config.GetValue<string>("OpenAiKey"));

        var kernel = builder.Build();
        var prompt = await _blobStorageService.GetSystemPrompt() ?? string.Empty;
        var topP = await _blobStorageService.GetTopP();
        var temperature = await _blobStorageService.GetTemperature();
        string skPrompt = "Given the chat history below, is there a promise being made? If so, do you know the promisee, promise keeper, description of the promise, and the deadline of the promise? If one of these is not clear, then respond with a statement asking the promise keeper to clarify in a helpful way. If all parts are clear, respond with 'confirmed'.\n\nHistory:\n{{$history}}";

        var promptConfig = new PromptTemplateConfig
        {
            Completion =
            {
                MaxTokens = 2000,
                Temperature = (double)temperature,
                TopP = (double)topP,
            }
        };

        var promptTemplate = new PromptTemplate(skPrompt, promptConfig, kernel);
        var functionConfig = new SemanticFunctionConfig(promptConfig, promptTemplate);
        var ask = kernel.RegisterSemanticFunction("AIbert", "Chat", functionConfig);

        var context = kernel.CreateNewContext();

        context.Variables["history"] = JsonSerializer.Serialize(_history);

        try
        {
            var bot_answer = await ask.InvokeAsync(context);
            _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} AIbert:: {bot_answer}");
            await Chat();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in parsing bot answer");
        }
    }

    private static (DateTime timestamp, string user, string message) GetParts(string chatString)
    {
        var parts = chatString.Split("::");
        var dateAuthorParts = parts[0].Split(" ");
        return (DateTime.Parse($"{dateAuthorParts[0]} {dateAuthorParts[1]}"), dateAuthorParts[2], parts[1]);
    }

    private static List<ChatMessage> GetChatMessages(List<string> threads)
    {
        var chatMessages = new List<ChatMessage>();

        foreach (var thread in threads)
        {
            var parts = GetParts(thread);
            chatMessages.Add(new ChatMessage(Guid.NewGuid(), parts.timestamp, parts.user, parts.message));
        }

        return chatMessages;
    }
}
