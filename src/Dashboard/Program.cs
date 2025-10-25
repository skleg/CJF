using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    var results = new List<object>();
    // Query recent entities (limit 200)
    await foreach (var e in client.QueryAsync<JobProgressEntity>(maxPerPage: 200))
    {
        results.Add(new { e.PartitionKey, e.RowKey, e.Status, e.Message, e.TimestampUtc });
    }
    return Results.Json(results);
});

app.MapGet("/api/jobs/{jobId}", async (string jobId, TableClient client) =>
{
    var partition = jobId;
    var list = new List<JobProgressEntity>();
    await foreach (var page in client.QueryAsync<JobProgressEntity>(e => e.PartitionKey == partition))
    {
        list.Add(page);
    }
    return Results.Json(list.OrderBy(e => e.TimestampUtc));
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
