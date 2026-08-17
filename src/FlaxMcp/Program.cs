using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(
    Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("FLAX_LOG_LEVEL"), true, out var minimumLevel) ?
        minimumLevel :
        LogLevel.Warning
);

builder.Services
       .AddMcpServer()
       .WithStdioServerTransport()
       .WithToolsFromAssembly();

await builder.Build().RunAsync();
