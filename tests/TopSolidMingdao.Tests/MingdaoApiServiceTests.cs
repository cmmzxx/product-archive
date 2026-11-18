using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using TopSolidMingdao.Core;
using TopSolidMingdao.Core.Models;
using TopSolidMingdao.Infrastructure.Mingdao;
using Xunit;

namespace TopSolidMingdao.Tests;

public sealed class MingdaoApiServiceTests
{
    [Fact]
    public async Task RequestUsesVerifiedGatewayAuthenticationAndServerSideSearch()
    {
        var calls = 0;
        using var client = Client(async (request, cancellation) =>
        {
            calls++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://example.test/gateway/api/v3/app/worksheets/products/rows/list", request.RequestUri?.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            Assert.Equal("test-app", Assert.Single(request.Headers.GetValues("HAP-Appid")));
            var text = await request.Content!.ReadAsStringAsync(cancellation);
            Assert.DoesNotContain("test-token", text);
            using var body = JsonDocument.Parse(text);
            var root = body.RootElement;
            Assert.Equal(2, root.GetProperty("pageIndex").GetInt32());
            Assert.Equal(50, root.GetProperty("pageSize").GetInt32());
            Assert.True(root.GetProperty("tableView").GetBoolean());
            Assert.True(root.GetProperty("useFieldIdAsKey").GetBoolean());
            Assert.True(root.GetProperty("includeTotalCount").GetBoolean());
            Assert.Equal(new[] { "name", "code" }, root.GetProperty("fields").EnumerateArray().Select(v => v.GetString()).ToArray());
            var filter = root.GetProperty("filter");
            Assert.Equal("OR", filter.GetProperty("logic").GetString());
            Assert.Equal(2, filter.GetProperty("children").GetArrayLength());
            Assert.All(filter.GetProperty("children").EnumerateArray(), condition =>
            {
                Assert.Equal("contains", condition.GetProperty("operator").GetString());
                Assert.Equal("支架", condition.GetProperty("value").GetString());
            });
            return Json("""
                {"success":true,"error_code":1,"data":{"rows":[
                  {"_id":"internal-id","rowid":"business-id","name":"支架","code":""}
                ],"total":51}}
                """);
        });
        var result = await Service(client).GetProductsAsync(new ProductQuery("  支架  ", new PageRequest(2)));
        Assert.Equal("business-id", Assert.Single(result.Items).RecordId);
        Assert.Null(result.Items[0].Code);
        Assert.Equal(51, result.TotalCount);
        Assert.False(result.HasMore);
        Assert.Equal(1, calls);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task DocumentedFieldsArrayAndIdAreMappedWithoutInternalIdFallback()
    {
        using var client = Client((_, _) => Task.FromResult(Json("""
            {"success":true,"data":{"rows":[
              {"id":"documented-id","fields":[{"id":"name","value":"产品 A"},{"id":"code","value":"CP-01"}]}
            ],"total":1}}
            """)));
        var result = await Service(client).GetProductsAsync(new ProductQuery("", new PageRequest()));
        var product = Assert.Single(result.Items);
        Assert.Equal("documented-id", product.RecordId);
        Assert.Equal("产品 A", product.Name);
        Assert.Equal("CP-01", product.Code);
    }

    [Fact]
    public async Task MissingCodeMappingDoesNotProduceAnEmptySortFieldOrCodeCondition()
    {
        using var client = Client(async (request, cancellation) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            var root = body.RootElement;
            Assert.Single(root.GetProperty("fields").EnumerateArray());
            Assert.Single(root.GetProperty("filter").GetProperty("children").EnumerateArray());
            Assert.Equal("name", root.GetProperty("sorts")[0].GetProperty("field").GetString());
            return Json("""{"success":true,"data":{"rows":[],"total":0}}""");
        });
        var options = Options(codeField: "");
        var result = await Service(client, options).GetProductsAsync(new ProductQuery("关键词", new PageRequest()));
        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
    }

    [Fact]
    public async Task MissingTotalIsNotReportedAsZero()
    {
        using var client = Client((_, _) => Task.FromResult(Json("""
            {"success":true,"data":{"rows":[{"rowid":"r1","name":"产品","code":null}]}}
            """)));
        var result = await Service(client).GetProductsAsync(new ProductQuery("", new PageRequest(1, 1)));
        Assert.Null(result.TotalCount);
        Assert.True(result.HasMore);
    }

    [Theory]
    [InlineData("{\"success\":true,\"data\":{\"rows\":[{\"_id\":\"internal-only\",\"name\":\"产品\"}]}}")]
    [InlineData("{\"success\":true,\"data\":{\"rows\":[{\"rowid\":\"r1\",\"wrong-name\":\"产品\"}]}}")]
    [InlineData("{\"success\":true,\"data\":{\"rows\":\"not-an-array\"}}")]
    [InlineData("{\"success\":true,\"data\":{\"rows\":[],\"total\":-1}}")]
    [InlineData("{\"success\":true,\"data\":{\"rows\":[{\"rowid\":\"r1\",\"name\":{\"unexpected\":true}}]}}")]
    [InlineData("<html>not the API</html>")]
    public async Task InvalidResponseCannotBecomeAnEmptySuccessfulProductList(string response)
    {
        using var client = Client((_, _) => Task.FromResult(Json(response)));
        await Assert.ThrowsAsync<MingdaoServiceException>(() => Service(client)
            .GetProductsAsync(new ProductQuery("", new PageRequest())));
    }

    [Fact]
    public async Task BusinessFailureInHttp200DoesNotEchoServerTextOrReturnDemoData()
    {
        using var client = Client((_, _) => Task.FromResult(Json("""
            {"success":false,"error_code":10105,"error_msg":"sensitive-server-text-test-token","data":null}
            """)));
        var exception = await Assert.ThrowsAsync<MingdaoServiceException>(() => Service(client)
            .GetProductsAsync(new ProductQuery("", new PageRequest())));
        Assert.Contains("令牌", exception.Message);
        Assert.Contains("10105", exception.Message);
        Assert.DoesNotContain("sensitive-server-text", exception.Message);
        Assert.DoesNotContain("test-token", exception.Message);
    }

    [Theory]
    [InlineData(401, "认证")]
    [InlineData(403, "权限")]
    [InlineData(404, "不存在")]
    [InlineData(429, "频繁")]
    [InlineData(302, "重定向")]
    [InlineData(503, "HTTP 503")]
    public async Task HttpFailuresAreUserFacingAndNeverRetriedIndefinitely(int status, string expected)
    {
        var calls = 0;
        using var client = Client((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        });
        var exception = await Assert.ThrowsAsync<MingdaoServiceException>(() => Service(client)
            .GetProductsAsync(new ProductQuery("", new PageRequest())));
        Assert.Contains(expected, exception.Message);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CallerCancellationReachesHttpAndRemainsCancellation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = Client(async (_, cancellation) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            return Json("{}");
        });
        using var cancellation = new CancellationTokenSource();
        var load = Service(client).GetProductsAsync(new ProductQuery("", new PageRequest()), cancellation.Token);
        await entered.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
    }

    [Fact]
    public async Task HttpTimeoutIsDifferentFromUserCancellation()
    {
        using var client = Client((_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException()));
        var exception = await Assert.ThrowsAsync<MingdaoServiceException>(() => Service(client)
            .GetProductsAsync(new ProductQuery("", new PageRequest())));
        Assert.Contains("超时", exception.Message);
    }

    [Fact]
    public async Task MissingTokenDoesNotSendAnyRequest()
    {
        var calls = 0;
        using var client = Client((_, _) =>
        {
            calls++;
            return Task.FromResult(Json("{}"));
        });
        var service = new MingdaoApiService(client, Options(), () => new MingdaoCredentials(""));
        await Assert.ThrowsAsync<MingdaoServiceException>(() => service
            .GetProductsAsync(new ProductQuery("", new PageRequest())));
        Assert.Equal(0, calls);
    }

    private static MingdaoApiService Service(HttpClient client, MingdaoOptions? options = null) =>
        new(client, options ?? Options(), () => new MingdaoCredentials("test-token"));

    private static MingdaoOptions Options(string? codeField = "code") => new()
    {
        ApiBaseUrl = "https://example.test/gateway/api/",
        AppId = "test-app",
        ProductWorksheetId = "products",
        ProductNameFieldId = "name",
        ProductCodeFieldId = codeField
    };

    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new StubHandler(send));

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
