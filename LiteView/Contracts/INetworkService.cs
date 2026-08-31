using System.Net.Http;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    /// <summary>
    /// HTTP abstraction for fetching raw strings and deserialized JSON from
    /// arbitrary URLs or Supabase REST endpoints.
    /// </summary>
    public interface INetworkService
    {
        /// <summary>
        /// Fetch a raw string from an absolute URL.
        /// </summary>
        /// <exception cref="HttpRequestException">Thrown on network or HTTP status errors.</exception>
        Task<string?> GetStringAsync(string url, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetch a raw string from a Supabase REST endpoint. The apikey header is added automatically.
        /// </summary>
        Task<string?> GetSupabaseStringAsync(string endpoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetch and deserialize JSON from an absolute URL into <typeparamref name="T"/>.
        /// </summary>
        Task<T> GetAsync<T>(string url, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetch and deserialize JSON from a Supabase REST endpoint into <typeparamref name="T"/>.
        /// The apikey header is added automatically.
        /// </summary>
        Task<T> GetSupabaseDataAsync<T>(string endpoint, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default);
    }
}
