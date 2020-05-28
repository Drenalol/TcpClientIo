using Microsoft.Extensions.Logging;

namespace Drenalol.Extensions
{
    internal static class ExceptionExt
    {
        public static T CaptureError<T>(this T e, ILogger logger)
        {
            logger?.LogCritical(e.ToString());
            return e;
        }
    }
}