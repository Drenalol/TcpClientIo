using System.Text;
using Drenalol.Attributes;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Drenalol.Stuff
{
    public struct Mock
    {
        [TcpPackageData(0, 8, AttributeData = TcpPackageDataType.Key)]
        [JsonIgnore]
        public long Id { get; set; }

        [TcpPackageData(8, 4, AttributeData = TcpPackageDataType.BodyLength)]
        public int Size { get; set; }

        [TcpPackageData(12, 58)] public string FirstName { get; set; }
        [TcpPackageData(70, 50)] public string LastName { get; set; }
        [TcpPackageData(120, 50)] public string Email { get; set; }
        [TcpPackageData(170, 50)] public string Gender { get; set; }
        [TcpPackageData(220, 50)] public string IpAddress { get; set; }
        public string Data { get; set; }

        [TcpPackageData(270, 0, AttributeData = TcpPackageDataType.Body)]
        public byte[] Body { get; set; }

        public Mock Build()
        {
            Body = Encoding.UTF8.GetBytes(Data);
            Size = Body.Length;
            return this;
        }
        public override string ToString() => JsonExt.Serialize(this);

        public static Mock Create(long id)
        {
            var rnd = TestContext.CurrentContext.Random;
            var mock = TcpClientIoTests.Mocks[rnd.Next(TcpClientIoTests.Mocks.Count)];

            return new Mock
            {
                Id = id,
                Email = mock.Email,
                FirstName = mock.FirstName,
                LastName = mock.LastName,
                Gender = mock.Gender,
                IpAddress = mock.IpAddress,
                Data = mock.Data,
                Size = mock.Size,
                Body = mock.Body
            };
        }
    }
}