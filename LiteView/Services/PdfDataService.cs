using LiteView.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LiteView.Services
{
    public class PdfListUpdatedEventArgs : EventArgs
    {
        public ObservableCollection<PdfItem> PdfList { get; }
        public PdfListUpdatedEventArgs(ObservableCollection<PdfItem> list) => PdfList = list;
    }

    public class PdfDataService : LiteView.Contracts.IPdfDataService
    {
        /// <summary>
        /// 全局共享的数据列表
        /// </summary>
        public ObservableCollection<PdfItem> PdfList { get; } = new();

        IReadOnlyList<PdfItem> LiteView.Contracts.IPdfDataService.PdfList => PdfList;

        public event EventHandler<PdfListUpdatedEventArgs> PdfListUpdated;

        public bool IsLoading { get; private set; }

        /// <summary>
        /// 列表变更通知
        /// </summary>
        public void NotifyListChanged()
        {
            PdfListUpdated?.Invoke(this, new PdfListUpdatedEventArgs(PdfList));
        }

        /// <summary>
        /// 添加 PdfItem
        /// </summary>
        /// <param name="pdfItem"></param>
        public void AddPdf(PdfItem pdfItem)
        {
            PdfList.Add(pdfItem);
            NotifyListChanged();
        }

        /// <summary>
        /// 添加多个 PdfItem (列表形式)
        /// </summary>
        /// <param name="pdfs">PdfItem 列表</param>
        public void AddPdfs(List<PdfItem> pdfs)
        {
            foreach (var pdf in pdfs)
            {
                PdfList.Add(pdf);
            }

            NotifyListChanged();
        }

        public void RemovePdf(PdfItem pdfItem)
        {
            PdfList.Remove(pdfItem);

            NotifyListChanged();
        }

        /// <summary>
        /// 异步加载 PDF 数据
        /// </summary>
        /// <param name="dataFilePath">数据文件路径</param>
        /// <returns></returns>
        public async Task LoadPdfDataAsync(string dataFilePath)
        {
            if (IsLoading) return;  // 防止重复加载

            IsLoading = true;
            NotifyListChanged();    // 可用于通知 UI 显示 Loading 动画

            // 加载数据
            var items = await Task.Run(() => LoadDataFileAsync(dataFilePath));

            if (items == null) return;
            
            PdfList.Clear();
            foreach (var item in items) PdfList.Add(item);

            IsLoading = false;
            NotifyListChanged();
        }

        /// <summary>
        /// 异步保存 PDF 数据
        /// </summary>
        /// <param name="dataFilePath">数据文件路径</param>
        /// <returns></returns>
        public async Task SavePdfDataAsync(string dataFilePath)
        {
            if (PdfList == null) return;

            var root = new PdfDataRoot { PdfItems = PdfList.ToList() };
            string json = JsonSerializer.Serialize(root, AppJsonContext.Default.PdfDataRoot);

            // 写入文件
            await File.WriteAllTextAsync(dataFilePath, json);
        }


        /// <summary>
        /// 加载存储的数据
        /// </summary>
        /// <param name="dataFilePath">数据文件路径</param>
        /// <returns>若加载成功，返回 PdfItem 列表，否则为 null。</returns>
        private async Task<List<PdfItem>?> LoadDataFileAsync(string dataFilePath)
        {
            try
            {
                // 检查路径是否有效
                if (!File.Exists(dataFilePath)) return null;

                // 读取并解析数据
                string data = await File.ReadAllTextAsync(dataFilePath);
                var root = JsonSerializer.Deserialize(data, AppJsonContext.Default.PdfDataRoot);

                // 返回数据
                return root?.PdfItems;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"读取 PDF 数据文件失败: {ex.Message}");
                return null;
            }
        }
    }
}
