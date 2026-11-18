using TopSolidMingdao.Core;
using TopSolidMingdao.Core.Interfaces;
using TopSolidMingdao.Core.Models;

namespace TopSolidMingdao.Infrastructure.Demo;

public sealed class DemoMingdaoService : IMingdaoService
{
    private static readonly string[] Names =
        ["主轴支架", "减速机安装座", "定位夹具", "联轴器护罩", "导向滑台"];

    private static readonly Product[] Products = Enumerable.Range(1, 125)
        .Select(i => new Product($"demo-product-{i}", $"{Names[(i - 1) % Names.Length]} {i:000}",
            $"PRD-{i:0000}", i % 2 == 0 ? "标准系列" : "精密系列", "机械部件"))
        .OrderBy(p => p.Name, StringComparer.Ordinal)
        .ThenBy(p => p.RecordId, StringComparer.Ordinal)
        .ToArray();

    public async Task<PageResult<Product>> GetProductsAsync(
        ProductQuery query, CancellationToken cancellationToken = default)
    {
        // 留出短暂等待，让离线演示也能检查加载与取消状态。
        await Task.Delay(150, cancellationToken);
        var keyword = query.Keyword.Trim();
        var matches = Products.Where(p =>
            p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            (p.Code?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)).ToArray();
        return Paginate(matches, query.Page);
    }

    public async Task<PageResult<Drawing>> GetDrawingsAsync(
        string productRecordId, PageRequest page, CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);
        var product = Products.FirstOrDefault(p => p.RecordId == productRecordId)
            ?? throw new MingdaoServiceException("该产品已不存在或不可访问，请刷新产品列表。");
        var index = Array.IndexOf(Products, product);
        // 第一条产品可检查多批加载，每四条中有一条可检查无图纸状态。
        var count = index == 0 ? 57 : index % 4 == 3 ? 0 : 3;
        string[] formats = ["DWG", "DXF", "TopPkg", "PDF"];
        var drawings = Enumerable.Range(1, count).Select(i =>
        {
            var format = formats[(i - 1) % formats.Length];
            return new Drawing($"{product.RecordId}-attachment-{i}", $"{product.Name} · 图纸 {i:00}",
                $"{product.Code}-{i:00}.{format.ToLowerInvariant()}", format,
                i % 3 == 0 ? null : 128_000L * i, i % 4 == 0 ? null : "A.1",
                new DateTimeOffset(2026, 11, 18, 2, 0, 0, TimeSpan.Zero).AddMinutes(-i),
                "演示附件");
        }).ToArray();
        return Paginate(drawings, page);
    }

    private static PageResult<T> Paginate<T>(T[] items, PageRequest page)
    {
        var offset = (long)(page.Number - 1) * page.Size;
        T[] result = offset >= items.Length ? [] : items.Skip((int)offset).Take(page.Size).ToArray();
        return new PageResult<T>(result, page.Number, offset + result.Length < items.Length, items.Length);
    }
}
