using AIbert.Api.Functions;
using AIbert.Api.Services;
using AIbert.Models;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace AIbert.Api.Core;

public class ChatGPT
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly BlobStorageService _blobStorageService;

    public ChatGPT(ILoggerFactory loggerFactory, IConfiguration config)
    {
        _logger = loggerFactory.CreateLogger<ChatFunction>();
        _config = config;
        _blobStorageService = new BlobStorageService(config.GetValue<string>("StorageAccountConnectionString"), "config");
    }

    public async Task ShouldRespond(ChatThread thread)
    {
        if (thread.HasChangedSinceLastCheck || !TimeBufferHasBeenReached(thread.chats.LastOrDefault()))
        {
            _logger.LogInformation("Not repsonding yet: Time buffer not passed yet.");
            return;
        }

        _logger.LogInformation("Acknowledging message: {0}", thread.chats.Last().chatId);

        IKernel kernel = GetKernel();
        var prompt = await _blobStorageService.GetInitialSystemPrompt() ?? throw new ChatGPTSystemPromptNotFoundException("Could not find initial system prompt in blob.");
        var (context, functionConfig) = await GetKernelBuilder(kernel, prompt);

        var ask = kernel.RegisterSemanticFunction("AIbert", "Chat", functionConfig);

        context.Variables["history"] = JsonSerializer.Serialize(thread.chats);

        try
        {
            var bot_answer = await ask.InvokeAsync(context);
            var bot_answer_string = bot_answer.ToString();

            _logger.LogInformation($"Should AIbert respond? {bot_answer_string}");

            //if (bot_answer_string.Contains("confirmed"))
            //{
            //    await Chat(thread);
            //}
            //else
            //{
                thread.chats.Add(new Chat(Guid.Empty, bot_answer_string, "AIbert", DateTime.Now));
                thread.HasChangedSinceLastCheck = true;
            //}
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in parsing bot answer");
        }
    }

    private async Task Chat(ChatThread thread)
    {
        IKernel kernel = GetKernel();
        var prompt = await _blobStorageService.GetSystemPrompt() ?? throw new ChatGPTSystemPromptNotFoundException("Could not find system prompt in blob.");
        var (context, functionConfig) = await GetKernelBuilder(kernel, prompt);

        var ask = kernel.RegisterSemanticFunction("AIbert", "Chat", functionConfig);

        context.Variables["history"] = JsonSerializer.Serialize(thread.chats);

        var participants = new List<string>();
        thread.chats.ToList().ForEach(t => participants.Add(t.userId));
        context.Variables["participants"] = string.Join(",", participants.Distinct());

        try
        {
            var bot_answer = await ask.InvokeAsync(context);
            _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} AIbert:: {bot_answer}");
            var answer = JsonSerializer.Deserialize<Answer>(bot_answer.ToString());
            context.Variables.Update(string.Join("\n", thread.chats, "\nAIbert: ", answer, "\n"));

            if (!string.IsNullOrEmpty(answer?.response) && !answer.response.ToLower().Contains("already confirmed"))
            {
                thread.chats.Add(new Chat(Guid.Empty, answer.response, "AIbert", DateTime.Now));
                thread.HasChangedSinceLastCheck = true;

                if (answer?.confirmed.ToLower() == "true")
                {
                    thread.promises.Add(new Promise(Guid.Empty, answer.promise, answer.deadline, answer.promisor, answer.promiseHolder));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in parsing bot answer");
        }

        context.Variables["promises"] = $"Active Promises:\n{JsonSerializer.Serialize(thread.promises)}";
    }

    private bool TimeBufferHasBeenReached(Chat? lastMessage)
    {
        if (lastMessage == null)
        {
            _logger.LogInformation("Not repsonding yet: no messages.");
            return false;
        }

        if (Guid.Empty == lastMessage.chatId)
        {
            _logger.LogInformation("Not repsonding yet: Last message is AIbert.");
            return false;
        }

        _logger.LogInformation("{0} <= {1}", lastMessage.timestamp, DateTime.Now.AddSeconds(-15));
        return lastMessage.timestamp <= DateTime.Now.AddSeconds(-15);
    }

    private async Task<(SKContext context, SemanticFunctionConfig config)> GetKernelBuilder(IKernel kernel, string prompt)
    {
        var topP = await _blobStorageService.GetTopP();
        var temperature = await _blobStorageService.GetTemperature();
        var promptConfig = new PromptTemplateConfig
        {
            Completion =
        {
            MaxTokens = 2000,
            Temperature = (double)temperature,
            TopP = (double)topP,
        }
        };
        string skPrompt = @$"{prompt}";
        var promptTemplate = new PromptTemplate(skPrompt, promptConfig, kernel);
        var functionConfig = new SemanticFunctionConfig(promptConfig, promptTemplate);

        return (kernel.CreateNewContext(), functionConfig);
    }

    private IKernel GetKernel()
    {
        var builder = new KernelBuilder();

        builder.WithOpenAIChatCompletionService(
                    "gpt-3.5-turbo",
                    _config.GetValue<string>("OpenAiKey"));

        var kernel = builder.Build();
        return kernel;
    }
}

public class ChatGPTSystemPromptNotFoundException : Exception
{
    public ChatGPTSystemPromptNotFoundException(string message)
        : base(message)
    {
    }

    public ChatGPTSystemPromptNotFoundException(string message, Exception exception)
        : base(message, exception)
    {
    }
}
