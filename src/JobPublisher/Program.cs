using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using JobContracts;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        var configuration = hostContext.Configuration;
        var asbConn = configuration.GetValue<string>("AzureServiceBus:ConnectionString");

        services.AddMassTransit(x =>
        {
            x.UsingAzureServiceBus((context, cfg) =>
            {
                cfg.Host(asbConn);
            });
        });
    });

var host = builder.Build();

var publishEndpoint = host.Services.GetRequiredService<IPublishEndpoint>();

Console.WriteLine("Job Publisher - Press any key to send a job, or 'q' to quit");

while (true)
{
    var key = Console.ReadKey(true);
    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
        break;

    var job = new RunJob
    {
        JobId = Guid.NewGuid(),
        JobName = $"Test Job {DateTime.Now:HH:mm:ss}",
        Description = "A test job to demonstrate the processing pipeline",
        CreatedAt = DateTime.UtcNow
    };

    await publishEndpoint.Publish(job);
    Console.WriteLine($"Published job: {job.JobId} - {job.JobName}");
}

Console.WriteLine("Publisher stopped.");
await host.StopAsync();
