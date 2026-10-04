using System.IO.Pipelines;

namespace Drenalol.TcpClientIo.Serialization.Pipelines;

internal class PipeWriterExecutor(PipeWriter pipeWriter)
{
    private readonly PipeWriter _pipeWriter = pipeWriter ?? throw new ArgumentNullException(nameof(pipeWriter));

    public virtual async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) => await _pipeWriter.WriteAsync(source, cancellationToken);
}
