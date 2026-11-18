using TopSolidMingdao.Core;

namespace TopSolidMingdao.Infrastructure.Mingdao;

public sealed class MingdaoOptions
{
    public string ApiBaseUrl { get; init; } = "";
    public string DisplayName { get; init; } = "明道云";
    public string AppId { get; init; } = "";
    public string ProductWorksheetId { get; init; } = "";
    public string ProductNameFieldId { get; init; } = "";
    public string? ProductCodeFieldId { get; init; }

    internal Uri ProductListUri()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
            throw new MingdaoServiceException("请在 appsettings.json 配置有效的明道云 HTTPS API 根地址，不要包含凭据、查询参数或片段。");

        if (string.IsNullOrWhiteSpace(AppId) || string.IsNullOrWhiteSpace(ProductWorksheetId) ||
            string.IsNullOrWhiteSpace(ProductNameFieldId))
            throw new MingdaoServiceException("请配置明道云应用 ID、产品工作表 ID 和产品名称字段 ID。");

        // 使用相对路径保留私有部署的网关前缀，不能用前导斜杠将其覆盖。
        var root = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/");
        return new Uri(root, $"v3/app/worksheets/{Uri.EscapeDataString(ProductWorksheetId)}/rows/list");
    }
}
