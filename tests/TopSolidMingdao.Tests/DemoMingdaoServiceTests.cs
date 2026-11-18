using TopSolidMingdao.Core.Models;
using TopSolidMingdao.Infrastructure.Demo;
using Xunit;

namespace TopSolidMingdao.Tests;

public sealed class DemoMingdaoServiceTests
{
    [Fact]
    public async Task ProductPagesAreDistinctAndSearchCoversAllProducts()
    {
        var service = new DemoMingdaoService();
        var first = await service.GetProductsAsync(new ProductQuery("", new PageRequest(1)));
        var second = await service.GetProductsAsync(new ProductQuery("", new PageRequest(2)));
        var search = await service.GetProductsAsync(new ProductQuery("  prd-0125  ", new PageRequest()));

        Assert.Equal(125, first.TotalCount);
        Assert.Equal(50, first.Items.Count);
        Assert.Empty(first.Items.Select(p => p.RecordId).Intersect(second.Items.Select(p => p.RecordId)));
        Assert.Equal("PRD-0125", Assert.Single(search.Items).Code);
    }

    [Fact]
    public async Task RelatedDrawingsCanContinueBeyondFirstPage()
    {
        var service = new DemoMingdaoService();
        var products = await service.GetProductsAsync(new ProductQuery("", new PageRequest()));
        var first = await service.GetDrawingsAsync(products.Items[0].RecordId, new PageRequest(1));
        var second = await service.GetDrawingsAsync(products.Items[0].RecordId, new PageRequest(2));

        Assert.Equal(50, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.Equal(7, second.Items.Count);
        Assert.False(second.HasMore);
        Assert.Equal(57, first.Items.Concat(second.Items).Select(d => d.Id).Distinct().Count());
    }

    [Fact]
    public async Task DemoCancellationIsHonored()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DemoMingdaoService()
            .GetProductsAsync(new ProductQuery("", new PageRequest()), cancellation.Token));
    }
}
