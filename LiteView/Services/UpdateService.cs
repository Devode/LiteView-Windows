using LiteView.Contracts;
using LiteView.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace LiteView.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly INetworkService _networkService;

        public UpdateService(INetworkService networkService)
        {
            _networkService = networkService;
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        /// <returns>若有更新，返回最新版本的 RemoteVersion，否则返回 null</returns>
        public async Task<RemoteVersion?> CheckUpdateAsync()
        {
            var versions = await _networkService.GetSupabaseDataAsync<RemoteVersion[]>("versions?software_id=eq.2");
            var remoteVersion = versions?.FirstOrDefault();

            if (remoteVersion == null) return null;

            if (!Version.TryParse(remoteVersion.Version, out Version? latestVersion)) {
                return null;
            }

            Version currentVersion = GetCurrentPackageVersion();

            if (latestVersion.CompareTo(currentVersion) > 0)
            {
                return remoteVersion;
            }
            else
            {
                return null;
            }
        }

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
