namespace AIbert.Models;

public record ChatInput(ChatThread thread);

public record ChatResponse(ChatThread thread, string debugLog);

public record Chat(Guid chatId, string message, string userId, DateTimeOffset timestamp);
