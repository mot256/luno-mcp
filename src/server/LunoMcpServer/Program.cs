using System.Reflection;
using LunoMcpServer;
using LunoMcpServer.Audit;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging
.AddConsole(o =>
{    
    // Configure all logs to go to stderr
    // This is needed to ensure that all logs are shown on the console for debugging purposes.
    // But also to ensure that the MCP client receives replies via the STDOUT (see MCP spec ).
    o.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Trace;
}).SetMinimumLevel(Debugger.IsAttached ? Microsoft.Extensions.Logging.LogLevel.Trace : Microsoft.Extensions.Logging.LogLevel.Error);


builder.Configuration.AddUserSecrets<Program>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<FileAuditLogger>();
builder.Services.AddSingleton<IAuditLogger>(sp => sp.GetRequiredService<FileAuditLogger>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<FileAuditLogger>());
builder.Services.AddScoped<LunoTools>();

builder.Services.AddMcpServer(o =>
{
    o.ServerInfo = new ModelContextProtocol.Protocol.Implementation
    {
        Name = "Luno MCP Server",
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
        Title = "Luno MCP Server"
    };
})
    .WithStdioServerTransport()
    .WithTools<LunoTools>();

var host = builder.Build();
await host.RunAsync();
