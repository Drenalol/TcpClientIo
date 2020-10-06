using System;

namespace TcpClientIo.TcpBatchRules
{
    /// <summary>
    /// TcpBatch creation or update rules
    /// </summary>
    public class TcpBatchRules<TResponse>
    {
        /// <summary>
        /// 
        /// </summary>
        public Func<object, TResponse, ITcpBatch<TResponse>> Create { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public Func<ITcpBatch<TResponse>, TResponse, ITcpBatch<TResponse>> Update { get; set; }

        public static TcpBatchRules<TResponse> Default => new TcpBatchRules<TResponse>
        {
            Create = (objectId, response) =>
            {
                var batch = new DefaultTcpBatch<TResponse>(objectId);
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