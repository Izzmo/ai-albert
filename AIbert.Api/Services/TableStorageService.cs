using Azure;
using Azure.Data.Tables;
using System.Linq.Expressions;

namespace AIbert.Api.Services;

public class BaseEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public BaseEntity()
    { }

    public BaseEntity(string partitionKey, string rowKey)
    {
        PartitionKey = partitionKey;
        RowKey = rowKey;
    }
}

public class TableStorageService<T> where T : BaseEntity
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly TableClient _tableClient;

    public TableStorageService(string connectionString, string tableName)
    {
        _tableServiceClient = new TableServiceClient(connectionString);
        _tableClient = _tableServiceClient.GetTableClient(tableName);
    }

    public async Task<IEnumerable<T>> GetEntitiesAsync()
    {
        return await SearchEntitiesAsync(x => !string.IsNullOrWhiteSpace(x.PartitionKey));
    }

    public async Task<IEnumerable<T>> SearchEntitiesAsync(Expression<Func<T, bool>> searchExp)
    {
        var entities = new List<T>();

        await foreach (var entity in _tableClient.QueryAsync<T>(searchExp))
        {
            entities.Add(entity);
        }

        return entities;
    }

    public async Task AddRow(T entity)
    {
        await _tableClient.UpsertEntityAsync(entity);
    }

    public async Task DeleteRow(string partitionKey, string rowKey)
    {
        await _tableClient.DeleteEntityAsync(partitionKey, rowKey);
    }
}
