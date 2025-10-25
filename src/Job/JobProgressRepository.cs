using Azure.Data.Tables;

namespace MassTransitSingleJob;

public class JobProgressRepository
{
    private readonly TableClient _tableClient;
    private readonly ILogger<JobProgressRepository> _logger;

    public JobProgressRepository(TableServiceClient serviceClient, string tableName, ILogger<JobProgressRepository> logger)
    {
        _tableClient = serviceClient.GetTableClient(tableName);
        _tableClient.CreateIfNotExists();
        _logger = logger;
    }

    public async Task LogProgressAsync(Guid jobId, string status, string message)
    {
        var entity = new JobProgressEntity
        {
            PartitionKey = jobId.ToString(),
            RowKey = Guid.NewGuid().ToString(),
            Status = status,
            Message = message,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        await _tableClient.AddEntityAsync(entity);
        _logger.LogInformation("{JobId}: Logged {Status} - {Message}", jobId, status, message);
    }
}