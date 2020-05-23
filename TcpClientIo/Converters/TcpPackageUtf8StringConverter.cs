using System.Text;
using Drenalol.Base;

namespace Drenalol.Converters
{
    public class TcpPackageUtf8StringConverter : TcpPackageConverter<string>
    {
        public override byte[] Convert(string input) => Encoding.UTF8.GetBytes(input);
        public override string ConvertBack(byte[] input) => Encoding.UTF8.GetString(input);
    }
}