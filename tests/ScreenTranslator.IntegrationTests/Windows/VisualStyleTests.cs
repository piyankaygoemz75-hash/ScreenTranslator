using System.Xml.Linq;
using ScreenTranslator.App.Services.Appearance;
using Wpf.Ui.Appearance;

namespace ScreenTranslator.IntegrationTests.Windows;

public sealed class VisualStyleTests
{
    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void Badges_Use_Compact_Rounded_Rectangle_Style()
    {
        var application = LoadXaml("src", "ScreenTranslator.App", "App.xaml");
        var badgeStyle = Assert.Single(
            application.Descendants(Presentation + "Style"),
            element =>
                (string?)element.Attribute(Xaml + "Key")
                == "CompactBadgeStyle");
        var cornerSetter = Assert.Single(
            badgeStyle.Elements(Presentation + "Setter"),
            element =>
                (string?)element.Attribute("Property")
                == "CornerRadius");

        Assert.Equal("5", (string?)cornerSetter.Attribute("Value"));
        AssertUsesCompactBadge(
            LoadXaml("src", "ScreenTranslator.App", "Pages", "GeneralPage.xaml"));
        AssertUsesCompactBadge(
            LoadXaml("src", "ScreenTranslator.App", "Pages", "AppearancePage.xaml"));
    }

    [Fact]
    public void Reported_Accent_Labels_Use_Theme_Appropriate_Foreground()
    {
        var generalPage = LoadXaml(
            "src",
            "ScreenTranslator.App",
            "Pages",
            "GeneralPage.xaml");
        var translationPage = LoadXaml(
            "src",
            "ScreenTranslator.App",
            "Pages",
            "TranslationPage.xaml");
        var appearancePage = LoadXaml(
            "src",
            "ScreenTranslator.App",
            "Pages",
            "AppearancePage.xaml");

        AssertAccentForeground(generalPage, "PrimaryCaptureButtonText");
        AssertAccentForeground(generalPage, "HotkeyBadgeText");
        AssertAccentForeground(translationPage, "SaveButtonText");
        AssertAccentForeground(appearancePage, "PreviewBadgeText");
    }

    [Fact]
    public void Translation_Surfaces_Are_Opaque_At_Full_Window_Opacity()
    {
        Assert.Equal(
            byte.MaxValue,
            TranslationSurfacePalette
                .CreateBrush(ApplicationTheme.Light)
                .Color
                .A);
        Assert.Equal(
            byte.MaxValue,
            TranslationSurfacePalette
                .CreateBrush(ApplicationTheme.Dark)
                .Color
                .A);

        AssertOpaqueTranslationSurface(
            LoadXaml(
                "src",
                "ScreenTranslator.App",
                "Windows",
                "SidePanelWindow.xaml"));
        AssertOpaqueTranslationSurface(
            LoadXaml(
                "src",
                "ScreenTranslator.App",
                "Windows",
                "TextOverlayWindow.xaml"));
    }

    private static void AssertUsesCompactBadge(XDocument document)
    {
        Assert.Contains(
            document.Descendants(Presentation + "Border"),
            element =>
                (string?)element.Attribute("Style")
                == "{StaticResource CompactBadgeStyle}");
    }

    private static void AssertOpaqueTranslationSurface(XDocument document)
    {
        var rootCard = Assert.Single(
            document.Descendants(Presentation + "Border"),
            element =>
                (string?)element.Attribute(Xaml + "Name")
                == "RootCard");

        Assert.Equal("1", (string?)rootCard.Attribute("Opacity"));
        Assert.Equal(
            "{DynamicResource TranslationSurfaceBrush}",
            (string?)rootCard.Attribute("Background"));
    }

    private static void AssertAccentForeground(
        XDocument document,
        string elementName)
    {
        var element = Assert.Single(
            document.Descendants(Presentation + "TextBlock"),
            candidate =>
                (string?)candidate.Attribute(Xaml + "Name") == elementName);

        Assert.Equal(
            "{DynamicResource TextOnAccentFillColorPrimaryBrush}",
            (string?)element.Attribute("Foreground"));
    }

    private static XDocument LoadXaml(params string[] pathParts)
    {
        var repositoryRoot = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                ".."));
        return XDocument.Load(
            Path.Combine([repositoryRoot, .. pathParts]));
    }
}
