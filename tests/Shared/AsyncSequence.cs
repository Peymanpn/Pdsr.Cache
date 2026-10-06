namespace Pdsr.Cache.Tests.Shared;

public sealed record Sample(int Id, string Name, bool Flag);

internal static class AsyncSequence
{
    public static async IAsyncEnumerable<T> From<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    public static KeyValuePair<string, TValue> Pair<TValue>(string key, TValue value) => new(key, value);
}
