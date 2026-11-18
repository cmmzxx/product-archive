using TopSolidMingdao.Core;
using TopSolidMingdao.Core.Interfaces;
using TopSolidMingdao.Core.Models;
using TopSolidMingdao.Desktop.ViewModels;
using Xunit;

namespace TopSolidMingdao.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task OlderDrawingSuccessAndErrorCannotOverwriteASecondSelectionOfSameProduct()
    {
        var first = Pending<PageResult<Drawing>>();
        var second = Pending<PageResult<Drawing>>();
        var latest = Pending<PageResult<Drawing>>();
        var responses = new Queue<Task<PageResult<Drawing>>>([first.Task, second.Task, latest.Task]);
        var service = new ControlledService { Drawings = (_, _, _) => responses.Dequeue() };
        using var vm = CreateViewModel(service);
        var a = new Product("a", "产品 A");
        var b = new Product("b", "产品 B");

        var firstLoad = vm.SelectProductAsync(a);
        var secondLoad = vm.SelectProductAsync(b);
        var latestLoad = vm.SelectProductAsync(a);
        latest.SetResult(DrawingPage("latest"));
        await latestLoad;
        first.SetResult(DrawingPage("old-a"));
        await firstLoad;
        second.SetException(new MingdaoServiceException("旧产品请求失败"));
        await secondLoad;

        Assert.Equal("latest", Assert.Single(vm.Drawings).Id);
        Assert.Null(vm.DrawingError);
        Assert.False(vm.IsLoadingDrawings);
        Assert.Equal("a", vm.SelectedProduct?.RecordId);
    }

    [Fact]
    public async Task OldProductFailureCannotOverwriteNewSearch()
    {
        var first = Pending<PageResult<Product>>();
        var latest = Pending<PageResult<Product>>();
        var responses = new Queue<Task<PageResult<Product>>>([first.Task, latest.Task]);
        var service = new ControlledService { Products = (_, _) => responses.Dequeue() };
        using var vm = CreateViewModel(service);

        var initialLoad = vm.InitializeAsync();
        vm.SearchText = "支架";
        var search = vm.SearchAsync();
        latest.SetResult(new PageResult<Product>([new Product("new", "主轴支架")], 1, false, 1));
        await search;
        first.SetException(new MingdaoServiceException("旧搜索失败"));
        await initialLoad;

        Assert.Equal("new", Assert.Single(vm.Products).RecordId);
        Assert.Null(vm.ProductError);
        Assert.False(vm.IsLoadingProducts);
    }

    [Fact]
    public async Task OldDrawingCompletionCannotClearNewLoadingState()
    {
        var first = Pending<PageResult<Drawing>>();
        var latest = Pending<PageResult<Drawing>>();
        var responses = new Queue<Task<PageResult<Drawing>>>([first.Task, latest.Task]);
        var service = new ControlledService { Drawings = (_, _, _) => responses.Dequeue() };
        using var vm = CreateViewModel(service);
        var oldLoad = vm.SelectProductAsync(new Product("a", "A"));
        var newLoad = vm.SelectProductAsync(new Product("b", "B"));

        first.SetResult(DrawingPage("old"));
        await oldLoad;
        Assert.True(vm.IsLoadingDrawings);
        Assert.Empty(vm.Drawings);

        latest.SetResult(DrawingPage("new"));
        await newLoad;
        Assert.False(vm.IsLoadingDrawings);
    }

    [Fact]
    public async Task FailedNextPageCanRetryWithoutDuplicatingExistingDrawings()
    {
        var calls = 0;
        var one = DrawingItem("one");
        var two = DrawingItem("two");
        var service = new ControlledService
        {
            Drawings = (_, _, _) => ++calls switch
            {
                1 => Task.FromResult(new PageResult<Drawing>([one], 1, true, 2)),
                2 => Task.FromException<PageResult<Drawing>>(new MingdaoServiceException("暂时不可用")),
                _ => Task.FromResult(new PageResult<Drawing>([one, two], 2, false, 2))
            }
        };
        using var vm = CreateViewModel(service);
        await vm.SelectProductAsync(new Product("a", "A"));
        await vm.LoadMoreDrawingsCommand.ExecuteAsync();
        Assert.Single(vm.Drawings);
        Assert.NotNull(vm.DrawingError);
        Assert.True(vm.HasMoreDrawings);

        await vm.LoadMoreDrawingsCommand.ExecuteAsync();
        Assert.Equal(2, vm.Drawings.Count);
        Assert.Null(vm.DrawingError);
        Assert.False(vm.HasMoreDrawings);
    }

    [Fact]
    public async Task CompletionAfterDisposeCannotPopulateDrawings()
    {
        var response = Pending<PageResult<Drawing>>();
        var service = new ControlledService { Drawings = (_, _, _) => response.Task };
        using var vm = CreateViewModel(service);
        var load = vm.SelectProductAsync(new Product("a", "A"));
        vm.Dispose();
        response.SetResult(DrawingPage("late"));
        await load;
        Assert.Empty(vm.Drawings);
    }

    private static MainViewModel CreateViewModel(IMingdaoService service) => new(service, "测试", _ => { });

    [Fact]
    public async Task ProductOnlyModeDoesNotQueryDrawingsOnSelectionOrRefresh()
    {
        var calls = 0;
        var service = new ControlledService
        {
            Drawings = (_, _, _) =>
            {
                calls++;
                throw new InvalidOperationException("产品列表模式不应查询图纸");
            }
        };
        using var vm = new MainViewModel(service, "测试", _ => { }, drawingBrowsingEnabled: false);
        await vm.SelectProductAsync(new Product("a", "A"));
        await vm.RefreshDrawingsCommand.ExecuteAsync();
        Assert.Equal(0, calls);
        Assert.Empty(vm.Drawings);
        Assert.Null(vm.DrawingError);
        Assert.False(vm.RefreshDrawingsCommand.CanExecute(null));
        Assert.Contains("暂未启用", vm.DrawingPlaceholder);
    }

    private static TaskCompletionSource<T> Pending<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static Drawing DrawingItem(string id) => new(id, id, $"{id}.dwg", "DWG", null, null, null, "测试");

    private static PageResult<Drawing> DrawingPage(string id) => new([DrawingItem(id)], 1, false, 1);

    private sealed class ControlledService : IMingdaoService
    {
        public Func<ProductQuery, CancellationToken, Task<PageResult<Product>>> Products { get; init; } =
            (_, _) => Task.FromResult(new PageResult<Product>([], 1, false, 0));
        public Func<string, PageRequest, CancellationToken, Task<PageResult<Drawing>>> Drawings { get; init; } =
            (_, _, _) => Task.FromResult(new PageResult<Drawing>([], 1, false, 0));

        public Task<PageResult<Product>> GetProductsAsync(ProductQuery query, CancellationToken cancellationToken = default) =>
            Products(query, cancellationToken);

        // 故意忽略取消，验证客户端不能只依靠 CancellationToken 防止旧结果覆盖。
        public Task<PageResult<Drawing>> GetDrawingsAsync(string productRecordId, PageRequest page,
            CancellationToken cancellationToken = default) => Drawings(productRecordId, page, cancellationToken);
    }
}
