using System.Reflection;
using System.Threading.RateLimiting;
using LunoMcpServer;
using LunoMcpServer.Audit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
// Add MCP HTTP server services
builder.Services.AddMcpServer(o =>
    {
        // Configure options here
        o.ServerInfo = new ModelContextProtocol.Protocol.Implementation
        {
            Name = "Luno MCP Server",
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            Title = "Luno MCP Server"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Register audit logger as singleton and hosted service using same instance
builder.Services.AddSingleton<FileAuditLogger>();
builder.Services.AddSingleton<IAuditLogger>(sp => sp.GetRequiredService<FileAuditLogger>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<FileAuditLogger>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LunoTools>();

// Add Basic Authentication
builder.Services.AddAuthorization();
/*builder.Services.AddAuthentication("Basic")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", null);
*/
var app = builder.Build();

// Use authentication/authorization middleware
//app.UseAuthentication();
//app.UseAuthorization();

// Map MCP HTTP server endpoints
app.MapMcp();//.RequireAuthorization();

app.Run();
