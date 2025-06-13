namespace Content.Shared._Corvax;

public static class TestEnv
{
    public static bool IsUnitTest { get; } =
#if UNIT_TESTS
        true;
#else
        false;
#endif
}
