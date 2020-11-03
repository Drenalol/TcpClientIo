using System;
using System.Buffers;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Stuff;

namespace TcpClientIo.Benchmarks
{
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [MemoryDiagnoser]
    [IterationsColumn]
    public class TcpSerializerBenchmark
    {
        private TcpSerializer<long, Mock, Mock> _serializer;
        private ArrayPool<byte> _arrayPool;
        private SerializedRequest _request;

        [GlobalSetup]
        public void Ctor()
        {
            _arrayPool = ArrayPool<byte>.Create();
            _serializer = new TcpSerializer<long, Mock, Mock>(new List<TcpConverter> {new TcpUtf8StringConverter()}, l => _arrayPool.Rent(l));
        }

        [Benchmark]
        public SerializedRequest Serialize()
        {
            _request = _serializer.Serialize(new Mock
            {
                Id = 1337,
                Email = "amavin2@etsy.com",
                FirstName = "Adelina",
                LastName = "Mavin",
                Gender = "Female",
                IpAddress = "42.241.120.161",
                Data = "amavin2@etsy.com: Adelina Mavin (Female) from 42.241.120.161"
            });
            _arrayPool.Return(_request.RentedArray);
            return _request;
        }

        [Benchmark]
        public (long, Mock) Deserialize()
        {
            return _serializer.Deserialize(new ReadOnlySequence<byte>(Convert.FromBase64String("OQUAAAAAAAA8AAAAQWRlbGluYQAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
                                                                                               "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAE1hdmluAAAAAAAAAAAAA" +
                                                                                               "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAYW1hdmluMkB" +
                                                                                               "ldHN5LmNvbQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABGZ" +
                                                                                               "W1hbGUAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
                                                                                               "AAAAAADQyLjI0MS4xMjAuMTYxAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
                                                                                               "AAAAAAAAAAAAAAAYW1hdmluMkBldHN5LmNvbTogQWRlbGluYSBNYXZpbiA" +
                                                                                               "oRmVtYWxlKSBmcm9tIDQyLjI0MS4xMjAuMTYx")));
        }
    }
}