namespace AIbert.Models;

public record Settings(string InitialSystemPrompt, string SystemPrompt, string PromisePrompt, decimal TopP, decimal Temperature);
