using System.Text;
using DeskBox.Core.Persistence;

namespace DeskBox.Tests;

public sealed class TaskbarPinServiceTests
{
    // Root "User Pinned" item (0x1F 0x80 + GUID) and a "TaskBar" folder item.
    private static readonly byte[] Root = [20, 0, 0x1F, 0x80, .. new byte[16]];
    private static readonly byte[] TaskBarItem = [22, 0, 0x31, 0, .. new byte[10], .. Encoding.ASCII.GetBytes("TaskBar\0")];

    private static byte[] FileItem(string name)
    {
        byte[] body = [0x32, 0, .. new byte[10], .. Encoding.Unicode.GetBytes(name + "\0"), 0x1C, 0];
        return [.. BitConverter.GetBytes((ushort)(body.Length + 2)), .. body];
    }

    private static byte[] Entry(byte[] pidl) => [0, .. BitConverter.GetBytes(pidl.Length), .. pidl];

    [Fact]
    public void AppendFindAndRemove_RoundTripsThePinList()
    {
        byte[] existing = [.. Root, .. TaskBarItem, .. FileItem("Chrome.lnk"), 0, 0];
        byte[] favorites = [.. Entry(existing), 0xFF];

        byte[]? prefix = TaskbarPinService.FindTaskBarPrefix(favorites);
        Assert.Equal([.. Root, .. TaskBarItem], prefix);

        byte[] pidl = TaskbarPinService.BuildPinnedPidl(prefix!, FileItem("Dev (cca0).lnk"), "OpenWinBox.TaskbarFolder.abc", DateTime.UtcNow);
        Assert.Equal(pidl.Length - prefix!.Length - 2, BitConverter.ToUInt16(pidl, prefix.Length));
        Assert.Equal([0, 0], pidl[^2..]);

        byte[] appended = TaskbarPinService.AppendEntry(favorites, pidl);
        Assert.Equal(0xFF, appended[^1]);
        Assert.Equal(1, TaskbarPinService.FindEntryIndex(appended, "Dev (cca0).lnk"));
        Assert.Equal(-1, TaskbarPinService.FindEntryIndex(appended, "Other.lnk"));
        Assert.Equal(favorites, TaskbarPinService.RemoveEntry(appended, 1));
    }

    [Fact]
    public void RemoveResolveEntry_DropsTheEntryAtTheSameIndex()
    {
        byte[] resolve = [.. BitConverter.GetBytes(2), 1, 1, .. BitConverter.GetBytes(3), 2, 2, 2];

        Assert.Equal([.. BitConverter.GetBytes(2), 1, 1], TaskbarPinService.RemoveResolveEntry(resolve, 1));
        Assert.Throws<InvalidDataException>(() => TaskbarPinService.RemoveResolveEntry(resolve, 2));
    }

    [Fact]
    public void ReadEntries_RejectsAListWithoutEndMarker()
    {
        Assert.Throws<InvalidDataException>(() => TaskbarPinService.ReadEntries([0, 2, 0, 0, 0, 9, 9]));
    }
}
