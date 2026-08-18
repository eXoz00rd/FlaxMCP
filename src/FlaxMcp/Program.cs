using FlaxMcp.Configuration;
using FlaxMcp.Flax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(
    Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable(FlaxMcpOptions.LogLevelVariable), true, out var minimumLevel) ?
        minimumLevel :
        LogLevel.Warning
);

builder.Services.AddSingleton<IValidateOptions<FlaxMcpOptions>, FlaxMcpOptionsValidator>();
builder.Services.AddSingleton<FlaxContentIndex>();
builder.Services
       .AddOptions<FlaxMcpOptions>()
       .Configure(options => options.LoadFromEnvironment())
       .ValidateOnStart();

var startupOptions = new FlaxMcpOptions();
startupOptions.LoadFromEnvironment();

var toolCount = ToolRegistration.AddTools(builder.Services, startupOptions);

builder.Services
       .AddMcpServer(options => options.ServerInstructions = ServerInstructions.Build(startupOptions, toolCount))
       .WithStdioServerTransport();

await builder.Build().RunAsync();
