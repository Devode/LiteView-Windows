using LiteView.Contracts;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiteView.Services
{
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

        /// <summary>
        /// 从服务器请求获取字符串数据
        /// </summary>
        /// <param name="url">请求地址</param>
        /// <param name="cancellationToken">取消令牌（用于页面关闭时取消请求）</param>
        /// <returns>成功返回字符串，失败抛出明确异常</returns>
        /// <exception cref="HttpRequestException">网络错误或状态码异常</exception>
        public async Task<string?> GetStringAsync(string url, CancellationToken cancellationToken = default)
        {
            return await _httpClient.GetStringAsync(url, cancellationToken);
        }

        /// <summary>
        /// 从 Supabase 数据库请求获取字符串数据
        /// </summary>
        /// <param name="endpoint">具体的资源路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>成功返回字符串，失败抛出明确异常</returns>
        public async Task<string?> GetSupabaseStringAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var fullUrl = new Uri(new Uri(_supabaseBaseUrl), endpoint).ToString();

            Debug.WriteLine(fullUrl);

            using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
            request.Headers.Add("apikey", _supabasekey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        public async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken = default)
        {
            var json = await _httpClient.GetStringAsync(url, cancellationToken);


            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

        public async Task<T> GetSupabaseDataAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            var fullUrl = new Uri(new Uri(_supabaseBaseUrl), endpoint).ToString();

            Debug.WriteLine(fullUrl);

            using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
            request.Headers.Add("apikey", _supabasekey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

    }
}
