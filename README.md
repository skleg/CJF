# Distributed Job Processor

A robust .NET solution for processing jobs in a distributed environment with guaranteed single-job execution, comprehensive audit trails, and real-time monitoring capabilities.

## Overview

This solution provides:
- **Single-Job Guarantee** - Ensures only one job processes at a time across multiple worker instances
- **Distributed Locking** - Uses Azure Blob leases for reliable coordination between workers
- **Complete Audit Trail** - Every job event is permanently logged with detailed timestamps
- **Real-Time Monitoring** - Web dashboard for tracking job progress and history
- **Resilient Processing** - Automatic retry handling and graceful shutdown capabilities
- **Cost-Effective** - Uses free/open source messaging with RabbitMQ

## Key Features

✅ **Guaranteed Single Execution** - Distributed lock prevents duplicate job processing  
✅ **Fault Tolerant** - Automatic lease renewal and worker failure recovery  
✅ **Full Observability** - Complete job lifecycle tracking and monitoring  
✅ **Modern UI** - Interactive dashboard with clickable job tracking  
✅ **Random Job Generation** - Built-in job publisher for testing and demonstrations  
✅ **Production Ready** - Comprehensive error handling and logging  

## Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Job Publisher │───▶│   Message Queue  │───▶│  Job Processor  │
│   (Console App) │    │    (RabbitMQ)    │    │ Worker Service  │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                                        │
                                                        ▼
                            ┌─────────────────┐    ┌─────────────────┐
                            │ Distributed     │    │ Audit Trail     │
                            │ Lock (Blob)     │    │ (Table Storage) │
                            └─────────────────┘    └─────────────────┘
                                                        │
                                                        ▼
                            ┌─────────────────┐    ┌─────────────────┐
                            │  Monitoring     │◀───┤   REST API      │
                            │  Dashboard      │    │   Endpoints     │
                            └─────────────────┘    └─────────────────┘
```

## Solution Structure

```
CJF/
├── src/
│   ├── Shared/
│   │   └── JobContracts/           # Shared job definitions
│   ├── Job/                        # Core job processor service
│   ├── Publisher/                  # Job submission utility
│   └── Dashboard/                  # Monitoring web application
└── CJF.sln
```

## Prerequisites

- .NET 8.0 SDK or later
- **Message Queue** (RabbitMQ recommended for development)
- **Azure Storage** (or Azure Storage Emulator for development)

### Quick Development Setup

Start the required services with Docker:

```bash
# Start RabbitMQ with management interface
docker run -d --hostname my-rabbit --name some-rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# Start Azure Storage Emulator (Azurite)
docker run -d --name azurite -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

RabbitMQ Management UI: <http://localhost:15672> (guest/guest)

## Getting Started

### 1. Build the Solution

```bash
dotnet build
```

### 2. Start the Job Processor

```bash
cd src/Job
dotnet run
```

The job processor will start listening for jobs and display status information.

### 3. Start the Monitoring Dashboard (Optional)

```bash
cd src/Dashboard
dotnet run --urls "http://localhost:5002"
```

Open <http://localhost:5002> to monitor job progress in real-time.

### 4. Submit Jobs for Processing

```bash
cd src/Publisher
dotnet run
```

Press any key to submit a new job, or 'q' to quit.

## How It Works

### Job Processing Lifecycle

1. **Job Submission**: Publisher sends a job request to the message queue
2. **Job Reception**: Processor receives the job and attempts to acquire a distributed lock
3. **Lock Acquisition**: 
   - ✅ **Success**: Job begins processing immediately
   - ❌ **Conflict**: Job is deferred until lock becomes available
4. **Progress Tracking**: Each processing step is logged with timestamps
5. **Lock Maintenance**: Background process automatically renews the lock during processing
6. **Completion**: Lock is released and final status is recorded

### Audit Trail Structure

Every job event creates a permanent record:

| Job ID | Event ID | Status | Message | Timestamp |
|--------|----------|---------|----------|-----------|
| job-123 | event-abc | Started | Job processing initiated | 2025-10-25T10:00:00Z |
| job-123 | event-def | Step 1 | Processing data chunk 1/10 | 2025-10-25T10:00:05Z |
| job-123 | event-ghi | Completed | Job finished successfully | 2025-10-25T10:01:00Z |

### Distributed Lock Guarantee

- Only one processor can hold the lock for a job at any time
- Lock automatically expires if a processor crashes (60-second timeout)
- Other processors wait and retry until the lock becomes available
- Prevents duplicate processing across multiple worker instances

## Dashboard Features

The web interface provides:

- **📋 Job History**: View all recent job events across the system
- **🔍 Job Details**: Search for specific jobs by ID to see complete processing history  
- **⏱️ Real-Time Updates**: Live status updates as jobs progress
- **🎯 Clickable Interface**: Interactive job names for easy navigation

### REST API

- `GET /api/jobs` - Returns recent job events (last 200)
- `GET /api/jobs/{jobId}` - Returns complete history for a specific job

## Configuration

### Development (Default)

Pre-configured for local development:

- **Message Queue**: RabbitMQ on localhost with default credentials
- **Storage**: Azure Storage Emulator with development connection string

### Production Deployment

Update connection strings in `appsettings.json` files:

```json
{
  "RabbitMQ": {
    "Host": "your-message-queue-host",
    "Username": "username",
    "Password": "password"
  },
  "AzureBlob": {
    "ConnectionString": "your-azure-storage-connection",
    "ContainerName": "job-locks"
  },
  "AzureTable": {
    "ConnectionString": "your-azure-storage-connection", 
    "TableName": "JobAuditTrail"
  }
}
```

## Monitoring and Logging

### Console Output

The job processor provides structured logging for easy monitoring:

```text
[10:30:15 INF] job-123: Attempting to acquire distributed lock...
[10:30:15 INF] job-123: Lock acquired successfully. Starting job processing...
[10:30:15 INF] job-123: Logged 'Started' - Job processing initiated
[10:30:20 INF] job-123: Processing step 1/10 - Data validation
[10:30:25 INF] job-123: Processing step 2/10 - File processing
```

### Persistent Audit Trail

Every job action is permanently recorded with:

- Unique job identifier for tracking
- Event-specific details and timestamps  
- Status updates and error information
- Complete processing history

## Error Handling and Resilience

### Lock Conflicts

When multiple processors compete for the same job:

```csharp
// Automatic deferral when lock is unavailable
if (!lockAcquired) {
    logger.LogInformation("Job {JobId}: Lock unavailable, deferring for retry", jobId);
    await DeferJobProcessing(TimeSpan.FromSeconds(10));
}
```

### Graceful Shutdown

Clean cancellation handling during system shutdown:

```csharp
// Graceful cleanup on cancellation
try {
    await ProcessJobSteps(job, cancellationToken);
} catch (OperationCanceledException) {
    await LogJobEvent(job.Id, "Cancelled", "Job processing cancelled gracefully");
    logger.LogWarning("Job {JobId}: Processing cancelled", job.Id);
}
```

### Processing Failures

Comprehensive error handling with detailed logging:

```csharp
// Exception handling with audit trail
catch (Exception ex) {
    await LogJobEvent(job.Id, "Failed", $"Processing error: {ex.Message}");
    logger.LogError(ex, "Job {JobId}: Processing failed", job.Id);
    throw; // Allow retry mechanism to handle recovery
}
```

## Customization

### Custom Job Processing

Implement your business logic by modifying the core processing method:

```csharp
private async Task ProcessJob(JobRequest request, CancellationToken cancellationToken)
{
    // Replace with your specific business logic
    for (int step = 1; step <= 10; step++)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // Your custom processing logic here
        await ExecuteBusinessLogic(request, step, cancellationToken);
        
        await LogProgress(request.JobId, $"Step {step}", $"Completed processing step {step}");
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}
```

### Extended Job Definitions

Add custom properties to job requests:

```csharp
public record JobRequest
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public string JobName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    // Add your custom job parameters
    public string? InputFilePath { get; init; }
    public Dictionary<string, object>? ProcessingParameters { get; init; }
    public int Priority { get; init; } = 0;
}
```

## Troubleshooting

### Common Issues

1. **Connection Problems**
   - Verify message queue is running and accessible
   - Check Azure Storage connection strings and permissions
   - Ensure network connectivity to required services

2. **Lock Acquisition Issues**
   - Confirm only one processor instance is configured for the same job type
   - Verify blob storage permissions and container existence
   - Check for network timeouts affecting lock operations

3. **Audit Trail Problems**
   - Ensure table storage permissions are properly configured
   - Verify storage account has table service enabled
   - Check for storage account access key validity

### Debug Configuration

Enable detailed logging by updating `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  }
}
```

## Performance Optimization

- **Lock Duration**: 60-second locks with 40-second renewals provide optimal balance
- **Processing Concurrency**: Single-job mode ensures data consistency
- **Retry Strategy**: 10-second deferrals prevent excessive retry storms
- **Storage Efficiency**: Job ID partitioning enables fast audit trail queries

## Production Considerations

- Store sensitive connection strings in Azure Key Vault
- Use managed identities when deploying to Azure
- Implement proper access controls on storage resources
- Enable Azure Storage encryption and access logging
- Monitor job processing metrics and set up alerts

## Technical Dependencies

This solution leverages several proven technologies:

- **.NET 8.0**: Modern runtime with excellent performance
- **MassTransit**: Reliable message processing framework
- **RabbitMQ**: Battle-tested message broker
- **Azure Storage**: Highly available cloud storage services
- **Serilog**: Structured logging for observability

## License

This project is provided as-is for educational and demonstration purposes.
