using TopSolidMingdao.Core.Models;
using TopSolidMingdao.Desktop.ViewModels;
using Xunit;

namespace TopSolidMingdao.Tests;

public sealed class DrawingItemViewModelTests
{
    [Theory]
    [InlineData(null, "—")]
    [InlineData(-1L, "—")]
    [InlineData(0L, "0 B")]
    [InlineData(1024L, "1 KiB")]
    [InlineData(2_097_152L, "2 MiB")]
    public void SizeDistinguishesMissingMetadataFromEmptyFile(long? bytes, string expected)
    {
        var vm = new DrawingItemViewModel(new Drawing("id", "图纸", "part.dwg", null,
            bytes, null, null, "附件"));
        Assert.Equal(expected, vm.FileSize);
        Assert.Equal("DWG", vm.Format);
        Assert.Equal("—", vm.Version);
        Assert.Equal("—", vm.UpdatedAt);
    }

    [Fact]
    public void UnknownFormatDoesNotRequireFileAccess()
    {
        var vm = new DrawingItemViewModel(new Drawing("id", "图纸", null, null,
            null, null, null, "关联记录"));
        Assert.Equal("未知", vm.Format);
    }
}
