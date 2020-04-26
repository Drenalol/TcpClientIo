using System;
using System.Text;
using Newtonsoft.Json;

namespace TcpClientDuplex
{
    public class TcpPackage<TKey, TValue> : ITcpPackage<TKey, TValue> where TKey : struct
    {
        public TKey PackageId { get; set; }
        private readonly byte[] _packageId;
        public uint PackageSize { get; }
        private readonly byte[] _packageSize;
        public TValue PackageBody { get; }
        private readonly byte[] _packageBody;

        public TcpPackage(TKey packageId, string rawData)
        {
            PackageId = packageId;
            PackageSize = (uint) rawData.Length;
            PackageBody = DeserializeBody(rawData);
            
            _packageId = InnerFunc(PackageId);
            _packageSize = InnerFunc(PackageSize);
            _packageBody = Encoding.ASCII.GetBytes(rawData);

            static byte[] InnerFunc(object o) => (byte[]) typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(TKey)})?.Invoke(null, new [] {o});
        }

        public TValue DeserializeBody(object rawData) => JsonConvert.DeserializeObject<TValue>(rawData.ToString()!, TcpClientDuplexExt.JsonSerializerSettings);

        public byte[] ToArray()
        {
            var data = new byte[_packageId.Length + _packageSize.Length + _packageBody.Length];
            data.Merge(0, _packageId);
            data.Merge(_packageId.Length, _packageSize);
            data.Merge(_packageId.Length + _packageSize.Length, _packageBody);
            return data;
        }
    }
}