using System.IO;

namespace TopSolidMingdao.Desktop.Diagnostics;

internal static class LocalLog
{
    private static readonly object Gate = new();

    public static void Write(Exception exception)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "TopSolidMingdao", "logs");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "app.log");
                if (File.Exists(path) && new FileInfo(path).Length > 1_048_576)
                    File.Move(path, Path.Combine(directory, "app.previous.log"), overwrite: true);

                // 不记录 Message 或响应正文，避免将未来 API 异常中的凭据写入日志。
                File.AppendAllText(path,
                    $"{DateTimeOffset.Now:O} {exception.GetType().FullName}{Environment.NewLine}" +
                    $"{exception.StackTrace}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // 日志目录不可写时，仍应让界面正常反馈原始业务失败。
        }
    }
}
