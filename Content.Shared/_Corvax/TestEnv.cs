using System.Linq;

namespace Content.Shared._Corvax;

public static class TestEnv
{
    public static readonly bool IsUnitTest =
        AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.GetName().Name is "nunit.framework" or "testhost" or "NUnit3.TestAdapter")
        || System.Diagnostics.Process.GetCurrentProcess().ProcessName.Contains("testhost");
}
