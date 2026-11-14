using DeskBox.Models;
using DeskBox.Services;
using Xunit;

namespace DeskBox.Tests;

/// <summary>
/// Every implemented widget kind must carry its own title icon in every icon mode,
/// so a new widget can never silently fall back to the default folder icon.
/// When this fails after adding a widget, finish the icon checklist in AGENTS.md.
/// </summary>
public sealed class WidgetTitleIconConsistencyTests
{
    public static IEnumerable<object[]> ImplementedKinds() =>
        WidgetContentFactory.DescriptorList
            .Where(d => d.ContentStage == WidgetContentStage.Implemented)
            .Select(d => new object[] { d.WidgetKind });

    [Theory]
    [MemberData(nameof(ImplementedKinds))]
    public void ImplementedWidget_HasItsOwnIconInEveryMode(WidgetKind widgetKind)
    {
        var descriptor = WidgetContentFactory.DescriptorList.Single(d => d.WidgetKind == widgetKind);
        string kindName = WidgetTitleIconKindNames.FromWidgetKind(widgetKind);
        Assert.NotEqual(WidgetTitleIconKindNames.Default, kindName);

        // The window title resolves by kind; the legacy glyph path must agree with it.
        if (widgetKind != WidgetKind.File)
        {
            Assert.Equal(kindName, WidgetTitleIconKindNames.FromLegacyGlyph(descriptor.DefaultGlyph));
        }

        var kind = WidgetTitleIconKindNames.NormalizeKind(kindName);
        Assert.Equal(kindName, WidgetTitleIconKindNames.ToSettingValue(kind));

        // Colour mode: the asset exists.
        string asset = WidgetTitleIconKindNames.GetColorAssetName(kind);
        Assert.NotEqual("default", asset);
        Assert.True(
            File.Exists(TestPaths.FromRepository($"src/DeskBox/Assets/WidgetTitleIcons/{asset}.svg")),
            $"Missing colour icon Assets/WidgetTitleIcons/{asset}.svg");

        // Filled and line modes: templates and mappings exist.
        string xaml = File.ReadAllText(TestPaths.FromRepository("src/DeskBox/Controls/WidgetTitleIcon.xaml"));
        string code = File.ReadAllText(TestPaths.FromRepository("src/DeskBox/Controls/WidgetTitleIcon.xaml.cs"));
        foreach (string mode in new[] { "Filled", "Line" })
        {
            Assert.Contains($"x:Key=\"{mode}{kindName}PathTemplate\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"WidgetTitleIconKind.{kindName} => \"{mode}{kindName}Path\"", code, StringComparison.Ordinal);
        }

        // Text-label mode: the label is translated in the source locale.
        string labelKey = WidgetTitleIconKindNames.GetLocalizationKey(kind);
        string en = File.ReadAllText(TestPaths.FromRepository("src/DeskBox/Strings/en-US.json"));
        Assert.Contains($"\"{labelKey}\"", en, StringComparison.Ordinal);
    }
}
