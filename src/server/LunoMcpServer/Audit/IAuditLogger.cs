namespace LunoMcpServer.Audit;

public interface IAuditLogger
{
    Task LogAsync(string action, string user, string details);
}
