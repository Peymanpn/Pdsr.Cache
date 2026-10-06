namespace Pdsr.Cache.Tests;

public class CacheResultTests
{
    [Fact]
    public void A_miss_has_no_value()
    {
        var miss = CacheResult<bool>.Miss;

        Assert.False(miss.HasValue);
        Assert.False(miss.Value);
        Assert.Equal(default(CacheResult<bool>), miss);
        Assert.Equal("Miss", miss.ToString());
    }

    [Fact]
    public void A_hit_carries_its_value_even_when_it_is_the_default()
    {
        var hit = CacheResult<bool>.Hit(false);

        Assert.True(hit.HasValue);
        Assert.False(hit.Value);
        Assert.NotEqual(CacheResult<bool>.Miss, hit);
        Assert.Equal("Hit(False)", hit.ToString());
    }

    [Fact]
    public void GetValueOrDefault_uses_the_fallback_only_on_a_miss()
    {
        Assert.Equal(5, CacheResult<int>.Miss.GetValueOrDefault(5));
        Assert.Equal(0, CacheResult<int>.Hit(0).GetValueOrDefault(5));
        Assert.Equal(0, CacheResult<int>.Miss.GetValueOrDefault());
    }

    [Fact]
    public void Deconstructs_into_presence_and_value()
    {
        var (hasValue, value) = CacheResult<string>.Hit("x");
        var (missHasValue, missValue) = CacheResult<string>.Miss;

        Assert.True(hasValue);
        Assert.Equal("x", value);
        Assert.False(missHasValue);
        Assert.Null(missValue);
    }

    [Fact]
    public void Equality_compares_presence_and_value()
    {
        Assert.True(CacheResult<string>.Hit("a") == CacheResult<string>.Hit("a"));
        Assert.True(CacheResult<string>.Hit("a") != CacheResult<string>.Hit("b"));
        Assert.True(CacheResult<string>.Hit(null) != CacheResult<string>.Miss);
        Assert.True(CacheResult<string>.Hit("a").Equals((object)CacheResult<string>.Hit("a")));
        Assert.False(CacheResult<string>.Hit("a").Equals("a"));
    }

    [Fact]
    public void Equal_results_have_equal_hash_codes()
    {
        Assert.Equal(CacheResult<string>.Hit("a").GetHashCode(), CacheResult<string>.Hit("a").GetHashCode());
        Assert.NotEqual(CacheResult<string>.Hit(null).GetHashCode(), CacheResult<string>.Miss.GetHashCode());
        Assert.Equal(0, CacheResult<string>.Miss.GetHashCode());
    }
}
