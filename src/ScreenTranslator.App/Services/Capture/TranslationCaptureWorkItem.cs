using ScreenTranslator.App.Services.Browser;
using ScreenTranslator.Core.Models;
using ScreenTranslator.Core.Settings;

namespace ScreenTranslator.App.Services.Capture;

public sealed record TranslationCaptureWorkItem(
    ScreenMonitor Monitor,
    PixelRect AbsoluteSelection,
    CapturedBitmap Bitmap,
    CapturedBrowserWindow? CapturedBrowser,
    IntPtr SourceWindowHandle,
    AppSettings Settings);
