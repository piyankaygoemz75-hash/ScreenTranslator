using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace ScreenTranslator.App.ViewModels;

public sealed class SelectionOverlayViewModel : ObservableObject
{
    private ImageSource? _screenshot;
    private Rect _selection;
    private Point _anchor;
    private bool _isSelecting;
    private bool _isContinuous;

    public ImageSource? Screenshot
    {
        get => _screenshot;
        set => SetProperty(ref _screenshot, value);
    }

    public Rect Selection
    {
        get => _selection;
        private set
        {
            if (SetProperty(ref _selection, value))
            {
                OnPropertyChanged(nameof(SelectionSizeText));
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool IsSelecting
    {
        get => _isSelecting;
        private set => SetProperty(ref _isSelecting, value);
    }

    public bool HasSelection => Selection.Width >= 1 && Selection.Height >= 1;

    public bool IsContinuous
    {
        get => _isContinuous;
        set
        {
            if (SetProperty(ref _isContinuous, value))
            {
                OnPropertyChanged(nameof(InstructionText));
                OnPropertyChanged(nameof(CancelText));
            }
        }
    }

    public string InstructionText => IsContinuous
        ? "连续框选 · 松开后继续选择"
        : "拖动框选要翻译的内容";

    public string CancelText => IsContinuous
        ? "·  Esc 或右键结束"
        : "·  Esc 取消";

    public string SelectionSizeText =>
        $"{Math.Round(Selection.Width)} × {Math.Round(Selection.Height)}";

    public void BeginSelection(Point point)
    {
        _anchor = point;
        Selection = new Rect(point, point);
        IsSelecting = true;
    }

    public void UpdateSelection(Point point)
    {
        if (!IsSelecting)
        {
            return;
        }

        Selection = new Rect(
            new Point(Math.Min(_anchor.X, point.X), Math.Min(_anchor.Y, point.Y)),
            new Point(Math.Max(_anchor.X, point.X), Math.Max(_anchor.Y, point.Y)));
    }

    public Rect CompleteSelection(Point point)
    {
        UpdateSelection(point);
        IsSelecting = false;
        return Selection;
    }

    public void Reset()
    {
        IsSelecting = false;
        Selection = Rect.Empty;
    }
}
