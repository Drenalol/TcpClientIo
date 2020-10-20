using System;

namespace Drenalol.TcpClientIo
{
    /// <summary>
    /// TcpBatch creation or update rules
    /// </summary>
    public class TcpBatchRules<TId, TResponse>
    {
        /// <summary>
        /// Creating rule of <see cref="ITcpBatch{TId, TResponse}"/>
        /// </summary>
        public Func<TId, TResponse, ITcpBatch<TId, TResponse>> Create { get; set; }

        /// <summary>
        /// Update rule of <see cref="ITcpBatch{TId, TResponse}"/> 
        /// </summary>
        public Func<ITcpBatch<TId, TResponse>, TResponse, ITcpBatch<TId, TResponse>> Update { get; set; }

        /// <summary>
        /// Default rules for Create and Update
        /// </summary>
        public static TcpBatchRules<TId, TResponse> Default => new TcpBatchRules<TId, TResponse>
        {
            Create = (objectId, response) =>
            {
                var batch = new DefaultTcpBatch<TId, TResponse>(objectId);
                batch.Update(response);
                return batch;
            },
            Update = (batch, response) =>
            {
                batch.Update(response);
                return batch;
            }
        };
    }
}