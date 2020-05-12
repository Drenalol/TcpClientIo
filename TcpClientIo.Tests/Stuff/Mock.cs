using System;
using System.Text;
using Drenalol.Attributes;

namespace Drenalol.Stuff
{
    public class Mock
    {
        [TcpPackageData(0, 16, AttributeData = TcpPackageDataType.Key)]
        public Guid Id { get; set; }

        [TcpPackageData(16, 4, AttributeData = TcpPackageDataType.BodyLength)]
        public int Size { get; set; }

        [TcpPackageData(20, 50)] public string FirstName { get; set; }
        [TcpPackageData(70, 50)] public string LastName { get; set; }
        [TcpPackageData(120, 50)] public string Email { get; set; }
        [TcpPackageData(170, 50)] public string Gender { get; set; }
        [TcpPackageData(220, 50)] public string IpAddress { get; set; }
        public string Data { get; set; }

        [TcpPackageData(270, 0, AttributeData = TcpPackageDataType.Body)]
        public byte[] Body { get; set; }

        public void SetId(bool empty = false) => Id = empty ? Guid.Empty : Guid.NewGuid();

        public Mock Build()
        {
            Body = Encoding.UTF8.GetBytes(Data);
            Size = Body.Length;
            return this;
        }
        public override string ToString() => JsonExt.Serialize(this);
    }
}