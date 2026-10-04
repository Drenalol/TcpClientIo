using System.IO.Pipelines;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo.Serialization.Pipelines;

internal class LoggingPipeWriterExecutor(PipeWriter pipeWriter, string? type, ILogger? logger) : PipeWriterExecutor(pipeWriter)
{
    private readonly string _type = type ?? throw new ArgumentNullException(nameof(type));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger is not configured");

    public override async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        var flushResult = await base.WriteAsync(source, cancellationToken);

        _logger.LogInformation(
            "[{Type:l}] Send: Length: {Length}, IsCanceled: {IsCanceled}, IsCompleted: {IsCompleted}",
            _type,
            source.Length,
            flushResult.IsCanceled,
            flushResult.IsCompleted
        );

        return flushResult;
    }
}
