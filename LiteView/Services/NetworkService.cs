using LiteView.Contracts;
using LiteView.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace LiteView.Services
{
    /// <summary>
    /// HTTP client wrapper that handles both arbitrary URLs and Supabase REST endpoints.
    /// Supabase requests automatically include the apikey header from configuration.
    /// </summary>
    public class NetworkService : INetworkService
    {
        private readonly HttpClient _httpClient;
        private readonly string _supabasekey;
        private readonly string _supabaseBaseUrl;

        public NetworkService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;

            _supabasekey = configuration["Supabase:ApiKey"] ?? "";
            _supabaseBaseUrl = configuration["Supabase:BaseUrl"] ?? "";
        }

        /// <inheritdoc/>
        public async Task<string?> GetStringAsync(string url, CancellationToken cancellationToken = default)
        {
            return await _httpClient.GetStringAsync(url, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<string?> GetSupabaseStringAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var fullUrl = new Uri(new Uri(_supabaseBaseUrl), endpoint).ToString();

            using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
            request.Headers.Add("apikey", _supabasekey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<T> GetAsync<T>(string url, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)
        {
            var json = await _httpClient.GetStringAsync(url, cancellationToken);

            return JsonSerializer.Deserialize<T>(json, jsonTypeInfo)!;
        }

        /// <inheritdoc/>
        public async Task<T> GetSupabaseDataAsync<T>(string endpoint, JsonTypeInfo<T> jsonTypeInfo, CancellationToken cancellationToken = default)
        {
            var fullUrl = new Uri(new Uri(_supabaseBaseUrl), endpoint).ToString();

            using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
            request.Headers.Add("apikey", _supabasekey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(json, jsonTypeInfo)!;
        }
    }
}
