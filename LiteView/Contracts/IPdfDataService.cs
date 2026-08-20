using LiteView.Models;
using LiteView.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace LiteView.Contracts
{
    public interface IPdfDataService
    {
        // 当前 PDF 列表
        ObservableCollection<PdfItem> PdfList { get; }

        // 是否正在加载数据
        bool IsLoading { get; }

        // 列表变更事件（UI 可订阅刷新）
        event EventHandler<PdfListUpdatedEventArgs> PdfListUpdated;

        /// <summary>
        /// 添加 PdfItem
        /// </summary>
        /// <param name="pdfItem"></param>
        void AddPdf(PdfItem pdfItem);

        /// <summary>
        /// 添加多个 PdfItem (列表形式)
        /// </summary>
        /// <param name="pdfs">PdfItem 列表</param>
        void AddPdfs(List<PdfItem> pdfs);

        void RemovePdf(PdfItem pdfItem);

        /// <summary>
        /// 异步加载 PDF 数据
        /// </summary>
        /// <param name="dataFilePath">数据文件路径</param>
        /// <returns></returns>
        Task LoadPdfDataAsync(string dataFilePath);

        /// <summary>
        /// 异步保存 PDF 数据
        /// </summary>
        /// <param name="dataFilePath">数据文件路径</param>
        /// <returns></returns>
        Task SavePdfDataAsync(string dataFilePath);
    }
}
