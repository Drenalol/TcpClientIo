using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Drenalol.TcpClientIo.Serialization.Pipelines
{
    internal class PipeWriterExecutor
    {
        private readonly PipeWriter _pipeWriter;

        public PipeWriterExecutor(PipeWriter pipeWriter) => _pipeWriter = pipeWriter;

        public virtual async ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) => await _pipeWriter.WriteAsync(source, cancellationToken);
    }
}