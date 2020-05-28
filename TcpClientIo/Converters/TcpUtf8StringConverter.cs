using System.Text;
using Drenalol.Abstractions;
using Drenalol.Base;

namespace Drenalol.Converters
{
    public class TcpUtf8StringConverter : TcpConverter<string>
    {
        public override byte[] Convert(string input) => Encoding.UTF8.GetBytes(input);
        public override string ConvertBack(byte[] input) => Encoding.UTF8.GetString(input);
    }
}