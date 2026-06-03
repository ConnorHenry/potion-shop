using System;

internal static class TestAssert
{
    public static void AssertEqual<T>(string name, T expected, T actual) where T : IEquatable<T>
    {
        if (!expected.Equals(actual))
            throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }

    public static void AssertEqual(string name, float expected, float actual, float tolerance = 0.01f)
    {
        if (MathF.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }

    public static void AssertTrue(string name, bool condition)
    {
        if (!condition)
            throw new InvalidOperationException($"{name}: expected condition to be true");
    }
}
