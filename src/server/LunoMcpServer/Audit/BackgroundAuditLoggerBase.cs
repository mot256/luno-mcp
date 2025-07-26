using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LunoMcpServer.Audit;

public record AuditLogEntry(string Timestamp, string Action, string User, string Details);

public abstract class BackgroundAuditLoggerBase : BackgroundService
{
    private readonly Channel<AuditLogEntry> _channel;
    private readonly int _batchSize;
    private readonly ILogger? _logger;

    protected BackgroundAuditLoggerBase(int maxQueueSize = 1000, int batchSize = 32, ILogger? logger = null)
    {
        _batchSize = batchSize;
        _logger = logger;
        _channel = Channel.CreateBounded<AuditLogEntry>(new BoundedChannelOptions(maxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task EnqueueAsync(AuditLogEntry entry)
    {
        await _channel.Writer.WriteAsync(entry);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger?.LogInformation("Starting background audit logger service");
        var batch = new List<AuditLogEntry>(_batchSize);
        try
        {
            while (true)
            {
                batch.Clear();
                // Try to read as long as there are items or the channel is not completed
                while (batch.Count < _batchSize && await _channel.Reader.WaitToReadAsync(CancellationToken.None))
                {
                    while (batch.Count < _batchSize && _channel.Reader.TryRead(out var entry))
                    {
                        batch.Add(entry);
                    }
                }
                if (batch.Count > 0)
                {
                    try
                    {
                        await PersistBatchAsync(batch, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Audit logger failed to persist batch");
                    }
                }
                // If channel is completed and empty, break
                if (_channel.Reader.Completion.IsCompleted)
                    break;
            }

            _logger?.LogInformation("Background audit logger service stopped gracefully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Audit logger background service failed");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.Complete();
        await _channel.Reader.Completion;
        await base.StopAsync(cancellationToken);
    }

    protected abstract Task PersistBatchAsync(IReadOnlyList<AuditLogEntry> batch, CancellationToken cancellationToken);
}
