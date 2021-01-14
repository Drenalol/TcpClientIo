using Drenalol.TcpClientIo.Attributes;
using Newtonsoft.Json;

namespace Drenalol.TcpClientIo.Stuff
{
    public class RecursiveMock<T> where T : new()
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

        public RecursiveMock()
        {
            Id = 1337;
            Email = "amavin2@etsy.com";
            FirstName = "Adelina";
            LastName = "Mavin";
            Gender = "Female";
            IpAddress = "42.241.120.161";
            Data = new T();
        }
    }
}