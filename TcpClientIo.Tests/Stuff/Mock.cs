using System;
using Drenalol.Attributes;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Drenalol.Stuff
{
    public struct Mock : IEquatable<Mock>
    {
        [TcpData(0, 8, TcpDataType = TcpDataType.Id)]
        [JsonIgnore]
        public long Id { get; set; }

        [TcpData(8, 4, TcpDataType = TcpDataType.BodyLength)]
        public int Size { get; set; }

        [TcpData(12, 58)]
        public string FirstName { get; set; }

        [TcpData(70, 50)]
        public string LastName { get; set; }

        [TcpData(120, 50)]
        public string Email { get; set; }

        [TcpData(170, 50)]
        public string Gender { get; set; }

        [TcpData(220, 50)]
        public string IpAddress { get; set; }

        [TcpData(270, TcpDataType = TcpDataType.Body)]
        public string Data { get; set; }

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
                Size = mock.Size
            };
        }

        public static bool operator ==(Mock x, Mock y) => x.Equals(y);

        public static bool operator !=(Mock x, Mock y) => !x.Equals(y);

        public bool Equals(Mock other) =>
            Id == other.Id &&
            FirstName == other.FirstName.Replace("\0", string.Empty) &&
            LastName == other.LastName.Replace("\0", string.Empty) &&
            Email == other.Email.Replace("\0", string.Empty) &&
            Gender == other.Gender.Replace("\0", string.Empty) &&
            IpAddress == other.IpAddress.Replace("\0", string.Empty) &&
            Data == other.Data;

        public override bool Equals(object obj) => obj is Mock other && Equals(other);
        
#if NETSTANDARD2_1 || NETCOREAPP3_1 || NETCOREAPP3_0
        public override int GetHashCode()
        {
            return HashCode.Combine(Id, FirstName, LastName, Email, Gender, IpAddress, Data);
        }
#endif
    }
}