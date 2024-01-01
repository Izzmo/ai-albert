﻿using AIbert.Api.Functions;
using AIbert.Api.Services;
using AIbert.Models;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SemanticFunctions;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace AIbert.Api.Core
{
    public class ChatGPT
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _config;
        private readonly BlobStorageService _blobStorageService;
        private readonly TableStorageService<ThreadEntity> _tableStorageService;

        public ChatGPT(ILoggerFactory loggerFactory, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<ChatFunction>();
            _config = config;
            _blobStorageService = new BlobStorageService(config.GetValue<string>("StorageAccountConnectionString"), "config");
            _tableStorageService = new TableStorageService<ThreadEntity>(config.GetValue<string>("StorageAccountConnectionString"), "threads");
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
            string skPrompt = "Given the chat history below, is there a promise being made? If so, do you know the promisee, promise keeper, description of the promise, and the deadline of the promise? If one of these is not clear, then respond with a statement asking the promise keeper to clarify in a helpful way. If all parts are clear, respond with 'confirmed'.\n\nHistory:\n{{$history}}";
            var (context, functionConfig) = await GetKernelBuilder(kernel, skPrompt);

            var ask = kernel.RegisterSemanticFunction("AIbert", "Chat", functionConfig);

            context.Variables["history"] = JsonSerializer.Serialize(thread.chats);

            try
            {
                var bot_answer = await ask.InvokeAsync(context);
                _logger.LogInformation($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} should respond? AIbert:: {bot_answer}");
                await Chat(thread);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in parsing bot answer");
            }
        }

        private async Task Chat(ChatThread thread)
        {
            IKernel kernel = GetKernel();
            var prompt = await _blobStorageService.GetSystemPrompt() ?? string.Empty;
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

                    await _tableStorageService.AddRow(ThreadEntity.ConvertFromChatThread(thread));
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
}
