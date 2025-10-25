using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using JobContracts;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = Host.CreateDefaultBuilder(args)
        .UseSerilog()
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

            services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitMqHost, "/", h =>
                    {
                        h.Username(rabbitMqUser);
                        h.Password(rabbitMqPass);
                    });
                });
            });
        });

    var host = builder.Build();
    await host.StartAsync();

    try
    {
        var publishEndpoint = host.Services.GetRequiredService<IPublishEndpoint>();
        
        Console.WriteLine("Job Publisher - Press any key to send a job, 'q' to quit");
        
        while (true)
        {
            var key = Console.ReadKey(true);
            
            if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                break;

            var jobId = Guid.NewGuid();
            var job = new RunJob
            {
                JobId = jobId,
                JobName = $"Sample Job {DateTime.Now:HH:mm:ss}",
                Description = "A sample job for testing the processing system",
                CreatedAt = DateTime.UtcNow
            };

            await publishEndpoint.Publish(job);
            Console.WriteLine($"Published job: {job.JobId} - {job.JobName}");
        }
    }
    finally
    {
        await host.StopAsync();
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Publisher terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
