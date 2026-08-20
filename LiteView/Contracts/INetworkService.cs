using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    public interface INetworkService
    {
        Task<string?> GetStringAsync(string url, CancellationToken cancellationToken = default);

        Task<string?> GetSupabaseStringAsync(string endpoint, CancellationToken cancellationToken = default);

        Task<T> GetAsync<T>(string url, CancellationToken cancellationToken = default);
        Task<T> GetSupabaseDataAsync<T>(string endpoint, CancellationToken cancellationToken = default);
    }
}
