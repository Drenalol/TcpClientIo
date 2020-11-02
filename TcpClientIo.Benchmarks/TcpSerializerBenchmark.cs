using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Serialization;
using Drenalol.TcpClientIo.Stuff;

namespace TcpClientIo.Benchmarks
{
    [SimpleJob(RuntimeMoniker.NetCoreApp31, targetCount: 100)]
    [MemoryDiagnoser]
    [IterationsColumn]
    public class TcpSerializerBenchmark
    {
        private TcpSerializer<long, Mock, Mock> _serializer;
        private Mock _mock;
        private ArrayPool<byte> _arrayPool;
        private SerializedRequest _request;
        private Func<PipeReader> _reader;

        [GlobalSetup]
        public void Ctor()
        {
            _arrayPool = ArrayPool<byte>.Create();
            _serializer = new TcpSerializer<long, Mock, Mock>(new List<TcpConverter> {new TcpUtf8StringConverter()},  l => _arrayPool.Rent(l));
            _mock = new Mock
            {
                Id = 1337,
                Email = "amavin2@etsy.com",
                FirstName = "Adelina",
                LastName = "Mavin",
                Gender = "Female",
                IpAddress = "42.241.120.161",
                Data = "amavin2@etsy.com: Adelina Mavin (Female) from 42.241.120.161"
            };
            _reader = () => PipeReader.Create(new MemoryStream(_request.Request.ToArray()));
        }

        [Benchmark]
        public SerializedRequest Serialize()
        {
            _request = _serializer.Serialize(_mock);
            _arrayPool.Return(_request.RentedArray);
            return _request;
        }

        [Benchmark]
        public Task<(long, Mock)> SerializeDeserialize()
        {
            _request = _serializer.Serialize(_mock);
            _arrayPool.Return(_request.RentedArray);
            return _serializer.DeserializeAsync(_reader(), CancellationToken.None);
        }
    }
}