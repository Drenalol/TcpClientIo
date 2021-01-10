using Drenalol.TcpClientIo.Attributes;
using Newtonsoft.Json;

namespace Drenalol.TcpClientIo.Stuff
{
    public class GenericMock<T>
    {
        [TcpData(0, 8, TcpDataType.Id)]
        [JsonIgnore]
        public long Id { get; set; }

        [TcpData(8, 4)]
        public int Size { get; set; }

        [TcpData(12, 58)] public string FirstName { get; set; }

        [TcpData(70, 50)] public string LastName { get; set; }

        [TcpData(120, 50)] public string Email { get; set; }

        [TcpData(170, 50)] public string Gender { get; set; }

        [TcpData(220, 50)] public string IpAddress { get; set; }

        [TcpData(270, TcpDataType = TcpDataType.Compose)]
        public T Data { get; set; }

        public override string ToString() => JsonExt.Serialize(this);

        public static GenericMock<MockOnlyData> Default(long id = 1337) => new GenericMock<MockOnlyData>
        {
            Id = id,
            Email = "amavin2@etsy.com",
            FirstName = "Adelina",
            LastName = "Mavin",
            Gender = "Female",
            IpAddress = "42.241.120.161",
            Data = new MockOnlyData
            {
                Test = 5555,
                Long = 12312312
            }
        };
    }
}