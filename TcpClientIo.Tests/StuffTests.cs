using Drenalol.Attributes;
using Drenalol.Exceptions;
using Drenalol.Helpers;
using NUnit.Framework;

namespace Drenalol
{
    public class StuffTests
    {
        class DoesNotHaveAny
        {
        }
        
        class DoesNotHaveKeyAttribute
        {
            [TcpPackageData(1, 1, AttributeData = TcpPackageDataType.Body)]
            public int Body { get; set; }
            [TcpPackageData(2, 1, AttributeData = TcpPackageDataType.BodyLength)]
            public int BodyLength { get; set; }
        }

        class DoesNotHaveBodyAttribute
        {
            [TcpPackageData(0, 1, AttributeData = TcpPackageDataType.Key)]
            public int Key { get; set; }
            [TcpPackageData(1, 2, AttributeData = TcpPackageDataType.BodyLength)]
            public int BodyLength { get; set; }
        }
        
        class DoesNotHaveBodyLengthAttribute
        {
            [TcpPackageData(0, 1, AttributeData = TcpPackageDataType.Key)]
            public int Key { get; set; }
            [TcpPackageData(1, 2, AttributeData = TcpPackageDataType.Body)]
            public int Body { get; set; }
        }

        class KeyDoesNotHaveSetter
        {
            [TcpPackageData(0, 1, AttributeData = TcpPackageDataType.Key)]
            public int Key { get; }
            [TcpPackageData(1, 2, AttributeData = TcpPackageDataType.BodyLength)]
            public int BodyLength { get; set; }
            [TcpPackageData(3, 2, AttributeData = TcpPackageDataType.Body)]
            public int Body { get; set; }
        }
        
        [Test]
        public void ReflectionErrorsTest()
        {
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<DoesNotHaveAny, DoesNotHaveAny>());
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<DoesNotHaveKeyAttribute, DoesNotHaveKeyAttribute>());
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<DoesNotHaveBodyAttribute, DoesNotHaveBodyAttribute>());
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<DoesNotHaveBodyLengthAttribute, DoesNotHaveBodyLengthAttribute>());
            Assert.Catch(typeof(TcpPackageException), () => new ReflectionHelper<KeyDoesNotHaveSetter, KeyDoesNotHaveSetter>());
        }
    }
}