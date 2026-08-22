using LiteView.Models;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    /// <summary>
    /// Checks a remote Supabase database for a newer version of the application.
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Compare the current package version against the latest remote version.
        /// </summary>
        /// <returns>The <see cref="RemoteVersion"/> if an update is available; otherwise null.</returns>
        Task<RemoteVersion?> CheckUpdateAsync();
    }
}
