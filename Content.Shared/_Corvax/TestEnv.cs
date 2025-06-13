using System.Reflection;
namespace Content.Shared._Corvax;

public static class TestEnv
{
    public static bool IsUnitTest { get; } =
#if UNIT_TESTS
        true;
#else
        (Assembly.GetEntryAssembly()?.GetName().Name?.Contains("testhost") ?? false)
        || (Assembly.GetEntryAssembly()?.GetName().Name?.Contains("vstest")  ?? false);
#
endif
}
