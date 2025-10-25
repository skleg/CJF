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
        
        // Random job name generator
        var adjectives = new[] { "Urgent", "Critical", "Routine", "Priority", "Express", "Standard", "Advanced", "Quick", "Complex", "Simple" };
        var tasks = new[] { "Data Processing", "File Analysis", "Report Generation", "System Backup", "Data Migration", "Content Review", "Performance Test", "Security Scan", "Index Rebuild", "Cache Refresh" };
        var random = new Random();
        
        while (true)
        {
            var key = Console.ReadKey(true);
            
            if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                break;

            var jobId = Guid.NewGuid();
            var adjective = adjectives[random.Next(adjectives.Length)];
            var task = tasks[random.Next(tasks.Length)];
            var jobName = $"{adjective} {task}";
            
            var job = new RunJob
            {
                JobId = jobId,
                JobName = jobName,
                Description = $"Automated {task.ToLower()} task with {adjective.ToLower()} priority",
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
