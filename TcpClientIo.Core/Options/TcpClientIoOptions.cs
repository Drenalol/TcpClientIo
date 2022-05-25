using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Sockets;
using Drenalol.TcpClientIo.Converters;

namespace Drenalol.TcpClientIo.Options
{
    public enum PipeExecutor
    {
        /// <summary>
        /// Default implementation
        /// </summary>
        Default,

        /// <summary>
        /// With logging
        /// </summary>
        Logging
    }

    /// <summary>
    /// Options for the TcpClientIo.Core
    /// </summary>
    public sealed class TcpClientIoOptions
    {
        /// <summary>
        /// Represents a set of options for controlling the creation of the <see cref="PipeReader"/> for the <see cref="NetworkStream"/>.
        /// </summary>
        public StreamPipeReaderOptions StreamPipeReaderOptions { get; set; } = null!;

        /// <summary>
        /// Represents a set of options for controlling the creation of the <see cref="PipeWriter"/> for the <see cref="NetworkStream"/>.
        /// </summary>
        public StreamPipeWriterOptions StreamPipeWriterOptions { get; set; } = null!;

        /// <summary>
        /// Gets or sets the amount of time a <see cref="TcpClient"/> will wait for a send operation to complete successfully.
        /// </summary>
        public int TcpClientSendTimeout { get; set; }

        /// <summary>
        /// Gets or sets the amount of time a <see cref="TcpClient"/> will wait to receive data once a read operation is initiated.
        /// </summary>
        public int TcpClientReceiveTimeout { get; set; }

        /// <summary>
        /// Gets or sets a value about sorting in reverse order for all byte arrays of primitive values.
        /// </summary>
        public bool PrimitiveValueReverse { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="TcpConverter"/> collection that will be used during serialization.
        /// </summary>
        public IList<TcpConverter> Converters { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="PipeExecutorOptions"/> that will be used in wrappers of <see cref="PipeReader"/> or <see cref="PipeWriter"/>
        /// </summary>
        public PipeExecutor PipeExecutorOptions { get; set; }

        public TcpClientIoOptions() => Converters = new List<TcpConverter>();

        /// <summary>
        /// Gets default options
        /// <returns><see cref="TcpClientIoOptions"/></returns>
        /// </summary>
        public static TcpClientIoOptions Default =>
            new TcpClientIoOptions
            {
                StreamPipeReaderOptions = new StreamPipeReaderOptions(bufferSize: 65536),
                StreamPipeWriterOptions = new StreamPipeWriterOptions(),
                TcpClientSendTimeout = 60000,
                TcpClientReceiveTimeout = 60000
            };

        /// <summary>
        /// Register converter
        /// </summary>
        /// <param name="tcpConverter"></param>
        public TcpClientIoOptions RegisterConverter(TcpConverter tcpConverter)
        {
            Converters.Add(tcpConverter);
            return this;
        }
    }
}