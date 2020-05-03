using System;
using System.Text;
using Drenalol.Extensions;

namespace Drenalol.Models
{
    public class Mock : ITcpDataModel
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public string IpAddress { get; set; }
        public string Hash { get; set; }
        public string Data { get; set; }
        
        public override string ToString() => JsonExt.Serialize(this);

        public byte[] GetBytes() => Encoding.ASCII.GetBytes(ToString());
    }
}