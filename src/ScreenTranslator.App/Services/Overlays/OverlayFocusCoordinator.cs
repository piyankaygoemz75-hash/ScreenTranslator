namespace ScreenTranslator.App.Services.Overlays;

public interface IOverlayFocusTarget
{
    void SetSourceWindowActive(bool active);
}

public sealed class OverlayFocusCoordinator
{
    private readonly IntPtr _sourceWindow;
    private readonly List<IOverlayFocusTarget> _targets;

    public OverlayFocusCoordinator(
        IntPtr sourceWindow,
        IEnumerable<IOverlayFocusTarget> targets)
    {
        if (sourceWindow == IntPtr.Zero)
        {
            throw new ArgumentException(
                "Source window is required.",
                nameof(sourceWindow));
        }

        ArgumentNullException.ThrowIfNull(targets);

        _sourceWindow = sourceWindow;
        _targets = targets.ToList();
    }

    public int Count => _targets.Count;

    public bool Remove(IOverlayFocusTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return _targets.Remove(target);
    }

    public void HandleForegroundChanged(IntPtr foregroundWindow)
    {
        var active = foregroundWindow == _sourceWindow;
        foreach (var target in _targets.ToArray())
        {
            target.SetSourceWindowActive(active);
        }
    }
}
