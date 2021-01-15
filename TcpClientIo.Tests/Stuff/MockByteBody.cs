using Drenalol.TcpClientIo.Attributes;

namespace Drenalol.TcpClientIo.Stuff
{
    public struct MockByteBody
    {
        [TcpData(0, 4, TcpDataType = TcpDataType.Id)]
        public int Id { get; set; }
        
        [TcpData(4, 4, TcpDataType = TcpDataType.Length)]
        public int Length { get; set; }
        
        [TcpData(8, 1)]
        public byte TestByte { get; set; }
        
        [TcpData(9, 2)]
        public byte[] TestByteArray { get; set; }
        
        [TcpData(11, TcpDataType = TcpDataType.Body)]
        public string Body { get; set; }
    }
}