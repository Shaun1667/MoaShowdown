using System.Linq;
using Moa.Showdown.Core;
using NUnit.Framework;

namespace Moa.Showdown.Tests
{
    public class CoreAssemblyTests
    {
        [Test]
        public void Core_DoesNotReferenceUnityEngine()
        {
            var refs = typeof(CoreAssembly).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n.StartsWith("UnityEngine") || n.StartsWith("UnityEditor"))
                .ToArray();
            Assert.That(refs, Is.Empty, "규칙 코드(Core)가 엔진을 참조함: " + string.Join(", ", refs));
        }
    }
}
