using LiteView.Contracts;
using LiteView.Helpers;
using LiteView.Services;
using LiteView.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.Storage;

namespace LiteView
{
    public partial class App : Application
    {
        private Window? _window;

        public static MainWindow MainWindowInstance { get; private set; }
        public static IHost? Host { get; private set; }

        public const int VERSION_CODE = 0;

        public string LocalFolderPath;
        public string PdfDataFilePath;

        public static App CurrentApp => (App)Current;

        public static IPdfDataService PdfService => Host!.Services.GetRequiredService<IPdfDataService>();

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Init();
        }

        private void Init()
        {
            Debug.WriteLine("[Init] Building host...");
            Host = Microsoft.Extensions.Hosting.Host
                .CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    var baseDir = Path.GetDirectoryName(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
                    config.AddJsonFile(Path.Combine(baseDir, "appsettings.json"), optional: false, reloadOnChange: false);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<HttpClient>(sp => new HttpClient());

                    services.AddSingleton<IPdfDataService, PdfDataService>();
                    services.AddSingleton<INetworkService, NetworkService>();
                    services.AddSingleton<IUpdateService, UpdateService>();
                    services.AddSingleton<IMessageDialogService, MessageDialogService>();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<IFilePickerService, FilePickerService>();

                    services.AddTransient<MainViewModel>();
                    services.AddTransient<PdfListViewModel>();

                    services.AddSingleton<MainWindow>();
                })
                .Build();

            Debug.WriteLine("[Init] Host built, starting...");
            Host.Start();
            Debug.WriteLine("[Init] Host started.");

            var pdfService = Host.Services.GetRequiredService<IPdfDataService>();

            LoadData(pdfService);
            Debug.WriteLine("[Init] Data loaded.");

            var localSettings = ApplicationData.Current.LocalSettings;
            ElementTheme themeToApply = ElementTheme.Default;

            if (localSettings.Values.ContainsKey("AppTheme"))
            {
                var savedTheme = localSettings.Values["AppTheme"].ToString();
                Debug.WriteLine($"[Init] Theme: {savedTheme}");
                Enum.TryParse(savedTheme, out themeToApply);
            }

            Debug.WriteLine("[Init] Resolving MainWindow...");
            _window = Host.Services.GetRequiredService<MainWindow>();
            Debug.WriteLine("[Init] MainWindow resolved.");

            ThemeHelper.RootTheme = themeToApply;

            MainWindowInstance = (MainWindow)_window;

            Debug.WriteLine("[Init] Activating window...");
            _window.Activate();
            Debug.WriteLine("[Init] Window activated.");
        }

        private void LoadData(IPdfDataService pdfService)
        {
            LocalFolderPath = ApplicationData.Current.LocalFolder.Path;
            PdfDataFilePath = System.IO.Path.Combine(LocalFolderPath, "pdf_list_data.json");

            _ = pdfService.LoadPdfDataAsync(PdfDataFilePath);
        }
    }
}
