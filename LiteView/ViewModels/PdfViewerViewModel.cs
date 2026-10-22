using CommunityToolkit.Mvvm.ComponentModel;
using LiteView.Contracts;
using LiteView.Controls;
using LiteView.Helpers;
using LiteView.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.UI;

namespace LiteView.ViewModels
{
    public partial class PdfViewerViewModel : ObservableObject
    {
        // 14.0 is the toolbar's default zoom display value (percentage-like, not DPI).
        [ObservableProperty]
        private double _buttonZoom = 14.0;

        [ObservableProperty]
        private double _strokeThickness = 1.0;

        [ObservableProperty]
        private Color _penColor = Colors.Red;

        [ObservableProperty]
        private float _strokeSimplifiedTolerance = 0.5f;

        [ObservableProperty]
        private bool _isClockOpen = false;

        [ObservableProperty]
        private int _ClockFontSize = 36;

        [ObservableProperty]
        private ToolKind _currentTool = ToolKind.Select;

        [ObservableProperty] 
        private string? _currentFilePath;

        [ObservableProperty]
        private int _currentPageIndex;

        [ObservableProperty]
        private int _pageCount;

        [ObservableProperty]
        private string _currentPageNumberTextBoxText = "";

        [ObservableProperty]
        private ObservableCollection<PageItem> _pdfItems = [];

        [ObservableProperty]
        private double _thumbnailHeight = 520;

        private readonly IPdfService _pdfService;

        public bool IsFirstRun => AppSettingsHelper.IsFirstRun;
        public void MarkTutorialSeen() => AppSettingsHelper.MarkTutorialAsSeen();

        public PdfViewerViewModel(IPdfService pdfService)
        {
            _pdfService = pdfService;
        }

        public async Task SafeInitializePdf(string pdfPath)
        {
            try
            {
                if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath))
                {
                    Debug.WriteLine($"[InitializePdf] PDF file does not exist: {pdfPath}");
                    return;
                }
                await InitializePdf(pdfPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InitializePdf] {ex.Message}");
            }
        }

        private async Task InitializePdf(string pdfPath)
        {
            await _pdfService.LoadAsync(pdfPath);

            PageCount = _pdfService.PageCount;
            if (_pageCount > 0)
            {
                CurrentPageIndex = 1;
            }

            PdfItems.Clear();
            double cumulativeTop = 0;
            for (int i = 0; i < _pageCount; i++)
            {
                var pageSize = _pdfService.PageSizes[i];
                double ratio = pageSize.Height / pageSize.Width;

                _pdfItems.Add(new PageItem
                {
                    PageIndex = i,
                    PageWidth = 160.0,
                    PageHeight = 160.0 * ratio,
                });

                cumulativeTop += pageSize.Height + 10;
            }

        }


    }
}
