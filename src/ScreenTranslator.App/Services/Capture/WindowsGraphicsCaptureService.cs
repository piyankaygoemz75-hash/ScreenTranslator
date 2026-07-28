using ScreenTranslator.App.Interop;

namespace ScreenTranslator.App.Services.Capture;

/// <summary>
/// Capability boundary for the Windows.Graphics.Capture backend.
/// The D3D11 frame-copy pipeline is intentionally not advertised until it is
/// available; callers can safely use <see cref="FallbackScreenCaptureService"/>.
/// </summary>
public sealed class WindowsGraphicsCaptureService : IScreenCaptureService
{
    public bool IsPlatformApiPresent => GraphicsCaptureItemInterop.IsApiPresent();

    public bool IsAvailable => false;

    public string UnavailabilityReason => IsPlatformApiPresent
        ? "Windows.Graphics.Capture 可用，但当前版本尚未提供 D3D11 单帧复制后端。"
        : "当前 Windows 版本不支持 Windows.Graphics.Capture。";

    public IReadOnlyList<ScreenMonitor> GetMonitors() =>
        MonitorEnumerator.GetMonitors();

    public Task<MonitorCapture> CaptureAsync(
        ScreenMonitor monitor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new PlatformNotSupportedException(UnavailabilityReason);
    }

    public Task<IReadOnlyList<MonitorCapture>> CaptureAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new PlatformNotSupportedException(UnavailabilityReason);
    }
}
