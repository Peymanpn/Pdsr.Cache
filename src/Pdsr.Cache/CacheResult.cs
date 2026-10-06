namespace Pdsr.Cache;

/// <summary>
/// The outcome of a single cache read: either a hit carrying the cached value, or a miss.
/// Lets callers tell a cached <c>false</c>/<c>0</c>/<c>null</c> apart from "not in the cache"
/// without a separate existence check that can race with expiry.
/// </summary>
/// <typeparam name="T">Type of the cached value</typeparam>
public readonly struct CacheResult<T> : IEquatable<CacheResult<T>>
{
    private CacheResult(bool hasValue, T? value)
    {
        HasValue = hasValue;
        Value = value;
    }

    /// <summary>A cache miss.</summary>
    public static CacheResult<T> Miss => default;

    /// <summary>A cache hit holding <paramref name="value"/>.</summary>
    public static CacheResult<T> Hit(T? value) => new(true, value);

    /// <summary>True when the key was present in the cache.</summary>
    public bool HasValue { get; }

    /// <summary>The cached value; <c>default</c> on a miss.</summary>
    public T? Value { get; }

    /// <summary>Returns the cached value, or <paramref name="fallback"/> on a miss.</summary>
    public T? GetValueOrDefault(T? fallback = default) => HasValue ? Value : fallback;

    /// <summary>Deconstructs into (hasValue, value).</summary>
    public void Deconstruct(out bool hasValue, out T? value)
    {
        hasValue = HasValue;
        value = Value;
    }

    /// <inheritdoc/>
    public bool Equals(CacheResult<T> other)
        => HasValue == other.HasValue && EqualityComparer<T?>.Default.Equals(Value, other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CacheResult<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HasValue ? (Value is null ? 1 : Value.GetHashCode() * 31 + 1) : 0;

    /// <inheritdoc/>
    public override string ToString() => HasValue ? $"Hit({Value})" : "Miss";

    /// <summary>Equality operator.</summary>
    public static bool operator ==(CacheResult<T> left, CacheResult<T> right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(CacheResult<T> left, CacheResult<T> right) => !left.Equals(right);
}
