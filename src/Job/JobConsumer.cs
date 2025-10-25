using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using MassTransit;
using JobContracts;

namespace MassTransitSingleJob;

public class BlobLockOptions
{
    public string LockBlobName { get; set; } = "job-lock";
}

public class JobConsumer : IConsumer<RunJob>
{
    private readonly BlobContainerClient _containerClient;
    private readonly BlobLockOptions _options;
    private readonly ILogger<JobConsumer> _logger;
    private readonly JobProgressRepository _progressRepo;

    public JobConsumer(BlobContainerClient containerClient, BlobLockOptions options, ILogger<JobConsumer> logger, JobProgressRepository progressRepo)
    {
        _containerClient = containerClient;
        _options = options;
        _logger = logger;
        _progressRepo = progressRepo;
    }

    public async Task Consume(ConsumeContext<RunJob> context)
    {
        await _containerClient.CreateIfNotExistsAsync();
        var blobClient = _containerClient.GetBlobClient(_options.LockBlobName);
        await blobClient.UploadAsync(BinaryData.FromString("lock"), overwrite: true);

        var leaseClient = blobClient.GetBlobLeaseClient();
        var cts = new CancellationTokenSource();
        context.CancellationToken.Register(cts.Cancel);

        try
        {
            _logger.LogInformation("{JobId}: Attempting to acquire lease...", context.Message.JobId);
            var lease = await leaseClient.AcquireAsync(TimeSpan.FromSeconds(60));
            _logger.LogInformation("{JobId}: Lease acquired (LeaseId: {LeaseId}). Running job...", context.Message.JobId, lease.Value.LeaseId);

            var renewalTask = RenewLeasePeriodicallyAsync(leaseClient, TimeSpan.FromSeconds(40), cts.Token);

            await _progressRepo.LogProgressAsync(context.Message.JobId, context.Message.JobName, "Started", "Job started and lease acquired.");

            try
            {
                await ProcessJob(context.Message, cts.Token);
                await _progressRepo.LogProgressAsync(context.Message.JobId, context.Message.JobName, "Completed", "Job completed successfully.");
                _logger.LogInformation("{JobId}: Job completed successfully.", context.Message.JobId);
            }
            catch (OperationCanceledException)
            {
                await _progressRepo.LogProgressAsync(context.Message.JobId, context.Message.JobName, "Cancelled", "Job cancelled.");
                _logger.LogWarning("{JobId}: Job cancelled.", context.Message.JobId);
            }
            catch (Exception ex)
            {
                await _progressRepo.LogProgressAsync(context.Message.JobId, context.Message.JobName, "Failed", ex.Message);
                _logger.LogError(ex, "{JobId}: Job processing failed.", context.Message.JobId);
                throw;
            }
            finally
            {
                cts.Cancel();
                await Task.WhenAny(renewalTask, Task.Delay(500));
                await leaseClient.ReleaseAsync();
                _logger.LogInformation("{JobId}: Lease released.", context.Message.JobId);
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogInformation("{JobId}: Could not acquire lease, deferring message. ({Message})", context.Message.JobId, ex.Message);
            await context.Defer(TimeSpan.FromSeconds(10));
        }
    }

    private async Task RenewLeasePeriodicallyAsync(BlobLeaseClient leaseClient, TimeSpan renewInterval, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(renewInterval, token);
                try
                {
                    await leaseClient.RenewAsync(cancellationToken: token);
                    _logger.LogDebug("Lease renewed successfully.");
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lease renewal failed.");
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task ProcessJob(RunJob message, CancellationToken token)
    {
        for (int i = 0; i < 10; i++)
        {
            token.ThrowIfCancellationRequested();
            _logger.LogInformation("{JobId}: Processing step {Step}/10", message.JobId, i + 1);
            await _progressRepo.LogProgressAsync(message.JobId, message.JobName, $"Step {i + 1}", $"Processing step {i + 1}.");
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
    }
}