using ScreenTranslator.Core.Browser;
using ScreenTranslator.Core.Models;

namespace ScreenTranslator.App.Services.Browser;

public interface ITrackedOverlay
{
    DipRect TrackingBounds { get; }

    void MoveTo(DipRect bounds);

    void SetTrackingVisibility(bool visible);
}

public sealed class BrowserFollowInvalidatedEventArgs(string reason) : EventArgs
{
    public string Reason { get; } = reason;
}

public sealed class BrowserFollowCoordinator
{
    private readonly BrowserTrackingSession _session;
    private readonly IReadOnlyList<ITrackedOverlay> _overlays;
    private DipRect _selectionBounds;
    private bool _invalidated;

    public BrowserFollowCoordinator(
        BrowserTrackingSession session,
        IReadOnlyList<ITrackedOverlay> overlays,
        DipRect selectionBounds)
    {
        _session = session;
        _overlays = overlays;
        _selectionBounds = selectionBounds;
    }

    public event EventHandler<BrowserFollowInvalidatedEventArgs>? Invalidated;

    public bool IsInvalidated => _invalidated;

    public BrowserKind Browser => _session.Browser;

    public int BrowserWindowId => _session.BrowserWindowId;

    public int TabId => _session.TabId;

    public void Handle(BrowserMessage message)
    {
        if (_invalidated)
        {
            return;
        }

        var update = _session.Apply(message);
        switch (update.Decision)
        {
            case BrowserSessionDecision.Ignore:
                return;
            case BrowserSessionDecision.Invalidate:
                Invalidate(update.Reason ?? "浏览器跟随会话已失效。");
                return;
            case BrowserSessionDecision.Move:
                ApplyMove(update);
                return;
            default:
                Invalidate("浏览器跟随返回了未知状态。");
                return;
        }
    }

    public void OffsetWithBrowserWindow(double deltaXDip, double deltaYDip)
    {
        if (_invalidated
            || !double.IsFinite(deltaXDip)
            || !double.IsFinite(deltaYDip))
        {
            return;
        }

        _selectionBounds = Translate(
            _selectionBounds,
            deltaXDip,
            deltaYDip);
        foreach (var overlay in _overlays)
        {
            overlay.MoveTo(Translate(
                overlay.TrackingBounds,
                deltaXDip,
                deltaYDip));
        }
    }

    public void Invalidate(string reason)
    {
        if (_invalidated)
        {
            return;
        }

        _invalidated = true;
        foreach (var overlay in _overlays)
        {
            overlay.SetTrackingVisibility(false);
        }

        Invalidated?.Invoke(
            this,
            new BrowserFollowInvalidatedEventArgs(reason));
    }

    private void ApplyMove(BrowserSessionUpdate update)
    {
        foreach (var overlay in _overlays)
        {
            var result = update.ScrollContainerDip is { } container
                ? OverlayFollowCalculator.ApplyNestedScroll(
                    overlay.TrackingBounds,
                    _selectionBounds,
                    container,
                    update.DeltaXDip,
                    update.DeltaYDip)
                : OverlayFollowCalculator.ApplyRootScroll(
                    overlay.TrackingBounds,
                    _selectionBounds,
                    update.DeltaXDip,
                    update.DeltaYDip);

            if (result.WasMoved)
            {
                overlay.MoveTo(result.Bounds);
            }

            overlay.SetTrackingVisibility(result.IsVisible);
        }
    }

    private static DipRect Translate(
        DipRect bounds,
        double deltaX,
        double deltaY) =>
        new(
            bounds.X + deltaX,
            bounds.Y + deltaY,
            bounds.Width,
            bounds.Height);
}
