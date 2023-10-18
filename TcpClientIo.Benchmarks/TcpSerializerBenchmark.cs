using System;
using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Options;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Stuff;

namespace TcpClientIo.Benchmarks
{
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [MemoryDiagnoser]
    [IterationsColumn]
    public class TcpSerializerBenchmark
    {
        private TcpDeserializer<long, Mock> _deserializer;
        private TcpSerializer<Mock> _serializer;
        private ArrayPool<byte> _arrayPool;
        private SerializedRequest _request;

        [GlobalSetup]
        public void Ctor()
        {
            _arrayPool = ArrayPool<byte>.Shared;
            var helper = new BitConverterHelper(TcpClientIoOptions.Default.RegisterConverter(new TcpUtf8StringConverter()));
            _serializer = new TcpSerializer<Mock>(helper, l => _arrayPool.Rent(l));
            _deserializer = new TcpDeserializer<long, Mock>(helper, null!);
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
            _request.ReturnRentedArray(_arrayPool);
            return _request;
        }

        [Benchmark]
        public (long, Mock) Deserialize()
        {
            return _deserializer.Deserialize(new ReadOnlySequence<byte>(Convert.FromBase64String("OQUAAAAAAAA8AAAAQWRlbGluYQAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
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