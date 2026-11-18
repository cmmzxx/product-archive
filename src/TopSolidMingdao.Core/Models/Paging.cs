namespace TopSolidMingdao.Core.Models;

public sealed record PageRequest
{
    public PageRequest(int number = 1, int size = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        Number = number;
        Size = size;
    }

    public int Number { get; }
    public int Size { get; }
}

public sealed record ProductQuery(string Keyword, PageRequest Page);

// TotalCount 与 Items 使用相同粒度；关联记录数不能冒充附件条目总数。
public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    bool HasMore,
    int? TotalCount = null);
