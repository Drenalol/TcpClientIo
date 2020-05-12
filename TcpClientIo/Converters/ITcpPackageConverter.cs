namespace Drenalol.Converters
{
    public interface ITcpPackageConverter
    {
        byte[] Convert(object input);
        object ConvertBack(byte[] input);
    }
}