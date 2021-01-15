using System;

namespace Drenalol.TcpClientIo.Attributes
{
    /// <summary>
    /// 
    /// </summary>
    public enum TcpDataType
    {
        /// <summary>
        /// 
        /// </summary>
        MetaData,

        /// <summary>
        /// 
        /// </summary>
        Id,

        /// <summary>
        /// 
        /// </summary>
        [Obsolete("Use TcpDataType.Length instead", true)]
        BodyLength,

        /// <summary>
        /// 
        /// </summary>
        Length,

        /// <summary>
        /// 
        /// </summary>
        Body,

        /// <summary>
        /// 
        /// </summary>
        Compose
    }
}