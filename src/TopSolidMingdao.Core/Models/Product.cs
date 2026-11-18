namespace TopSolidMingdao.Core.Models;

public sealed record Product(
    string RecordId,
    string Name,
    string? Code = null,
    string? Series = null,
    string? Category = null);
