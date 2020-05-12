using System;

namespace Drenalol.Converters
{
    public class TcpPackageGuidConverter : ITcpPackageConverter
    {
        public byte[] Convert(object input) => (input is Guid guid ? guid : default).ToByteArray();

        public object ConvertBack(byte[] input)
        {
            var guid = new Guid(input);
            return guid;
        }
    }
}