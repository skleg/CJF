using Azure;
using Azure.Data.Tables;

namespace MassTransitSingleJob;

public class JobProgressEntity : ITableEntity
{
    public string PartitionKey { get; set; } = default!; // JobId
    public string RowKey { get; set; } = default!; // Unique per event
    public string JobName { get; set; } = string.Empty; // Human-friendly job name
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}