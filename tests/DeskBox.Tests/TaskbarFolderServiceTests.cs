using DeskBox.Core.Persistence;

namespace DeskBox.Tests;

public sealed class TaskbarFolderServiceTests
{
    [Fact]
    public void BuildIco_WritesHeaderDirectoryAndPayloadOffsets()
    {
        byte[] big = [1, 2, 3, 4];
        byte[] small = [9, 8];

        byte[] ico = TaskbarFolderService.BuildIco([(256, big), (16, small)]);

        Assert.Equal(6 + (16 * 2) + big.Length + small.Length, ico.Length);
        Assert.Equal(1, BitConverter.ToUInt16(ico, 2)); // type: icon
        Assert.Equal(2, BitConverter.ToUInt16(ico, 4)); // count
        Assert.Equal(0, ico[6]); // 256 px is stored as 0
        Assert.Equal(16, ico[6 + 16]);
        Assert.Equal(big.Length, BitConverter.ToInt32(ico, 6 + 8));
        Assert.Equal(38, BitConverter.ToInt32(ico, 6 + 12));
        Assert.Equal(38 + big.Length, BitConverter.ToInt32(ico, 6 + 16 + 12));
        Assert.Equal(big, ico[38..42]);
        Assert.Equal(small, ico[42..]);
    }

    [Theory]
    [InlineData(new[] { "app.exe", "--taskbar-folder", "abc123" }, "abc123")]
    [InlineData(new[] { "app.exe", "--TASKBAR-FOLDER", "\"abc123\"" }, "abc123")]
    [InlineData(new[] { "app.exe", "--taskbar-folder" }, null)]
    [InlineData(new[] { "app.exe", "--taskbar-folder", "..\\evil" }, null)]
    [InlineData(new[] { "app.exe", "--open-settings" }, null)]
    public void TryGetFolderIdArgument_AcceptsOnlyPlainIds(string[] args, string? expected)
    {
        Assert.Equal(expected, TaskbarFolderService.TryGetFolderIdArgument(args));
    }

    [Theory]
    [InlineData("Juegos", "Juegos")]
    [InlineData("a/b:c", "a_b_c")]
    [InlineData("   ", "Folder")]
    public void SanitizeFileName_ReplacesInvalidCharacters(string input, string expected)
    {
        Assert.Equal(expected, TaskbarFolderService.SanitizeFileName(input));
    }
}
