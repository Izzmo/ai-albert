using System.Net;
using System.Text.Json;
using AIbert.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.SemanticFunctions;

namespace AIbert.Api.Functions;
public record ChatInput(string input);

public class ChatFunction
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly BlobStorageService _blobStorageService;
    private static readonly List<string> _history = new();

    public ChatFunction(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<ChatFunction>();
        _config = config;
        _blobStorageService = new BlobStorageService(config.GetConnectionString("AzStorage"), "config");
    }

    [Function("Chat")]
    public async Task<HttpResponseData> ChatAsync([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        var dataToBeSaved = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<ChatInput>(dataToBeSaved);

        if (data == null || string.IsNullOrWhiteSpace(data.input))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

        try
        {
            response.WriteString(await Chat(data.input));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error with chat.");
        }

        return response;
    }

    private async Task<string> Chat(string input)
    {
        var builder = new KernelBuilder();

        builder.WithOpenAIChatCompletionService(
                 "gpt-3.5-turbo",
                 _config.GetValue<string>("OpenAiKey"));

        var kernel = builder.Build();
        var prompt = await _blobStorageService.GetSystemPrompt() ?? string.Empty;
        string skPrompt = @$"{prompt}

{{$history}}
User: {{$userInput}}
AIbert:";

        var promptConfig = new PromptTemplateConfig
        {
            Completion =
            {
                MaxTokens = 2000,
                Temperature = 0.7,
                TopP = 0.5,
            }
        };

        var promptTemplate = new PromptTemplate(skPrompt, promptConfig, kernel);
        var functionConfig = new SemanticFunctionConfig(promptConfig, promptTemplate);
        var ask = kernel.RegisterSemanticFunction("AIbert", "Chat", functionConfig);

        var context = kernel.CreateNewContext();
        context.Variables["history"] = string.Join("\n", _history);

        var userInput = input;
        context.Variables["userInput"] = userInput;

        var bot_answer = await ask.InvokeAsync(context);
        _history.Insert(0, $"\n\nUser: {userInput}\nAIbert: {bot_answer}\n");
        context.Variables.Update(string.Join("\n", _history));

        return JsonSerializer.Serialize(_history);
    }
}