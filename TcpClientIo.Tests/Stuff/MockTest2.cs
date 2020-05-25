using Drenalol.Attributes;

namespace Drenalol.Stuff
{
    public struct MockTest2
    {
        [TcpPackageData(0, 4, AttributeData = TcpPackageDataType.Id)]
        public int Id { get; set; }
        
        [TcpPackageData(4, 4, AttributeData = TcpPackageDataType.BodyLength)]
        public int Length { get; set; }
        
        [TcpPackageData(8, 1)]
        public byte TestByte { get; set; }
        
        [TcpPackageData(9, 2)]
        public byte[] TestByteArray { get; set; }
        
        [TcpPackageData(11, AttributeData = TcpPackageDataType.Body)]
        public string Body { get; set; }
    }
}