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
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using LiteView.Pages;
using Microsoft.Windows.AppLifecycle;
using System.Linq;
using LiteView.Models;

namespace LiteView
{
    /// <summary>
    /// Application entry point. Builds the DI host, registers all services and ViewModels,
    /// loads configuration, restores persisted theme, and activates the main window.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        /// <summary>
        /// Singleton reference to the main window, accessible from Pages/Controls via service locator.
        ///
        /// NOTE: There is also MainWindow.current — a static field inside MainWindow itself.
        /// This App.MainWindowInstance is set once during startup (before window.Activate()),
        /// while MainWindow.current is set inside MainWindow.Loaded. Both exist to support
        /// different access patterns; if one is ever null, the other should be tried.
        /// </summary>
        public static MainWindow MainWindowInstance { get; private set; }

        /// <summary>The DI host. Exposed as a static service locator for code-behind that cannot use constructor injection.</summary>
        public static IHost? Host { get; private set; }

        public const int VERSION_CODE = 0;

        /// <summary>Path to the app's local data folder (roaming-safe).</summary>
        public string LocalFolderPath;

        /// <summary>Full path to the persisted PDF list JSON file.</summary>
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

        /// <summary>
        /// Synchronous initialization: build the DI host, load PDF data in the background,
        /// restore the persisted theme, and activate the main window.
        /// Kept synchronous to avoid async void deadlocks during startup.
        /// </summary>
        private void Init()
        {
            AppActivationArguments appActivationArguments =
                AppInstance.GetCurrent().GetActivatedEventArgs();

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

                    // Services
                    services.AddSingleton<IPdfDataService, PdfDataService>();
                    services.AddSingleton<INetworkService, NetworkService>();
                    services.AddSingleton<IUpdateService, UpdateService>();
                    services.AddSingleton<IMessageDialogService, MessageDialogService>();
                    services.AddSingleton<INavigationService, NavigationService>();
                    services.AddSingleton<IFilePickerService, FilePickerService>();

                    // ViewModels
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<PdfListViewModel>();
                    services.AddTransient<SettingsViewModel>();

                    // Pages
                    services.AddTransient<PdfListPage>();
                    services.AddTransient<SettingsPage>();

                    services.AddSingleton<MainWindow>();
                })
                .Build();

            Host.Start();

            var pdfService = Host.Services.GetRequiredService<IPdfDataService>();

            LocalFolderPath = ApplicationData.Current.LocalFolder.Path;
            PdfDataFilePath = System.IO.Path.Combine(LocalFolderPath, "pdf_list_data.json");

            // Fire-and-forget: LoadPdfDataAsync reads the local JSON file and populates pdfService.PdfList.
            // The window activates below and may render the PDF list before this completes.
            // PdfDataService.IsLoading prevents double-load; PdfListViewModel.EmptyVisibility
            // handles the transient empty state. If the JSON is malformed, this is an
            // unobserved Task exception (same limitation as OnPdfPathChanged).
            _ = pdfService.LoadPdfDataAsync(PdfDataFilePath);

            var localSettings = ApplicationData.Current.LocalSettings;
            ElementTheme themeToApply = ElementTheme.Default;

            if (localSettings.Values.ContainsKey("AppTheme"))
            {
                var savedTheme = localSettings.Values["AppTheme"].ToString();
                Enum.TryParse(savedTheme, out themeToApply);
            }

            _window = Host.Services.GetRequiredService<MainWindow>();

            MainWindowInstance = (MainWindow)_window;

            ThemeHelper.RootTheme = themeToApply;

            _window.Activate();

            var navFrame = (_window as MainWindow)?.GetNavFrame();

            if (appActivationArguments.Kind == ExtendedActivationKind.File)
            {
                var fileActivatedEventArgs = appActivationArguments.Data as IFileActivatedEventArgs;

                if (fileActivatedEventArgs?.Files.FirstOrDefault() is IStorageFile file)
                {
                    string filePath = file.Path;

                    Debug.WriteLine(filePath);
                    Debug.WriteLine(navFrame is null);
                    navFrame?.Navigate(
                        typeof(PdfViewerPage), 
                        new PdfItem{
                            FilePath = filePath,
                        });
                }
            }
        }

        public static T GetService<T>() where T : class
        {
            return Host!.Services.GetRequiredService<T>();
        }
    }
}
