namespace AIbert.Models;

public record ChatInput(List<string> thread);
public record ChatResponse(List<string> thread, List<Promise> promises);
