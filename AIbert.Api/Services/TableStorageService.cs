using Azure;
using Azure.Data.Tables;

namespace AIbert.Api.Services;

public class PromiseEntity : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // Add additional properties as needed
    public string Name { get; set; }
    public int Age { get; set; }
}

public class TableStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableClient _tableClient;

    public TableStorageService(string connectionString, string tableName)
    {
        _tableServiceClient = new TableServiceClient(connectionString);
        _tableClient = _tableServiceClient.GetTableClient(tableName);
    }

    public async Task<IEnumerable<PromiseEntity>> GetEntitiesAsync()
    {
        var entities = new List<PromiseEntity>();

        await foreach (var entity in _tableClient.QueryAsync<PromiseEntity>(x => x.Name == string.Empty))
        {
            entities.Add(entity);
        }

        return entities;
    }
}
