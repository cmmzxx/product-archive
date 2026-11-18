namespace TopSolidMingdao.Core.Models;

// Id 标识一个展示条目；同一图纸记录中的不同附件必须使用不同 Id。
public sealed record Drawing(
    string Id,
    string Name,
    string? FileName,
    string? Format,
    long? SizeBytes,
    string? Version,
    DateTimeOffset? UpdatedAt,
    string Source);
