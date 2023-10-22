namespace AIbert.Models;

public record ChatInput(string input, string sender, string participant2);
public record ChatResponse(List<string> thread, List<Promise> promises);
