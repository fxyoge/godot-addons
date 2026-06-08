using System;
using System.Reflection;
using Xunit;

namespace Fxyoge.DependencyInjection.Tests;

public sealed class StartupDiscoveryTests
{
    [Fact]
    public void GetStartupTypesThrowsWhenAssemblyTypesCannotLoad()
    {
        var loaderException = new InvalidOperationException("missing dependency");
        var assembly = new ThrowingAssembly(
            new ReflectionTypeLoadException(
                Array.Empty<Type>(),
                new Exception[] { loaderException }));

        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupDiscovery.GetStartupTypes(assembly));

        Assert.Contains(assembly.FullName, ex.Message);
        Assert.Contains(loaderException.Message, ex.Message);
    }

    private sealed class ThrowingAssembly : Assembly
    {
        private readonly Exception _exception;

        public ThrowingAssembly(Exception exception)
        {
            _exception = exception;
        }

        public override string FullName => "ThrowingAssembly";

        public override Type[] GetTypes() => throw _exception;
    }
}
