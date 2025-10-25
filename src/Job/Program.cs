using MassTransit;
using Azure.Storage.Blobs;
using Azure.Data.Tables;
using Serilog;
using MassTransitSingleJob;

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog((ctx, lc) => lc.WriteTo.Console())
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;

        var rabbitMqHost = configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
        var rabbitMqUser = configuration.GetValue<string>("RabbitMQ:Username") ?? "guest";
        var rabbitMqPass = configuration.GetValue<string>("RabbitMQ:Password") ?? "guest";
        var queueName = configuration.GetValue<string>("RabbitMQ:QueueName") ?? "job-queue";

        var blobConn = configuration.GetValue<string>("AzureBlob:ConnectionString") ?? "UseDevelopmentStorage=true";
        var containerName = configuration.GetValue<string>("AzureBlob:ContainerName") ?? "locks";
        var lockBlobName = configuration.GetValue<string>("AzureBlob:LockBlobName") ?? "job-lock";

        var tableConn = configuration.GetValue<string>("AzureTable:ConnectionString") ?? "UseDevelopmentStorage=true";
        var tableName = configuration.GetValue<string>("AzureTable:TableName") ?? "JobProgress";

        services.AddSingleton(_ => new BlobContainerClient(blobConn, containerName));
        services.AddSingleton(sp => new BlobLockOptions { LockBlobName = lockBlobName });

        services.AddSingleton(_ => new TableServiceClient(tableConn));
        services.AddSingleton(sp => new JobProgressRepository(
            sp.GetRequiredService<TableServiceClient>(),
            tableName,
            sp.GetRequiredService<ILogger<JobProgressRepository>>()));

        services.AddMassTransit(x =>
        {
            x.AddConsumer<JobConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitMqHost, "/", h =>
                {
                    h.Username(rabbitMqUser);
                    h.Password(rabbitMqPass);
                });

                cfg.ReceiveEndpoint(queueName, e =>
                {
                    e.ConcurrentMessageLimit = configuration.GetValue<int>("MassTransit:ConcurrentMessageLimit");
                    e.ConfigureConsumer<JobConsumer>(context);
                });
            });
        });
    });

await builder.RunConsoleAsync();
