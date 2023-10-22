using System.Net;
using System.Security;
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

public class ChatFunction
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly BlobStorageService _blobStorageService;
    private static readonly List<string> _history = new();
    private static readonly List<Promise> _promises = new();

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
            await Chat(data.thread);
            response.WriteString(JsonSerializer.Serialize(new ChatResponse(_history, _promises)));
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

        await response.WriteStringAsync(JsonSerializer.Serialize(new ChatResponse(_history, _promises)));

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

    private async Task Chat(List<string> thread)
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

        _history.Clear();
        _history.AddRange(thread);
        context.Variables["history"] = JsonSerializer.Serialize(_history);

        var participants = new List<string>();
        _history.ForEach(t => participants.Add(t.Split(":")[0]));
        context.Variables["participants"] = string.Join(",", participants.Distinct());

        try
        {
            var bot_answer = await ask.InvokeAsync(context);
            _logger.LogInformation($"AIbert: {bot_answer}");
            var answer = JsonSerializer.Deserialize<Answer>(bot_answer.ToString());
            context.Variables.Update(string.Join("\n", _history, "\nAIbert: ", answer, "\n"));

            if (!string.IsNullOrEmpty(answer?.response))
                _history.Add($"AIbert: {answer?.response}");

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
}
