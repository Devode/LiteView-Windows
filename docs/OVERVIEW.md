# LiteView 技术架构概要

> 本文档面向开发者，概述 LiteView 的整体架构、模块职责、核心渲染管线与数据流。更多使用与构建说明请参阅 [README.md](../README.md)。

## 1. 项目概述

LiteView 是一款轻量级的原生 **WinUI 3** PDF 阅读器，适用于 Windows 10/11，基于 .NET 8 与 PDFium 构建。支持 PDF 库管理、连续滚动阅读、自由笔批注、主题切换与全屏模式。

### 技术栈

| 组件 | 技术 |
|---|---|
| UI 框架 | WinUI 3（Windows App SDK 1.8） |
| 语言/运行时 | C#（.NET 8，`net8.0-windows10.0.19041.0`） |
| PDF 引擎 | PDFium（`bblanchon.PDFium.Win32` 原生库 + `PdfiumViewer.Updated` + 自建 P/Invoke 绑定） |
| 数据持久化 | JSON（System.Text.Json 源生成，`%LOCALAPPDATA%\LiteView\pdf_list_data.json`） |
| 绘制 | XAML Shapes（`Path` / `PolyQuadraticBezierSegment`） |
| 其他引用 | CommunityToolkit.Mvvm、Win2D、WebView2、SettingsControls（部分暂未实际使用） |
| CI/CD | GitHub Actions（x86/x64 Release MSIX + 构建溯源） |

## 2. 解决方案与构建配置

- 使用 `.slnx`（XML 解决方案格式）单项目结构，支持 `x86` / `x64` / `ARM64` 三平台。
- 项目内通过条件编译组合出多种运行形态：
  - `Packaged`（默认）/ `Unpackaged`（非打包桌面应用）。
  - Release 使用 `Package.appxmanifest`，Debug 使用 `Package.Dev.appxmanifest`（开发清单，便于调试）。
  - 发布配置：Release 开启 `PublishTrimmed` 与 `PublishReadyToRun`。

## 3. 架构分层

```
LiteView/
├── App.xaml.cs          # 应用入口：初始化服务、主题、主窗口
├── MainWindow.xaml      # 主窗口：TitleBar + NavigationView + Frame 导航
├── Controls/            # 自定义控件（PdfViewerControl、AnnotationCanvasControl、PdfListItemControl）
├── Pages/               # 应用页面（PdfListPage、PdfViewerPage、SettingsPage）
├── Services/            # 数据服务（PdfDataService）
├── Native/              # PDFium P/Invoke 绑定与渲染（PdfiumNativeMethods、PdfRenderer）
├── Models/              # 数据模型（PdfItem、Stroke、PdfPageViewModel、VersionInfo 等）
├── Helpers/             # 工具类（ThemeHelper、WindowHelper、ImageHelper、StrokeHelper、ShadowHelper）
├── Strings/             # 本地化资源（zh-CN、en-US）
├── Styles/              # 数据模板与样式
└── Properties/          # 发布配置文件（win-x64.pubxml 等）
```

### 模块职责

| 模块 | 职责 |
|---|---|
| `App` | 创建 `PdfDataService` 单例；从 `LocalSettings` 恢复主题；加载持久化数据；创建并激活 `MainWindow` |
| `MainWindow` | 自定义标题栏、NavigationView 导航；启动时调用 Supabase REST 接口做版本检测；标题栏搜索框过滤 PDF 列表 |
| `PdfListPage` | PDF 列表展示（卡片式）；`FileOpenPicker` 添加 PDF；重复文件校验；路径失效检测；导航至阅读器 |
| `PdfViewerPage` | 工具条状态管理（选择/画笔/橡皮擦）；缩放与翻页按钮；跳转页码对话框；全屏切换；笔色与笔触粗细设置 |
| `PdfViewerControl` | 核心阅读控件：滚动容器、页面虚拟化渲染、当前页定位、缩放/适应窗口、批注与滚动模式切换 |
| `AnnotationCanvasControl` | 自由笔绘制与橡皮擦；笔画平滑处理；仅内存持有笔画（见 §8） |
| `PdfDataService` | `ObservableCollection<PdfItem>` 的增删与加载/保存；列表变更事件 `PdfListUpdated` |
| `PdfRenderer` / `PdfiumNativeMethods` | PDFium 互操作：整页渲染与可视区域裁剪渲染；SafeHandle 资源管理 |
| Helpers | 主题与标题栏按钮颜色联动、全屏切换、位图组装、笔画算法（Douglas-Peucker / 尖角检测）、阴影辅助 |

## 4. 核心渲染管线

`PdfViewerControl` 采用**低模底图 + 局部高清叠加**的两层渲染策略，兼顾滚动流畅度与清晰度。

### 页面布局

- `ScrollViewer` 内嵌 `ItemsRepeater`（垂直 `StackLayout`，页间距 10px），每页由 `PdfPageViewModel` 绑定 `Border + Image`。
- 页面加载时根据 `PdfiumViewer.PdfDocument.PageSizes` 预建全部页面的元数据（尺寸、累计顶部距离 `DocumentTop`），实际图片按需渲染。

### 渲染流程

1. **整页低模渲染（底图）**：初次进入视口时，以 `BASIC_DPI = 300` 渲染整页，通过 `PdfRenderer.RenderFullPage` → `RawBitmapData` → `ImageHelper.AssembleBitmapAsync` 生成 `WriteableBitmap` 作为页面底图。
2. **滚动加载**：`ViewChanged` 触发后做 200ms 防抖（`CancellationTokenSource`），仅加载当前可视范围内的页面。
3. **局部高清渲染（叠加层）**：对可视区顶/底两个页面调用 `PdfRenderer.RenderRegion` 按 DPI `96 * zoom` 裁剪渲染可见矩形，作为 `ParticalImageTop` / `ParticalImageBottom` 通过 `Canvas.SetLeft/Top` 精确定位叠加到 `PartialRenderCanvas` 上。滚动或缩放时隐藏/重新定位。
4. **当前页跟踪**：取视口中点的 Y 坐标，通过二分查找（`FindPageByPosition`，基于 `DocumentTop`）确定 `CurrentPageIndex`，驱动页码显示与翻页。

### 缩放与定位

- 缩放（放大/缩小/适应窗口）通过 `ZoomAtViewportCenter` 计算新偏移量，保证视口中心内容不漂移。
- 翻页/跳转页码使用 `_pageToTopDistances` 映射，配合水平居中计算调用 `ScrollViewer.ChangeView`。

### 互操作层（Native）

- `PdfiumNativeMethods.cs`：使用 `LibraryImport` 声明 `FPDF_*` 系列函数；文档/页面/位图均用 `SafeHandle` 派生类封装，确保非托管资源释放。
- `PdfiumBootstrap`：线程安全的单例初始化，进程退出时自动调用 `FPDF_DestroyLibrary`。

## 5. 批注子系统

- 笔画模型为 `record Stroke(List<Vector2> Points, Color PenColor, float Thickness)`。
- 画笔：指针按下开始新笔画，移动时收集点并实时以贝塞尔路径预览；松开后经 `StrokeHelper.DouglasPeucker` 点集简化、`DetectCorners` 尖角检测，生成平滑的 `PolyQuadraticBezierSegment` 路径并固化到画布。
- 橡皮擦：命中检测（指针与笔画点距离阈值）移除对应笔画及其视觉路径。
- 工具栏（`PdfViewerPage`）以单选互斥方式在 选择 / 画笔 / 橡皮擦 间切换，并联动 `PdfViewerControl` 的批注可用性与滚动可用性。

## 6. 数据流

- 列表数据统一由 `App.PdfService`（`PdfDataService`）持有 `ObservableCollection<PdfItem>`。
- `PdfListUpdated` 事件将变更推送给 UI（列表页空态显示、主窗口搜索索引），同时触发 `SavePdfDataAsync` 将数据写入 `%LOCALAPPDATA%\LiteView\pdf_list_data.json`。
- 序列化使用 System.Text.Json **源生成上下文** `AppJsonContext`（`Models/JsonContext.cs`），避免运行时反射。
- 应用启动时（`App.Init`）先加载该 JSON，再创建主窗口。

## 7. 其他功能

- **主题切换**：`SettingsPage` 的 ComboBox 选项（Default/Light/Dark）持久化到 `LocalSettings["AppTheme"]`；`ThemeHelper` 统一应用并同步标题栏按钮颜色。
- **全屏**：`WindowHelper.SetFullScreen` 通过 `AppWindow.SetPresenter` 切换。
- **更新检测**：`MainWindow.Init` 启动时请求 `https://ratzizwtoyyhdlypecsn.supabase.co/rest/v1/versions`，解析 `VersionInfo`（取 `SoftwareId == 2`），若 `VersionsCode > App.VERSION_CODE` 弹出更新对话框并可跳转下载地址。
- **搜索**：标题栏 `AutoSuggestBox` 基于 `PdfItemNames`（文件名列表）做包含匹配过滤。
- **本地化**：`Strings/zh-CN` 与 `Strings/en-US` 的 `.resw` 资源；当前以简体中文为主，英语支持未完全覆盖。

## 8. 构建与 CI

- GitHub Actions（`.github/workflows/dotnet-desktop.yml`）在推送 `main` 时矩阵构建 x86/x64 的 Release MSIX 包，输出到 `AppPackages\`，并生成构建溯源（attestation）后上传工件。
- 本地命令行构建见 README「命令行」一节。

## 9. 已知注意事项与后续方向

- **批注未持久化**：笔画仅保存在 `AnnotationCanvasControl` 内存中，关闭页面即丢失，未写入 JSON 或 PDF。
- **遗留代码**：`AnnotationCanvasControl` 与 `PdfViewerControl` 中保留了大量注释掉的旧实现（Win2D `CanvasControl` 渲染、表达式动画同步滚动等），当前已改用 XAML Shapes + Canvas 定位方案。
- **未用引用**：`Microsoft.Graphics.Win2D`、`Microsoft.Web.WebView2` 等包已引用但暂无实际调用。
- **`App.VERSION_CODE` 硬编码为 0**，发布新版本时需手动同步，否则更新检测无法正确触发。
- **`Extensions/` 目录为空**，后续工具方法可归入该目录组织。
