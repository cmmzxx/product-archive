using TopSolidMingdao.Core.Models;

namespace TopSolidMingdao.Core.Interfaces;

public interface IMingdaoService
{
    Task<PageResult<Product>> GetProductsAsync(
        ProductQuery query, CancellationToken cancellationToken = default);

    Task<PageResult<Drawing>> GetDrawingsAsync(
        string productRecordId, PageRequest page, CancellationToken cancellationToken = default);
}
