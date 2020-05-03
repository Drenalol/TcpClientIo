using System;
using System.Text;
using Drenalol.Extensions;
using Drenalol.Models;

namespace Drenalol.Base
{
   
    public class TcpPackage
    {
        public uint PackageId { get; }
        private readonly byte[] _packageId;
        public uint PackageSize { get; }
        private readonly byte[] _packageSize;
        public object PackageBody { get; }
        private readonly byte[] _packageBody;

        private TcpPackage(uint packageId)
        {
            PackageId = packageId;
            _packageId = BitConverter.GetBytes(PackageId);
        }

        public TcpPackage(uint packageId, string stringData) : this(packageId)
        {
            PackageSize = (uint) stringData.Length;
            EnsurePackageIsNotEmpty();
            _packageSize = BitConverter.GetBytes(PackageSize);
            PackageBody = stringData;
            _packageBody = Encoding.ASCII.GetBytes(stringData);
        }

        public TcpPackage(uint packageId, byte[] bytesData) : this(packageId)
        {
            PackageSize = (uint) bytesData.Length;
            EnsurePackageIsNotEmpty();
            _packageSize = BitConverter.GetBytes(PackageSize);
            PackageBody = bytesData;
            _packageBody = bytesData;
        }

        public TcpPackage(uint packageId, ITcpDataModel tcpDataModel) : this(packageId)
        {
            var bytesData = tcpDataModel.GetBytes();
            PackageSize = (uint) bytesData.Length;
            EnsurePackageIsNotEmpty();
            _packageSize = BitConverter.GetBytes(PackageSize);
            PackageBody = tcpDataModel;
            _packageBody = bytesData;
        }

        private void EnsurePackageIsNotEmpty()
        {
            if (PackageSize == 0)
                throw new AggregateException("Package is empty");
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