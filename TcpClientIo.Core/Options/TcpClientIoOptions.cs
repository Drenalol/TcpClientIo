using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using Drenalol.TcpClientIo.Converters;

namespace Drenalol.TcpClientIo.Options
{
    /// <summary>
    /// Options for the TcpClientIo.Core
    /// </summary>
    public sealed class TcpClientIoOptions
    {
        /// <summary>
        /// Represents a set of options for controlling the creation of the <see cref="PipeReader"/> for the <see cref="NetworkStream"/>.
        /// </summary>
        public StreamPipeReaderOptions StreamPipeReaderOptions { get; set; }

        /// <summary>
        /// Represents a set of options for controlling the creation of the <see cref="PipeWriter"/> for the <see cref="NetworkStream"/>.
        /// </summary>
        public StreamPipeWriterOptions StreamPipeWriterOptions { get; set; }

        public int TcpClientSendTimeout { get; set; }
        public int TcpClientReceiveTimeout { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="TcpConverter"/> collection that will be used during serialization.
        /// </summary>
        public IReadOnlyCollection<TcpConverter> Converters { get; set; }

        /// <summary>
        /// Gets default options
        /// <returns><see cref="TcpClientIoOptions"/></returns>
        /// </summary>
        public static TcpClientIoOptions Default => new TcpClientIoOptions
        {
            StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 65536),
            StreamPipeWriterOptions = new StreamPipeWriterOptions(),
            TcpClientSendTimeout = 60000,
            TcpClientReceiveTimeout = 60000,
            Converters = new List<TcpConverter>()
        };
    }
}