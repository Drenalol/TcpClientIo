using System;
using TcpClientDuplex.Extensions;

namespace TcpClientDuplex.Models
{
    public class Mock
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
    }
}