namespace TcpClientDuplex
{
    public interface ITcpPackage<out TKey, out TValue>
    {
        TKey PackageId { get; }
        uint PackageSize { get; }
        TValue PackageBody { get; }
        TValue DeserializeBody(object rawData);
        byte[] ToArray();
    }
}