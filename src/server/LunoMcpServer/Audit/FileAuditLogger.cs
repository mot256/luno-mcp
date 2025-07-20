namespace LunoMcpServer.Audit;

public class FileAuditLogger : BackgroundAuditLoggerBase, IAuditLogger
{
    private readonly string _logPath;

    public FileAuditLogger(string? logPath = null)
        : base()
    {
        _logPath = logPath ?? Path.Combine(AppContext.BaseDirectory, "audit.log");
    }

    public async Task LogAsync(string action, string user, string details)
    {
        var entry = new AuditLogEntry(DateTime.UtcNow.ToString("O"), action, user, details);
        await EnqueueAsync(entry);
    }

    protected override async Task PersistBatchAsync(IReadOnlyList<AuditLogEntry> batch, CancellationToken cancellationToken)
    {
        using var writer = new StreamWriter(_logPath, append: true);
        foreach (var entry in batch)
        {
            await writer.WriteLineAsync($"{entry.Timestamp}\t{entry.Action}\t{entry.User}\t{entry.Details}");
        }
    }
}
