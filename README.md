# MassTransit Single-Job Processor with Azure Blob Lease and Azure Table Storage Progress

A comprehensive .NET solution demonstrating single-job processing using MassTransit with RabbitMQ, Azure Storage for distributed locking and progress tracking.

## Overview

This solution includes:
- **Worker service** that ensures only one job runs at a time using Azure Blob leases
- **Lease renewal** and graceful cancellation handling
- **Full job progress logging** to Azure Table Storage (one record per step/event)
- **Separate Publisher** utility to enqueue jobs for testing
- **Web Dashboard** for monitoring job progress and history
- **Free/Open Source** - Uses RabbitMQ instead of Azure Service Bus

## Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Publisher     │───▶│    RabbitMQ      │───▶│ MassTransit     │
│   (Console App) │    │     (Queue)      │    │ Worker Service  │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                                        │
                                                        ▼
                            ┌─────────────────┐    ┌─────────────────┐
                            │ Azure Blob      │    │ Azure Table     │
                            │ Storage (Lock)  │    │ Storage (Audit) │
                            └─────────────────┘    └─────────────────┘
                                                        │
                                                        ▼
                            ┌─────────────────┐    ┌─────────────────┐
                            │   Dashboard     │◀───┤     API         │
                            │  (Web UI)       │    │   Endpoints     │
                            └─────────────────┘    └─────────────────┘
```

## Projects Structure

```
MassTransitJobProcessor/
├── src/
│   ├── Shared/
│   │   └── JobContracts/           # Shared message contracts
│   ├── MassTransitSingleJob/       # Main worker service
│   ├── Publisher/                  # Job publishing utility
│   └── Dashboard/                  # Web monitoring dashboard
└── MassTransitJobProcessor.sln
```

## Prerequisites

- .NET 8.0 SDK or later
- **RabbitMQ Server** (for messaging)
- **Azure Storage Emulator** or Azure Storage Account (for blob leases and table storage)

### Quick Setup with Docker

Start RabbitMQ and Azure Storage Emulator with Docker:

```bash
# Start RabbitMQ
docker run -d --hostname my-rabbit --name some-rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# Start Azure Storage Emulator (Azurite)
docker run -d --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

RabbitMQ Management UI will be available at: http://localhost:15672 (guest/guest)

## Configuration

### 1. Development Setup (Default)

The solution is pre-configured to work with local development tools:

- **RabbitMQ**: localhost with default guest/guest credentials
- **Azure Storage**: Uses Azure Storage Emulator (Azurite) with `UseDevelopmentStorage=true`

### 2. Production Configuration

For production deployments, update the connection strings in each project's `appsettings.json`:

#### MassTransitSingleJob/appsettings.json

```json
{
  "RabbitMQ": {
    "Host": "your-rabbitmq-server.com",
    "Username": "your-username",
    "Password": "your-password",
    "QueueName": "job-queue"
  },
  "AzureBlob": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=...",
    "ContainerName": "locks",
    "LockBlobName": "job-lock"
  },
  "AzureTable": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=...",
    "TableName": "JobProgress"
  },
  "MassTransit": {
    "ConcurrentMessageLimit": 1
  }
}
```

#### Publisher/appsettings.json

```json
{
  "RabbitMQ": {
    "Host": "your-rabbitmq-server.com",
    "Username": "your-username",
    "Password": "your-password",
    "QueueName": "job-queue"
  }
}
```

#### Dashboard/appsettings.json

```json
{
  "AzureTable": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=yourstorageaccount;AccountKey=...",
    "TableName": "JobProgress"
  }
}
```

## Running the Solution

### 1. Build the Solution
```bash
dotnet build
```

### 2. Start the Worker Service
```bash
cd src/MassTransitSingleJob
dotnet run
```
The worker will start listening for jobs and log its status to the console.

### 3. Start the Dashboard (Optional)
```bash
cd src/Dashboard
dotnet run --urls "http://localhost:5002"
```
Open `http://localhost:5002` in your browser to monitor job progress.

### 4. Publish Jobs for Testing

```bash
cd src/Publisher
dotnet run
```

Press any key to publish a job, or 'q' to quit.

## How It Works

### Job Processing Flow

1. **Job Publication**: Publisher sends a `RunJob` message to RabbitMQ
2. **Job Reception**: MassTransit worker receives the message
3. **Lock Acquisition**: Worker attempts to acquire an Azure Blob lease
   - If successful: Job starts processing
   - If failed: Message is deferred for 10 seconds
4. **Progress Tracking**: Each processing step is logged to Azure Table Storage
5. **Lease Renewal**: Background task renews the lease every 40 seconds
6. **Job Completion**: Lease is released, final status logged

### Table Storage Structure
Each job event creates a new row in Azure Table Storage:

| PartitionKey (JobId) | RowKey (UUID) | Status | Message | TimestampUtc |
|----------------------|---------------|---------|----------|---------------|
| 12345678-1234-...    | abcd-efgh-... | Started | Job started and lease acquired | 2025-10-25T10:00:00Z |
| 12345678-1234-...    | ijkl-mnop-... | Step 1  | Processing step 1 | 2025-10-25T10:00:05Z |
| 12345678-1234-...    | qrst-uvwx-... | Completed | Job completed successfully | 2025-10-25T10:01:00Z |

### Single-Job Guarantee
- Only one worker can hold the blob lease at a time
- If a worker crashes, the lease expires after 60 seconds
- Other workers defer processing until the lease becomes available

## Dashboard Features

The web dashboard provides:
- **Recent Jobs**: View all recent job events across all jobs
- **Job Details**: Enter a specific Job ID to see its complete processing history
- **Real-time Updates**: Refresh to see the latest job status

### API Endpoints
- `GET /api/jobs` - Returns recent job events (limited to 200)
- `GET /api/jobs/{jobId}` - Returns all events for a specific job in chronological order

## Monitoring and Logging

### Console Logging
The worker service uses Serilog to output structured logs:
```
[10:30:15 INF] 12345678-abcd-...: Attempting to acquire lease...
[10:30:15 INF] 12345678-abcd-...: Lease acquired (LeaseId: xyz). Running job...
[10:30:15 INF] 12345678-abcd-...: Logged Started - Job started and lease acquired.
[10:30:20 INF] 12345678-abcd-...: Processing step 1/10
```

### Azure Table Storage Audit Trail
Every job action is permanently recorded with:
- Job ID (PartitionKey)
- Unique event ID (RowKey)  
- Status and descriptive message
- Precise timestamp

## Error Handling

### Lease Conflicts
When multiple workers compete for the same job:
```csharp
catch (RequestFailedException ex)
{
    _logger.LogInformation("{JobId}: Could not acquire lease, deferring message. ({Message})", 
        context.Message.JobId, ex.Message);
    await context.Defer(TimeSpan.FromSeconds(10));
}
```

### Job Cancellation
Graceful shutdown and cancellation:
```csharp
catch (OperationCanceledException)
{
    await _progressRepo.LogProgressAsync(context.Message.JobId, "Cancelled", "Job cancelled.");
    _logger.LogWarning("{JobId}: Job cancelled.", context.Message.JobId);
}
```

### Processing Failures
All exceptions are logged to both console and Azure Table Storage:
```csharp
catch (Exception ex)
{
    await _progressRepo.LogProgressAsync(context.Message.JobId, "Failed", ex.Message);
    _logger.LogError(ex, "{JobId}: Job processing failed.", context.Message.JobId);
    throw; // Re-throw for MassTransit retry handling
}
```

## Customization

### Job Processing Logic
Modify the `ProcessJob` method in `JobConsumer.cs` to implement your specific business logic:

```csharp
private async Task ProcessJob(RunJob message, CancellationToken token)
{
    // Replace this with your actual job processing logic
    for (int i = 0; i < 10; i++)
    {
        token.ThrowIfCancellationRequested();
        
        // Your custom processing step here
        await DoCustomWork(message, i, token);
        
        await _progressRepo.LogProgressAsync(message.JobId, $"Step {i + 1}", $"Completed step {i + 1}.");
        await Task.Delay(TimeSpan.FromSeconds(5), token);
    }
}
```

### Message Contracts
Extend the `RunJob` record in `JobContracts` to include additional job parameters:

```csharp
public record RunJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public string JobName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    // Add your custom properties here
    public string? InputFilePath { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
}
```

## Troubleshooting

### Common Issues

1. **Connection String Errors**
   - Verify Azure Service Bus and Storage connection strings
   - Ensure proper access policies are configured

2. **Lease Acquisition Failures**
   - Check if multiple workers are running with the same configuration
   - Verify blob storage permissions

3. **Table Storage Issues**
   - Confirm storage account has Table service enabled
   - Check if table creation permissions are available

### Debug Mode
To enable detailed logging, modify the log level in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

## Performance Considerations

- **Lease Duration**: 60-second lease with 40-second renewal provides good balance
- **Concurrent Limit**: Set to 1 to ensure single-job processing
- **Message Deferral**: 10-second deferral prevents excessive retry attempts
- **Table Storage**: Partitioned by JobId for efficient querying

## Security Best Practices

- Store connection strings in Azure Key Vault for production
- Use Managed Identity when running in Azure
- Implement proper access controls on Azure resources
- Enable Azure Storage encryption and access logging

## License

This project is provided as-is for educational and demonstration purposes.