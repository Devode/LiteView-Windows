using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Core;
using Windows.Storage;

namespace LiteView.Helpers
{
    public static class AppSettingsHelper
    {
        private static ApplicationDataContainer _localSettings;

        static AppSettingsHelper()
        {
            _localSettings = ApplicationDataManager
                .CreateForPackageFamily(Package.Current.Id.FamilyName)
                .LocalSettings;
        }

        public static bool IsFirstRun
        {
            get
            {
                if (_localSettings.Values.TryGetValue("HasSeenTutorial", out var value))
                {
                    return (bool)value == false;
                }
                return true;
            }
        }

        public static string AppThemeTag
        {
            get
            {
                if (_localSettings.Values.TryGetValue("AppTheme", out var value))
                {
                    return (string)value;
                }
                return "Default";
            }
        }

        public static void MarkTutorialAsSeen()
        {
            _localSettings.Values["HasSeenTutorial"] = true;
        }

        public static void SetValue(string key, object value)
        {
            _localSettings.Values[key] = value;
        }

        public static object? GetValue(string key)
        {
            if (_localSettings.Values.TryGetValue(key, out var data))
            {
                return data;
            }
            return null;
        }
    }
}
