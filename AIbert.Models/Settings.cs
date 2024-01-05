namespace AIbert.Models;

public record Settings(string InitialSystemPrompt, string SystemPrompt, decimal TopP, decimal Temperature);
