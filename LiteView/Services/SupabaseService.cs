using LiteView.Contracts;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiteView.Services
{
    public class SupabaseService : ISupabaseService
    {
        private readonly Supabase.Client _client;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private Task? _initTask;

        public SupabaseService(Supabase.Client client) => _client = client;

        private async Task EnsureInitializedAsync()
        {
            if (_initTask is not null) { await _initTask; return; }

            await _initLock.WaitAsync();
            try { _initTask ??= _client.InitializeAsync(); }
            finally { _initLock.Release(); }

            await _initTask;
        }

        public async Task<IReadOnlyList<T>> GetAllAsync<T>() where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            var response = await _client.From<T>().Get();
            return response.Models;
        }

        public async Task<IReadOnlyList<T>> QueryAsync<T>(
            Action<IPostgrestTable<T>>? configure = null) where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            var table = _client.From<T>();
            configure?.Invoke(table);           // 就地添加 Where / Order / Range
            var response = await table.Get();
            return response.Models;
        }

        public async Task<T?> GetSingleAsync<T>(
            Action<IPostgrestTable<T>> configure) where T : BaseModel, new()
        {
            await EnsureInitializedAsync();
            var table = _client.From<T>();
            configure(table);

            var response = await table.Get();

            if (response.Models.Count == 0)
                return null;

            return response.Models.First();
        }
    }
}
