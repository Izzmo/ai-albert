using AIbert.Api.Core;
using AIbert.Api.Services;
using AIbert.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIbert.Core;

public class MessageHandler
{
    private readonly ILogger _logger;
    private readonly TableStorageService<ThreadEntity> _threadService;

    public MessageHandler(ILoggerFactory loggerFactory, TableStorageService<ThreadEntity> threadService)
    {
        _logger = loggerFactory.CreateLogger<MessageHandler>();
        _threadService = threadService;
    }

    public async Task AddChatToThread(string threadLookupId, string userId, string message, DateTimeOffset date)
    {
        _logger.LogInformation("Adding chat to thread lookup {threadLookupId}: {message}", threadLookupId, message);

        var threadEntity = _threadService.SearchEntitiesAsync(x => x.PartitionKey == threadLookupId).Result.FirstOrDefault();
        threadEntity ??= new ThreadEntity(threadLookupId, threadLookupId);

        var thread = threadEntity.ConvertTo();
        thread.chats.Add(new Chat(Guid.NewGuid(), message, userId, date));
        await _threadService.AddRow(ThreadEntity.ConvertFromChatThread(thread));
    }

    public async Task UpdateWholeThread(ChatThread thread)
    {
        _logger.LogInformation("Update whole thread {threadId}: {message}", thread.threadId, JsonSerializer.Serialize(thread));
        await _threadService.AddRow(ThreadEntity.ConvertFromChatThread(thread));
    }
}
