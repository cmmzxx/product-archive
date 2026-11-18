using System.IO;
using System.Text.Json;
using TopSolidMingdao.Infrastructure.Mingdao;

namespace TopSolidMingdao.Desktop.Configuration;

public sealed class AppSettings
{
    public string DataMode { get; init; } = "";
    public MingdaoOptions Mingdao { get; init; } = new();

    public static AppSettings Load()
    {
        // TopSolid 启动时的工作目录不一定等于程序目录。
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))
            ?? throw new InvalidDataException("无法读取应用配置。");
        if (settings.DataMode is not ("Demo" or "Mingdao"))
            throw new InvalidDataException("DataMode 必须为 Demo 或 Mingdao。");
        return settings;
    }
}
