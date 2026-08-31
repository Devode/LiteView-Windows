using LiteView.Contracts;
using LiteView.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace LiteView.Services
{
    /// <summary>
    /// Compares the running package version against the latest version stored
    /// in Supabase and returns a <see cref="RemoteVersion"/> if an update is available.
    /// </summary>
    public class UpdateService : IUpdateService
    {
        private readonly INetworkService _networkService;

        public UpdateService(INetworkService networkService)
        {
            _networkService = networkService;
        }

        /// <inheritdoc/>
        public async Task<RemoteVersion?> CheckUpdateAsync()
        {
            // Fetches all versions for software_id=2. The Supabase response ordering is not
            // guaranteed — FirstOrDefault returns whichever row the API returns first.
            // If multiple version rows exist, this may not be the latest one.
            // A proper solution would add ORDER BY version DESC to the Supabase query.
            var versions = await _networkService.GetSupabaseDataAsync("versions?software_id=eq.2", AppJsonContext.Default.RemoteVersionArray);
            var remoteVersion = versions?.FirstOrDefault();

            if (remoteVersion == null) return null;

            if (!Version.TryParse(remoteVersion.Version, out Version? latestVersion)) {
                return null;
            }

            Version currentVersion = GetCurrentPackageVersion();

            // Return the remote version only if it is strictly newer
            if (latestVersion.CompareTo(currentVersion) > 0)
            {
                return remoteVersion;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Read the version from the running MSIX package manifest.
        /// </summary>
        private Version GetCurrentPackageVersion()
        {
            PackageVersion packageVersion = Package.Current.Id.Version;

            return new Version(
                packageVersion.Major,
                packageVersion.Minor,
                packageVersion.Build,
                packageVersion.Revision
            );
        }
    }
}
