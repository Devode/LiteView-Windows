using Supabase.Postgrest.Interfaces;
using Supabase.Postgrest.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    public interface ISupabaseService
    {
        /// <summary>获取全表数据。</summary>
        Task<IReadOnlyList<T>> GetAllAsync<T>() where T : BaseModel, new();

        /// <summary>带过滤/排序/分页的查询。</summary>
        Task<IReadOnlyList<T>> QueryAsync<T>(
            Action<IPostgrestTable<T>>? configure = null) where T : BaseModel, new();

        /// <summary>查单条，0 条返回 null，多于 1 条抛异常。</summary>
        Task<T?> GetSingleAsync<T>(
            Action<IPostgrestTable<T>> configure) where T : BaseModel, new();
    }
}
