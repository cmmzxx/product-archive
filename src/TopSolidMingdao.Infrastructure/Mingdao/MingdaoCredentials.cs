using TopSolidMingdao.Core;

namespace TopSolidMingdao.Infrastructure.Mingdao;

public sealed class MingdaoCredentials
{
    public MingdaoCredentials(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new MingdaoServiceException("未配置明道云令牌。请在 Windows 启动环境中设置 TOPSOLID_MINGDAO_TOKEN，或使用 scripts/start.ps1 启动。");
        if (accessToken.Any(char.IsControl))
            throw new MingdaoServiceException("明道云凭据包含无效控制字符，请重新配置。");
        AccessToken = accessToken.Trim();
    }

    internal string AccessToken { get; }

    public static MingdaoCredentials FromEnvironment() => new(
        Environment.GetEnvironmentVariable("TOPSOLID_MINGDAO_TOKEN") ?? "");
}
