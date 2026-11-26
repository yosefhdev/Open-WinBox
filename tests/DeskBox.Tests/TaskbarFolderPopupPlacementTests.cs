using DeskBox.Platform;
using DeskBox.Views;
using Windows.Graphics;

namespace DeskBox.Tests;

public sealed class TaskbarFolderPopupPlacementTests
{
    private static readonly Win32Helper.RECT Monitor = new() { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

    [Fact]
    public void BottomTaskbar_CentersOnButtonAndSitsAboveTaskbar()
    {
        var work = new Win32Helper.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1032 };

        RectInt32 bounds = TaskbarFolderPopup.ComputeTaskbarBounds(Monitor, work, 900, 1056, 400, 200, 12, out var edge);

        Assert.Equal(TaskbarFolderPopup.TaskbarEdge.Bottom, edge);
        Assert.Equal(700, bounds.X);
        Assert.Equal(1032 - 200 - 12, bounds.Y);
    }

    [Fact]
    public void AnchorInScreenMiddle_StillDocksToTaskbarEdge()
    {
        var work = new Win32Helper.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1032 };

        RectInt32 bounds = TaskbarFolderPopup.ComputeTaskbarBounds(Monitor, work, 960, 400, 400, 200, 12, out _);

        Assert.Equal(1032 - 200 - 12, bounds.Y);
    }

    [Fact]
    public void ButtonNearScreenEdge_ClampsInsideWorkArea()
    {
        var work = new Win32Helper.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1032 };

        RectInt32 bounds = TaskbarFolderPopup.ComputeTaskbarBounds(Monitor, work, 20, 1056, 400, 200, 12, out _);

        Assert.Equal(12, bounds.X);
    }

    [Theory]
    [InlineData(6, 1920, 6)]   // fits in one row
    [InlineData(30, 1920, 20)] // 1920 * 0.9 = 1728 DIP -> 20 tiles of 84 DIP
    [InlineData(0, 1920, 1)]
    [InlineData(3, 50, 1)]
    public void Columns_StayInOneRowUntilNinetyPercentOfMonitor(int apps, double monitorDip, int expected)
    {
        Assert.Equal(expected, TaskbarFolderPopup.ComputeColumns(apps, monitorDip));
    }

    [Fact]
    public void LeftTaskbar_CentersVerticallyOnButton()
    {
        var work = new Win32Helper.RECT { Left = 48, Top = 0, Right = 1920, Bottom = 1080 };

        RectInt32 bounds = TaskbarFolderPopup.ComputeTaskbarBounds(Monitor, work, 24, 500, 400, 200, 12, out var edge);

        Assert.Equal(TaskbarFolderPopup.TaskbarEdge.Left, edge);
        Assert.Equal(48 + 12, bounds.X);
        Assert.Equal(400, bounds.Y);
    }
}
