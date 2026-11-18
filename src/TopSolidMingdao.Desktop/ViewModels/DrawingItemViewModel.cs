using System.Globalization;
using System.IO;
using TopSolidMingdao.Core.Models;

namespace TopSolidMingdao.Desktop.ViewModels;

public sealed class DrawingItemViewModel(Drawing drawing)
{
    public string Id => drawing.Id;
    public string Name => drawing.Name;
    public string FileName => drawing.FileName ?? "—";
    public string Source => drawing.Source;
    public string Version => string.IsNullOrWhiteSpace(drawing.Version) ? "—" : drawing.Version;
    public string UpdatedAt => drawing.UpdatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—";

    public string Format
    {
        get
        {
            var format = drawing.Format;
            if (string.IsNullOrWhiteSpace(format))
                format = Path.GetExtension(drawing.FileName)?.TrimStart('.');
            return string.IsNullOrWhiteSpace(format) ? "未知" : format.ToUpperInvariant();
        }
    }

    public string FileSize => FormatFileSize(drawing.SizeBytes);

    private static string FormatFileSize(long? sizeBytes)
    {
        if (sizeBytes is not long bytes || bytes < 0) return "—";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1_048_576) return FormatSize(bytes / 1024d, "KiB");
        if (bytes < 1_073_741_824) return FormatSize(bytes / 1_048_576d, "MiB");
        return FormatSize(bytes / 1_073_741_824d, "GiB");
    }

    private static string FormatSize(double value, string unit) =>
        $"{value.ToString("0.#", CultureInfo.InvariantCulture)} {unit}";
}
