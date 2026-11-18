using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TopSolidMingdao.Core;
using TopSolidMingdao.Core.Interfaces;
using TopSolidMingdao.Core.Models;

namespace TopSolidMingdao.Infrastructure.Mingdao;

public sealed class MingdaoApiService(
    HttpClient httpClient, MingdaoOptions options, Func<MingdaoCredentials> getCredentials) : IMingdaoService
{
    public async Task<PageResult<Product>> GetProductsAsync(
        ProductQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query.Page.Size > 1000)
            throw new MingdaoServiceException("明道云 v3 每页最多读取 1000 条产品。");
        var uri = options.ProductListUri();
        var credentials = getCredentials();
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(BuildQuery(query))
        };
        // 凭据仅放在当前请求头中，不使用 URL 或正文，也不污染共享 HttpClient 的默认头。
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        request.Headers.Add("HAP-Appid", options.AppId);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            using var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw HttpError(response.StatusCode);
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token)
                .ConfigureAwait(false);
            return ParseProducts(document.RootElement, query);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new MingdaoServiceException("明道云产品查询超时，请检查网络后重试。");
        }
        catch (HttpRequestException)
        {
            throw new MingdaoServiceException("无法连接明道云，请检查 API 地址、网络、VPN、代理及证书信任。");
        }
        catch (JsonException)
        {
            throw InvalidResponse();
        }
    }

    public Task<PageResult<Drawing>> GetDrawingsAsync(
        string productRecordId, PageRequest page, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<PageResult<Drawing>>(new MingdaoServiceException("当前仅接入产品列表，关联图纸尚未启用。"));
    }

    private Dictionary<string, object> BuildQuery(ProductQuery query)
    {
        string[] fields = string.IsNullOrWhiteSpace(options.ProductCodeFieldId)
            ? [options.ProductNameFieldId] : [options.ProductNameFieldId, options.ProductCodeFieldId];
        fields = fields.Distinct(StringComparer.Ordinal).ToArray();
        var payload = new Dictionary<string, object>
        {
            ["pageIndex"] = query.Page.Number,
            ["pageSize"] = query.Page.Size,
            ["fields"] = fields,
            ["responseFormat"] = "json",
            ["tableView"] = true,
            ["useFieldIdAsKey"] = true,
            ["includeTotalCount"] = true,
            ["includeSystemFields"] = false,
            ["sorts"] = new[]
            {
                new { field = fields[^1], isAsc = true },
                new { field = "rowid", isAsc = true }
            }
        };
        var keyword = query.Keyword.Trim();
        if (keyword.Length > 0)
            payload["filter"] = new
            {
                type = "group",
                logic = "OR",
                children = fields.Select(field => new
                    { type = "condition", field, @operator = "contains", value = keyword }).ToArray()
            };
        return payload;
    }

    private PageResult<Product> ParseProducts(JsonElement root, ProductQuery query)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("success", out var success) ||
            success.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw InvalidResponse();
        int? errorCode = null;
        if (root.TryGetProperty("error_code", out var code))
        {
            if (code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var number)) throw InvalidResponse();
            errorCode = number;
        }
        if (!success.GetBoolean() || (errorCode.HasValue && errorCode != 1)) throw BusinessError(errorCode);
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            throw InvalidResponse();

        var products = new List<Product>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object) throw InvalidResponse();
            // 已核验的 tableView 响应使用 rowid；官方其他 JSON 响应使用 id。
            // _id 是内部标识，不能用来代替业务记录 ID。
            var recordId = ReadIdentity(row, "rowid") ?? ReadIdentity(row, "id");
            if (recordId is null) throw new MingdaoServiceException("产品响应缺少业务记录 ID，请检查 API 返回格式。");
            var name = ReadField(row, options.ProductNameFieldId, required: true) ?? "未命名产品";
            var productCode = string.IsNullOrWhiteSpace(options.ProductCodeFieldId)
                ? null : ReadField(row, options.ProductCodeFieldId, required: false);
            products.Add(new Product(recordId, name, productCode));
        }

        int? total = null;
        if (data.TryGetProperty("total", out var totalValue) && totalValue.ValueKind != JsonValueKind.Null)
        {
            if (totalValue.ValueKind != JsonValueKind.Number || !totalValue.TryGetInt32(out var number) || number < 0)
                throw InvalidResponse();
            total = number;
        }
        var offset = (long)(query.Page.Number - 1) * query.Page.Size;
        var hasMore = total.HasValue ? offset + products.Count < total.Value : products.Count == query.Page.Size;
        return new PageResult<Product>(products, query.Page.Number, hasMore, total);
    }

    private static string? ReadIdentity(JsonElement row, string key) =>
        row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? Normalize(value.GetString()) : null;

    private static string? ReadField(JsonElement row, string fieldId, bool required)
    {
        if (row.TryGetProperty(fieldId, out var value)) return ReadText(value);
        // 官方文档同时列出动态字段对象与 fields 数组；仅兼容这两种明确的结构。
        if (row.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
            foreach (var field in fields.EnumerateArray())
                if (field.ValueKind == JsonValueKind.Object && field.TryGetProperty("id", out var id) &&
                    id.ValueKind == JsonValueKind.String && id.GetString() == fieldId &&
                    field.TryGetProperty("value", out value)) return ReadText(value);
        if (required) throw new MingdaoServiceException("响应中缺少产品名称字段，请检查字段 ID 和读取权限。");
        return null;
    }

    private static string? ReadText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => Normalize(value.GetString()),
        _ => throw new MingdaoServiceException("产品名称或编码不是文本值，请检查字段映射与接口返回格式。")
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MingdaoServiceException InvalidResponse() =>
        new("明道云响应格式不符合 v3 产品列表协议，请检查 API 地址、版本和字段配置。");

    private static MingdaoServiceException HttpError(HttpStatusCode status) => new(status switch
    {
        HttpStatusCode.Unauthorized => "明道云认证失败，请检查连接令牌。",
        HttpStatusCode.Forbidden => "当前明道云连接身份无权读取产品，请检查权限和 IP 白名单。",
        HttpStatusCode.NotFound => "明道云接口或工作表不存在，请检查 API 根地址和工作表 ID。",
        HttpStatusCode.TooManyRequests => "明道云请求过于频繁，请稍后重试。",
        _ when (int)status is >= 300 and < 400 => "明道云接口返回重定向，请配置最终 HTTPS API 地址。",
        _ => $"明道云服务请求失败（HTTP {(int)status}），请稍后重试。"
    });

    private static MingdaoServiceException BusinessError(int? code)
    {
        // 不直接回显 error_msg，服务端错误文本可能包含请求数据或敏感凭据。
        var message = code switch
        {
            10101 or 10102 or 10105 or 10106 => "明道云认证失败或连接令牌已失效，请更新令牌。",
            10000 or 10005 or 430014 => "当前连接身份无读取权限，或访问 IP 受限。",
            10007 or 430013 or 430022 => "产品工作表不存在或不可访问，请检查工作表 ID。",
            51 or 90000 => "明道云请求次数受限，请稍后重试。",
            10001 or 10002 => "明道云查询参数无效，请检查字段 ID 和 API 版本。",
            _ => "明道云产品查询失败，请联系管理员检查接口状态。"
        };
        return new MingdaoServiceException(code.HasValue ? $"{message}（错误码 {code.Value}）" : message);
    }
}
