# Luno MCP Server

This project is a C# MCP server that exposes Luno API endpoints as MCP tools.

## Features
- Wraps Luno API endpoints (e.g., get balances, place orders)
- Uses ModelContextProtocol SDK
- Configurable Luno API credentials


## Getting Started
1. Install .NET 9.0 SDK
2. Restore dependencies: `dotnet restore`
3. Configure your Luno API credentials (see below)
4. Run the server: `dotnet run --project src/server/LunoMcpServer/LunoMcpServer.csproj`
5. (Optional) Inspect with MCP Inspector (see below)

## Configuration

### Luno API Credentials
Set the following environment variables before running the server:

- `LUNO_API_KEY_ID` — Your Luno API key ID
- `LUNO_API_KEY_SECRET` — Your Luno API key secret

Example (Linux/macOS):
```sh
export LUNO_API_KEY_ID=your_key_id
export LUNO_API_KEY_SECRET=your_key_secret
dotnet run --project src/server/LunoMcpServer/LunoMcpServer.csproj
```

Example (Windows PowerShell):
```powershell
$env:LUNO_API_KEY_ID="your_key_id"
$env:LUNO_API_KEY_SECRET="your_key_secret"
dotnet run --project src/server/LunoMcpServer/LunoMcpServer.csproj
```

### Audit Logging
Audit logs are written to a file by default. You can configure the log file path and retention in the `FileAuditLogger` class if needed.

## Using with MCP Inspector
You can use [@modelcontextprotocol/inspector](https://github.com/modelcontextprotocol/inspector) to test and debug your MCP server.

Example VS Code launch configuration:
```jsonc
{
    "name": "Inspect LunoMcpServer",
    "program": "@modelcontextprotocol/inspector",
    "args": ["dotnet run --project src/server/LunoMcpServer/LunoMcpServer.csproj"],
    "request": "launch",
    "runtimeExecutable": "npx",
    "type": "node"
}

```
## References
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Luno API Docs](https://www.luno.com/en/developers/api)