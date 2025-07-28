
# Copilot Instructions for Luno MCP Server

## Project Overview
This is a C# Model Context Protocol (MCP) server that exposes Luno API endpoints as MCP tools. The server is designed for extensibility, auditability, and compatibility with the MCP Inspector and Luno API.

## Architecture & Key Patterns
- **Entry Point:** `Program.cs` sets up the DI container, MCP server, and Stdio transport. All logs go to stderr to avoid interfering with MCP stdio protocol.
- **MCP Tools:** Implemented in `LunoTools.cs` using `[McpServerTool]` attributes. Each public method wraps a Luno API endpoint and is discoverable by the MCP runtime.
- **Audit Logging:** All sensitive or state-changing actions (e.g., order placement, withdrawals) require explicit confirmation (`confirm=true`) and are logged via `IAuditLogger`/`FileAuditLogger` (see `Audit/`).
- **Configuration:** Luno API credentials are injected via environment variables (`LUNO_API_KEY_ID`, `LUNO_API_KEY_SECRET`) and accessed through `IConfiguration`.
- **Extensibility:** Add new MCP tools by extending `LunoTools` with new `[McpServerTool]` methods. Use dependency injection for all services.
- **Docker:** The project is container-ready; see `.csproj` for container properties. Use `dotnet publish` with the container profile to build images.

## Developer Workflows
- **Build:** `dotnet restore && dotnet build src/server/LunoMcpServer/LunoMcpServer.csproj`
- **Run (local):** `dotnet run --project src/server/LunoMcpServer/LunoMcpServer.csproj`
- **Run (Docker):** `dotnet publish -c Release -r linux-x64 --os linux --arch x64 /p:PublishProfile=DefaultContainer`
- **Test with Inspector:** Use [@modelcontextprotocol/inspector](https://github.com/modelcontextprotocol/inspector) to interactively test the server via stdio.
- **Configure credentials:** Set `LUNO_API_KEY_ID` and `LUNO_API_KEY_SECRET` in your environment before running.

## Project-Specific Conventions
- **Confirmation Required:** Any method that changes state (e.g., `PlaceMarketOrder`, `Send`, `CreateAccount`) must be called with `confirm=true` to execute. Otherwise, it returns a confirmation prompt.
- **Audit Logging:** All state-changing actions are logged to `audit.log` (see `FileAuditLogger`).
- **Error Handling:** API errors are surfaced as exceptions or error strings; ensure proper propagation to the MCP client.
- **No HTTP Server:** The server uses Stdio transport only (not HTTP). All communication is via stdin/stdout.
- **Environment Variables:** Prefer environment variables for secrets/configuration. Do not hardcode credentials.

## Integration Points
- **ModelContextProtocol SDK:** See https://github.com/modelcontextprotocol/csharp-sdk for SDK usage and extension.
- **Luno API:** See https://www.luno.com/en/developers/api for endpoint details.
- **Inspector:** See https://github.com/modelcontextprotocol/inspector for local testing/debugging.

## Key Files
- `Program.cs`: DI, server, and transport setup
- `LunoTools.cs`: MCP tool implementations (one method per Luno API endpoint)
- `Audit/`: Audit logging interfaces and implementations
- `LunoMcpServer.csproj`: Build, dependency, and Docker/container settings
- `README.md`: Usage, configuration, and Inspector instructions

## References
- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Luno API Docs](https://www.luno.com/en/developers/api)
- [C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk)


