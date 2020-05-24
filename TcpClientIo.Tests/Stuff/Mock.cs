using System;
using System.Collections.Generic;
using Drenalol.Attributes;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Drenalol.Stuff
{
    public struct Mock : IEqualityComparer<Mock>
    {
        [TcpPackageData(0, 8, AttributeData = TcpPackageDataType.Id)]
        [JsonIgnore]
        public long Id { get; set; }

        [TcpPackageData(8, 4, AttributeData = TcpPackageDataType.BodyLength)]
        public int Size { get; set; }

        [TcpPackageData(12, 58)]
        public string FirstName { get; set; }

        [TcpPackageData(70, 50)]
        public string LastName { get; set; }

        [TcpPackageData(120, 50)]
        public string Email { get; set; }

        [TcpPackageData(170, 50)]
        public string Gender { get; set; }

        [TcpPackageData(220, 50)]
        public string IpAddress { get; set; }

        [TcpPackageData(270, AttributeData = TcpPackageDataType.Body)]
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

        public override int GetHashCode() => HashCode.Combine(Id, FirstName, LastName, Email, Gender, IpAddress, Data);

        public bool Equals(Mock x, Mock y) => x.Equals(y);

        public int GetHashCode(Mock obj) => HashCode.Combine(obj.Id, obj.FirstName, obj.LastName, obj.Email, obj.Gender, obj.IpAddress, obj.Data);
    }
}