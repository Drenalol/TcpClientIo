using System;

namespace Drenalol.TcpClientIo.Batches
{
    /// <summary>
    /// TcpBatch creation or update rules
    /// </summary>
    public class TcpBatchRules<TResponse>
    {
        /// <summary>
        /// Creating rule of <see cref="ITcpBatch{TResponse}"/>
        /// </summary>
        public Func<TResponse, ITcpBatch<TResponse>> Create { get; set; } = null!;

        /// <summary>
        /// Update rule of <see cref="ITcpBatch{TResponse}"/> 
        /// </summary>
        public Func<ITcpBatch<TResponse>, TResponse, ITcpBatch<TResponse>> Update { get; set; } = null!;

        /// <summary>
        /// Default rules for Create and Update
        /// </summary>
        public static TcpBatchRules<TResponse> Default => new TcpBatchRules<TResponse>
        {
            Create = response =>
            {
                var batch = new DefaultTcpBatch<TResponse> {response};
                return batch;
            },
            Update = (batch, response) =>
            {
                batch.Add(response);
                return batch;
            }
        };
    }
}