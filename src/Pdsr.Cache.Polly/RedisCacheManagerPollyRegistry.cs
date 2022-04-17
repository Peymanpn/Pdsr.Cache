using Polly;

namespace Pdsr.Cache.Polly
{
    public class RedisCacheManagerPollyRegistry
    {
        public RedisCacheManagerPollyRegistry(
            IAsyncPolicy? defaultPolicy = null,
            IAsyncPolicy? getRetryPolicy = null,
            IAsyncPolicy? setRetryPolicy = null)
        {
            DefaultRetryPolicy = defaultPolicy;
            SetRetryPolicy = setRetryPolicy;
            GetRetryPolicy = getRetryPolicy;
        }

        public IAsyncPolicy? GetRetryPolicy { get; set; }
        public IAsyncPolicy? SetRetryPolicy { get; set; }
        public IAsyncPolicy? DefaultRetryPolicy { get; set; }

    }
}
