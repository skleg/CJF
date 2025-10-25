using Azure;
using Azure.Data.Tables;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Table storage
var configuration = builder.Configuration;
var tableConn = configuration.GetValue<string>("AzureTable:ConnectionString");
var tableName = configuration.GetValue<string>("AzureTable:TableName");
var tableService = new TableServiceClient(tableConn);
var tableClient = tableService.GetTableClient(tableName);
await tableClient.CreateIfNotExistsAsync();

builder.Services.AddSingleton(tableClient);

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapGet("/api/jobs", async (TableClient client) =>
{
    // return recent job summaries (latest event per job)
    var results = new List<JobProgressEntity>();
    // Query recent entities (limit 200)
    await foreach (var e in client.QueryAsync<JobProgressEntity>(maxPerPage: 200))
    {
        results.Add(e);
    }
    // Order by TimestampUtc descending (most recent first) and format for display
    var orderedResults = results
        .OrderByDescending(e => e.TimestampUtc)
        .Select(e => new 
        { 
            JobId = e.PartitionKey,
            Status = e.Status,
            Message = e.Message,
            Time = e.TimestampUtc.ToString("MMM/dd HH:mm:ss")
        });
    
    return Results.Json(orderedResults);
});

app.MapGet("/api/jobs/{jobId}", async (string jobId, TableClient client) =>
{
    var partition = jobId;
    var list = new List<JobProgressEntity>();
    await foreach (var page in client.QueryAsync<JobProgressEntity>(e => e.PartitionKey == partition))
    {
        list.Add(page);
    }
    // Order by TimestampUtc descending (most recent first) and format for display
    var formattedResults = list
        .OrderByDescending(e => e.TimestampUtc)
        .Select(e => new 
        { 
            Status = e.Status,
            Message = e.Message,
            Time = e.TimestampUtc.ToString("MMM/dd HH:mm:ss")
        });
    
    return Results.Json(formattedResults);
});

app.MapFallbackToFile("index.html");

app.Run();

record JobProgressEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
