using System.Collections.ObjectModel;
using TopSolidMingdao.Core;
using TopSolidMingdao.Core.Interfaces;
using TopSolidMingdao.Core.Models;
using TopSolidMingdao.Desktop.Commands;

namespace TopSolidMingdao.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const int PageSize = 50;
    private readonly IMingdaoService _service;
    private readonly Action<Exception> _logError;
    private readonly bool _drawingBrowsingEnabled;
    private CancellationTokenSource? _productCancellation;
    private CancellationTokenSource? _drawingCancellation;
    private CancellationTokenSource? _searchCancellation;
    private int _productVersion;
    private int _drawingVersion;
    private int _drawingPage;
    private int? _productTotal;
    private int? _drawingTotal;
    private bool _hasLoadedProducts;
    private bool _hasLoadedDrawings;
    private bool _productHasMore;
    private bool _disposed;
    private string _searchText = "";
    private Product? _selectedProduct;

    public MainViewModel(IMingdaoService service, string environmentLabel, Action<Exception> logError,
        bool drawingBrowsingEnabled = true)
    {
        _service = service;
        _logError = logError;
        _drawingBrowsingEnabled = drawingBrowsingEnabled;
        EnvironmentLabel = environmentLabel;
        SearchCommand = new(SearchAsync, HandleUnexpectedError, () => !_disposed);
        RefreshProductsCommand = new(RefreshProductsAsync, HandleUnexpectedError,
            () => !_disposed && !IsLoadingProducts);
        PreviousPageCommand = new(() => LoadProductsAsync(CurrentPage - 1, false), HandleUnexpectedError,
            () => !_disposed && !IsLoadingProducts && CurrentPage > 1);
        NextPageCommand = new(() => LoadProductsAsync(CurrentPage + 1, false), HandleUnexpectedError,
            () => !_disposed && !IsLoadingProducts && _productHasMore);
        RefreshDrawingsCommand = new(() => LoadDrawingsAsync(true), HandleUnexpectedError,
            () => !_disposed && _drawingBrowsingEnabled && SelectedProduct is not null && !IsLoadingDrawings);
        LoadMoreDrawingsCommand = new(() => LoadDrawingsAsync(false), HandleUnexpectedError,
            () => !_disposed && _drawingBrowsingEnabled && HasMoreDrawings && !IsLoadingDrawings);
    }

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<DrawingItemViewModel> Drawings { get; } = [];
    public string EnvironmentLabel { get; }
    public string TimeZoneLabel => $"时间：{TimeZoneInfo.Local.DisplayName}";
    public int CurrentPage { get; private set; } = 1;
    public bool IsLoadingProducts { get; private set; }
    public bool IsLoadingDrawings { get; private set; }
    public bool HasMoreDrawings { get; private set; }
    public string? ProductError { get; private set; }
    public string? DrawingError { get; private set; }
    public bool HasProductError => ProductError is not null;
    public bool HasDrawingError => DrawingError is not null;
    public bool ShowProductPlaceholder => Products.Count == 0;
    public bool ShowDrawingPlaceholder => Drawings.Count == 0;
    public string ProductSummary => _productTotal is int total
        ? $"第 {CurrentPage} 页 · 本页 {Products.Count} 条 · 共 {total} 条"
        : $"第 {CurrentPage} 页 · 本页 {Products.Count} 条";
    public string DrawingSummary => _drawingTotal is int total
        ? $"已加载 {Drawings.Count} / {total} 个图纸条目"
        : $"已加载 {Drawings.Count} 个图纸条目";
    public string ProductPlaceholder => IsLoadingProducts ? "正在加载产品…"
        : HasProductError ? "产品加载失败，请刷新重试"
        : _hasLoadedProducts ? "未找到匹配的产品" : "等待加载产品";
    public string DrawingPlaceholder => !_drawingBrowsingEnabled ? "当前先读取产品列表，关联图纸暂未启用"
        : SelectedProduct is null ? "请选择产品查看关联图纸"
        : IsLoadingDrawings ? "正在加载关联图纸…"
        : HasDrawingError ? "图纸加载失败，请刷新重试"
        : _hasLoadedDrawings ? "暂无可见图纸" : "等待加载图纸";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value)) ScheduleSearch();
        }
    }

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        // SelectProductAsync 在返回 Task 前完成选中状态更新；异步异常由加载方法处理。
        set => _ = SelectProductAsync(value);
    }

    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand RefreshProductsCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public AsyncRelayCommand RefreshDrawingsCommand { get; }
    public AsyncRelayCommand LoadMoreDrawingsCommand { get; }

    public Task InitializeAsync() => LoadProductsAsync(1, false);

    public Task SearchAsync()
    {
        _searchCancellation?.Cancel();
        return LoadProductsAsync(1, false);
    }

    public Task RefreshProductsAsync()
    {
        _searchCancellation?.Cancel();
        return LoadProductsAsync(CurrentPage, true);
    }

    public Task SelectProductAsync(Product? product)
    {
        if (_disposed || !SetProperty(ref _selectedProduct, product, nameof(SelectedProduct)))
            return Task.CompletedTask;
        return LoadDrawingsAsync(true);
    }

    private void ScheduleSearch()
    {
        _searchCancellation?.Cancel();
        if (_disposed) return;
        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        _ = SearchAfterDelayAsync(cancellation);
    }

    private async Task SearchAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(400, cancellation.Token);
            await LoadProductsAsync(1, false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (!_disposed && ReferenceEquals(_searchCancellation, cancellation))
                HandleUnexpectedError(exception);
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation)) _searchCancellation = null;
            cancellation.Dispose();
        }
    }

    private async Task LoadProductsAsync(int page, bool preserveSelection)
    {
        if (_disposed) return;
        var selectedId = preserveSelection ? SelectedProduct?.RecordId : null;
        var version = ++_productVersion;
        _productCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _productCancellation = cancellation;
        var keyword = SearchText.Trim();
        Product? restoreSelection = null;

        await SelectProductAsync(null);
        Products.Clear();
        CurrentPage = Math.Max(1, page);
        _productTotal = null;
        _productHasMore = false;
        ProductError = null;
        IsLoadingProducts = true;
        NotifyProductState();

        try
        {
            var result = await _service.GetProductsAsync(
                new ProductQuery(keyword, new PageRequest(CurrentPage, PageSize)), cancellation.Token);
            if (!IsCurrentProductRequest(version)) return;

            // 仅在总数证实当前页失效时回到第一页，不扫描所有中间页面。
            if (result.Items.Count == 0 && CurrentPage > 1 && result.TotalCount is int total &&
                total <= (long)(CurrentPage - 1) * PageSize)
            {
                CurrentPage = 1;
                result = await _service.GetProductsAsync(
                    new ProductQuery(keyword, new PageRequest(1, PageSize)), cancellation.Token);
                if (!IsCurrentProductRequest(version)) return;
            }

            foreach (var product in result.Items) Products.Add(product);
            CurrentPage = result.PageNumber;
            _productTotal = result.TotalCount;
            _productHasMore = result.HasMore;
            _hasLoadedProducts = true;
            restoreSelection = Products.FirstOrDefault(p => p.RecordId == selectedId);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrentProductRequest(version))
            {
                ProductError = UserMessage(exception, "产品查询失败，请检查连接后刷新重试。");
                _logError(exception);
            }
        }
        finally
        {
            if (IsCurrentProductRequest(version))
            {
                IsLoadingProducts = false;
                NotifyProductState();
            }
            if (ReferenceEquals(_productCancellation, cancellation)) _productCancellation = null;
            cancellation.Dispose();
        }

        if (IsCurrentProductRequest(version) && restoreSelection is not null)
            await SelectProductAsync(restoreSelection);
    }

    private async Task LoadDrawingsAsync(bool reset)
    {
        if (_disposed || (!reset && (IsLoadingDrawings || !HasMoreDrawings))) return;
        if (reset) ResetDrawings();
        if (!_drawingBrowsingEnabled) return;
        var productId = SelectedProduct?.RecordId;
        if (productId is null) return;

        var version = ++_drawingVersion;
        var page = _drawingPage + 1;
        var cancellation = new CancellationTokenSource();
        _drawingCancellation = cancellation;
        IsLoadingDrawings = true;
        DrawingError = null;
        NotifyDrawingState();

        try
        {
            var result = await _service.GetDrawingsAsync(
                productId, new PageRequest(page, PageSize), cancellation.Token);
            if (!IsCurrentDrawingRequest(version, productId)) return;

            var knownIds = Drawings.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var drawing in result.Items)
                if (knownIds.Add(drawing.Id)) Drawings.Add(new DrawingItemViewModel(drawing));
            _drawingPage = result.PageNumber;
            _drawingTotal = result.TotalCount;
            HasMoreDrawings = result.HasMore;
            _hasLoadedDrawings = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrentDrawingRequest(version, productId))
            {
                DrawingError = UserMessage(exception, "图纸查询失败，请刷新重试。") +
                    (Drawings.Count > 0 ? " 已加载内容尚不完整，可重试加载更多。" : "");
                _logError(exception);
            }
        }
        finally
        {
            if (IsCurrentDrawingRequest(version, productId))
            {
                IsLoadingDrawings = false;
                NotifyDrawingState();
            }
            if (ReferenceEquals(_drawingCancellation, cancellation)) _drawingCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ResetDrawings()
    {
        ++_drawingVersion;
        _drawingCancellation?.Cancel();
        _drawingCancellation = null;
        Drawings.Clear();
        _drawingPage = 0;
        _drawingTotal = null;
        HasMoreDrawings = false;
        _hasLoadedDrawings = false;
        IsLoadingDrawings = false;
        DrawingError = null;
        NotifyDrawingState();
    }

    private bool IsCurrentProductRequest(int version) => !_disposed && version == _productVersion;

    private bool IsCurrentDrawingRequest(int version, string productId) =>
        !_disposed && version == _drawingVersion && SelectedProduct?.RecordId == productId;

    private static string UserMessage(Exception exception, string fallback) =>
        exception is MingdaoServiceException ? exception.Message : fallback;

    private void NotifyProductState()
    {
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(IsLoadingProducts));
        OnPropertyChanged(nameof(ProductError));
        OnPropertyChanged(nameof(HasProductError));
        OnPropertyChanged(nameof(ProductSummary));
        OnPropertyChanged(nameof(ProductPlaceholder));
        OnPropertyChanged(nameof(ShowProductPlaceholder));
        RefreshProductsCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyDrawingState()
    {
        OnPropertyChanged(nameof(IsLoadingDrawings));
        OnPropertyChanged(nameof(DrawingError));
        OnPropertyChanged(nameof(HasDrawingError));
        OnPropertyChanged(nameof(HasMoreDrawings));
        OnPropertyChanged(nameof(DrawingSummary));
        OnPropertyChanged(nameof(DrawingPlaceholder));
        OnPropertyChanged(nameof(ShowDrawingPlaceholder));
        RefreshDrawingsCommand.NotifyCanExecuteChanged();
        LoadMoreDrawingsCommand.NotifyCanExecuteChanged();
    }

    private void HandleUnexpectedError(Exception exception)
    {
        _logError(exception);
        if (_disposed) return;
        ProductError = "操作未完成，请刷新后重试；如仍有问题，请联系管理员查看日志。";
        NotifyProductState();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ++_productVersion;
        ++_drawingVersion;
        _productCancellation?.Cancel();
        _drawingCancellation?.Cancel();
        _searchCancellation?.Cancel();
        // CTS 由对应异步方法在 finally 中释放，避免与尚在运行的请求争用。
    }
}
