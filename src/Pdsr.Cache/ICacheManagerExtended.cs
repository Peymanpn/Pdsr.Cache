namespace Pdsr.Cache;

public interface ICacheManagerExtended : ICacheManager
{

    IEnumerable<string> GetIEnum(string key, Func<IEnumerable<string>> acquire, int? cacheTime = null);
    IEnumerable<int> GetIEnum(string key, Func<IEnumerable<int>> acquire, int? cacheTime = null);
    IEnumerable<long> GetIEnum(string key, Func<IEnumerable<long>> acquire, int? cacheTime = null);
    IEnumerable<double> GetIEnum(string key, Func<IEnumerable<double>> acquire, int? cacheTime = null);

    Task<IEnumerable<string>> GetIEnumAsync(string key, Func<Task<IEnumerable<string>>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<int>> GetIEnumAsync(string key, Func<Task<IEnumerable<int>>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<long>> GetIEnumAsync(string key, Func<Task<IEnumerable<long>>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<double>> GetIEnumAsync(string key, Func<Task<IEnumerable<double>>> acquire, int? cacheTime = null, CancellationToken cancellationToken = default);

    void SetIEnum(string key, IEnumerable<string> data, int? cacheTime = null);
    void SetIEnum(string key, IEnumerable<int> data, int? cacheTime = null);
    void SetIEnum(string key, IEnumerable<long> data, int? cacheTime = null);
    void SetIEnum(string key, IEnumerable<double> data, int? cacheTime = null);

    Task SetIEnumAsync(string key, IEnumerable<string> data, int? cacheTime = null, CancellationToken cancellationToken = default);
    Task SetIEnumAsync(string key, IEnumerable<int> data, int? cacheTime = null, CancellationToken cancellationToken = default);
    Task SetIEnumAsync(string key, IEnumerable<long> data, int? cacheTime = null, CancellationToken cancellationToken = default);
    Task SetIEnumAsync(string key, IEnumerable<double> data, int? cacheTime = null, CancellationToken cancellationToken = default);


}
