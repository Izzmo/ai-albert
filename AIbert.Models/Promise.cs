namespace AIbert.Models;

public record Promise(Guid PromiseId, string Description, string Deadline, string Promiser, string PromiseHolder);
