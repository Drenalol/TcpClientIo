using System.IO.Pipelines;
using Microsoft.Extensions.Logging;

namespace Drenalol.TcpClientIo.Serialization.Pipelines;

internal class LoggingPipeReaderExecutor(PipeReader reader, string? type, ILogger? logger) : PipeReaderExecutor(reader)
{
    private readonly string _type = type ?? throw new ArgumentNullException(nameof(type));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger), "Logger is not configured");

    public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var readResult = await base.ReadAsync(cancellationToken);

        _logger.LogInformation(
            "[{Type:l}] Read: Length: {Length}, IsCanceled: {IsCanceled}, IsCompleted: {IsCompleted}",
            _type,
            readResult.Buffer.Length,
            readResult.IsCanceled,
            readResult.IsCompleted
        );

        return readResult;
    }
}
