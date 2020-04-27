using System;
using System.Text;
using TcpClientDuplex.Extensions;

namespace TcpClientDuplex.Base
{
    public class TcpPackage
    {
        public uint PackageId { get; }
        private readonly byte[] _packageId;
        public uint PackageSize { get; }
        private readonly byte[] _packageSize;
        public string PackageBody { get; }
        private readonly byte[] _packageBody;

        public TcpPackage(uint packageId, string rawData)
        {
            PackageId = packageId;
            PackageSize = (uint) rawData.Length;
            PackageBody = rawData;
            
            _packageId = BitConverter.GetBytes(PackageId);
            _packageSize = BitConverter.GetBytes(PackageSize);
            _packageBody = Encoding.ASCII.GetBytes(rawData);
        }

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