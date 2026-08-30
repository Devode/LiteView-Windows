using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace LiteView.Helpers
{
    public static class ResourceHelper
    {
        private static readonly ResourceLoader _resourceLoader = new();

        public static string GetLocalizedString(string resourceKey, params object[] args)
        {
            string template = _resourceLoader.GetString(resourceKey);
            return string.Format(template, args);
        }
    }
}
