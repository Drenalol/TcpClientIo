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
        Length,

        /// <summary>
        /// 
        /// </summary>
        Body,

        /// <summary>
        /// 
        /// </summary>
        [Obsolete("Will be refactored in the future", true)]
        Compose
    }
}