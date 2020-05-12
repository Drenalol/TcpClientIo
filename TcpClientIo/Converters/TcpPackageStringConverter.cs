using System.Text;

namespace Drenalol.Converters
{
    public class TcpPackageStringConverter : ITcpPackageConverter
    {
        public byte[] Convert(object input) => Encoding.UTF8.GetBytes((string) input);
        public object ConvertBack(byte[] input) => Encoding.UTF8.GetString(input);
    }
}