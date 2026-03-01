using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VtNetCore.VirtualTerminal;
using VtNetCore.VirtualTerminal.Layout;
using Brushes = System.Windows.Media.Brushes;
using Clipboard = System.Windows.Clipboard;
using Color = System.Windows.Media.Color;
using FlowDirection = System.Windows.FlowDirection;
using FontFamily = System.Windows.Media.FontFamily;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace JustRDP.Presentation.Controls.Terminal;

public struct CellPosition
{
    public int Column { get; set; }
    public int Row { get; set; }

    public CellPosition(int column, int row)
    {
        Column = column;
        Row = row;
    }
}

public sealed class TerminalRenderer : FrameworkElement
{
    private readonly DrawingVisual _visual;
    private readonly Dictionary<Color, SolidColorBrush> _brushCache = new();
    private readonly DispatcherTimer _cursorBlinkTimer;

    private TerminalSession? _session;
    private TerminalColorScheme? _colorScheme;

    private Typeface _typeface = new("Consolas");
    private Typeface _boldTypeface = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    private Typeface _italicTypeface = new(new FontFamily("Consolas"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
    private Typeface _boldItalicTypeface = new(new FontFamily("Consolas"), FontStyles.Italic, FontWeights.Bold, FontStretches.Normal);
    private double _fontSize = 14;
    private double _dpiScale;

    private bool _isDirty = true;
    private bool _cursorVisible = true;
    private bool _attached;

    // Scrollback
    public int ScrollOffset { get; private set; }

    // Text selection
    private CellPosition _selectionStart;
    private CellPosition _selectionEnd;
    private bool _isSelecting;
    public bool HasSelection { get; private set; }

    public double CellWidth { get; private set; }
    public double CellHeight { get; private set; }
    public int VisibleColumns { get; private set; } = 80;
    public int VisibleRows { get; private set; } = 24;

    public TerminalRenderer()
    {
        _visual = new DrawingVisual();
        AddVisualChild(_visual);
        AddLogicalChild(_visual);

        _cursorBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _cursorBlinkTimer.Tick += (_, _) =>
        {
            _cursorVisible = !_cursorVisible;
            _isDirty = true;
        };

        Focusable = true;
        ClipToBounds = true;

        // Resolve DPI scale once loaded into visual tree
        _dpiScale = 1.0;
        Loaded += (_, _) => UpdateDpiScale();
    }

    private void UpdateDpiScale()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            _dpiScale = source.CompositionTarget.TransformToDevice.M11;
            MeasureCellSize();
            _isDirty = true;
        }
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _visual;

    public void SetFont(string fontFamily, double fontSize)
    {
        _fontSize = fontSize;
        var family = new FontFamily(fontFamily);
        _typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        _boldTypeface = new Typeface(family, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        _italicTypeface = new Typeface(family, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
        _boldItalicTypeface = new Typeface(family, FontStyles.Italic, FontWeights.Bold, FontStretches.Normal);
        MeasureCellSize();
        _isDirty = true;
    }

    private void MeasureCellSize()
    {
        var ft = new FormattedText(
            "W",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface,
            _fontSize,
            Brushes.White,
            _dpiScale);
        CellWidth = ft.WidthIncludingTrailingWhitespace;
        CellHeight = ft.Height;
    }

    public void Attach(TerminalSession session, TerminalColorScheme colorScheme)
    {
        _session = session;
        _colorScheme = colorScheme;

        UpdateDpiScale();
        MeasureCellSize();
        CalculateDimensions();

        _session.TerminalUpdated += OnTerminalUpdated;
        CompositionTarget.Rendering += OnCompositionTargetRendering;
        _cursorBlinkTimer.Start();
        _attached = true;
        _isDirty = true;
    }

    public void Detach()
    {
        _attached = false;
        _cursorBlinkTimer.Stop();
        CompositionTarget.Rendering -= OnCompositionTargetRendering;

        if (_session != null)
        {
            _session.TerminalUpdated -= OnTerminalUpdated;
            _session = null;
        }

        _colorScheme = null;
        _brushCache.Clear();
    }

    private void OnTerminalUpdated()
    {
        _isDirty = true;
    }

    private void OnCompositionTargetRendering(object? sender, EventArgs e)
    {
        if (_isDirty && _attached)
        {
            _isDirty = false;
            Render();
        }
    }

    public void CalculateDimensions()
    {
        if (CellWidth <= 0 || CellHeight <= 0)
            return;

        VisibleColumns = Math.Max(1, (int)(ActualWidth / CellWidth));
        VisibleRows = Math.Max(1, (int)(ActualHeight / CellHeight));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (CellWidth > 0 && CellHeight > 0)
        {
            VisibleColumns = Math.Max(1, (int)(availableSize.Width / CellWidth));
            VisibleRows = Math.Max(1, (int)(availableSize.Height / CellHeight));
        }
        return availableSize;
    }

    private void Render()
    {
        if (_session == null || _colorScheme == null)
            return;

        var terminal = _session.Terminal;
        using var dc = _visual.RenderOpen();

        // Draw background
        var bgBrush = GetBrush(_colorScheme.DefaultBackground);
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        // Get page spans with selection range
        VtNetCore.VirtualTerminal.TextRange? selectionRange = null;
        if (HasSelection)
        {
            selectionRange = BuildSelectionRange();
        }

        int topRow = terminal.ViewPort.TopRow;
        int startLine = Math.Max(0, topRow - ScrollOffset);

        List<LayoutRow> rows;
        try
        {
            rows = terminal.GetPageSpans(
                startLine,
                VisibleRows,
                VisibleColumns,
                selectionRange);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TerminalRenderer.Render: GetPageSpans failed: {ex.Message}");
            return;
        }

        // Render each row
        for (int rowIndex = 0; rowIndex < rows.Count && rowIndex < VisibleRows; rowIndex++)
        {
            var row = rows[rowIndex];
            double y = rowIndex * CellHeight;
            double x = 0;

            foreach (var span in row.Spans)
            {
                if (string.IsNullOrEmpty(span.Text))
                {
                    x += span.Text?.Length * CellWidth ?? 0;
                    continue;
                }

                // Parse colors
                Color fg = !string.IsNullOrEmpty(span.ForgroundColor)
                    ? TerminalColorScheme.FromHex(span.ForgroundColor)
                    : _colorScheme.DefaultForeground;

                Color bg = !string.IsNullOrEmpty(span.BackgroundColor)
                    ? TerminalColorScheme.FromHex(span.BackgroundColor)
                    : _colorScheme.DefaultBackground;

                // Draw background rectangle if not default
                double spanWidth = span.Text.Length * CellWidth;
                if (bg != _colorScheme.DefaultBackground)
                {
                    dc.DrawRectangle(GetBrush(bg), null, new Rect(x, y, spanWidth, CellHeight));
                }

                // Skip hidden text
                if (span.Hidden)
                {
                    x += spanWidth;
                    continue;
                }

                // Choose typeface
                Typeface tf = (span.Bold, span.Italic) switch
                {
                    (true, true) => _boldItalicTypeface,
                    (true, false) => _boldTypeface,
                    (false, true) => _italicTypeface,
                    _ => _typeface,
                };

                // Draw text
                var fgBrush = GetBrush(fg);
                var formattedText = new FormattedText(
                    span.Text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    tf,
                    _fontSize,
                    fgBrush,
                    _dpiScale);

                dc.DrawText(formattedText, new Point(x, y));

                // Draw underline
                if (span.Underline)
                {
                    var pen = new Pen(fgBrush, 1);
                    double underlineY = y + CellHeight - 2;
                    dc.DrawLine(pen, new Point(x, underlineY), new Point(x + spanWidth, underlineY));
                }

                x += spanWidth;
            }
        }

        // Draw cursor
        if (_cursorVisible && ScrollOffset == 0 && _session.Terminal.CursorState.ShowCursor)
        {
            int cursorCol = _session.CursorColumn;
            int cursorRow = _session.CursorRow;

            if (cursorRow >= 0 && cursorRow < VisibleRows && cursorCol >= 0 && cursorCol < VisibleColumns)
            {
                double cx = cursorCol * CellWidth;
                double cy = cursorRow * CellHeight;
                var cursorBrush = GetBrush(Color.FromArgb(0xAA, 0xCC, 0xCC, 0xCC));
                dc.DrawRectangle(cursorBrush, null, new Rect(cx, cy, CellWidth, CellHeight));
            }
        }
    }

    private SolidColorBrush GetBrush(Color color)
    {
        if (_brushCache.TryGetValue(color, out var brush))
            return brush;

        brush = new SolidColorBrush(color);
        brush.Freeze();
        _brushCache[color] = brush;
        return brush;
    }

    private VtNetCore.VirtualTerminal.TextRange? BuildSelectionRange()
    {
        if (!HasSelection)
            return null;

        var start = _selectionStart;
        var end = _selectionEnd;

        // Normalize: start before end
        if (start.Row > end.Row || (start.Row == end.Row && start.Column > end.Column))
            (start, end) = (end, start);

        var range = new VtNetCore.VirtualTerminal.TextRange();
        range.Start = new VtNetCore.VirtualTerminal.TextPosition { Column = start.Column, Row = start.Row };
        range.End = new VtNetCore.VirtualTerminal.TextPosition { Column = end.Column, Row = end.Row };
        return range;
    }

    // --- Scrollback ---

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (_session == null)
            return;

        int delta = e.Delta > 0 ? 3 : -3; // scroll up = positive offset
        int topRow = _session.Terminal.ViewPort.TopRow;
        int maxScroll = Math.Max(0, topRow);

        ScrollOffset = Math.Clamp(ScrollOffset + delta, 0, maxScroll);
        _isDirty = true;
        e.Handled = true;
    }

    // --- Text Selection ---

    private CellPosition PixelToCell(Point p)
    {
        int col = Math.Max(0, (int)(p.X / CellWidth));
        int row = Math.Max(0, (int)(p.Y / CellHeight));
        return new CellPosition(col, row);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        CaptureMouse();

        var pos = PixelToCell(e.GetPosition(this));
        _selectionStart = pos;
        _selectionEnd = pos;
        _isSelecting = true;
        HasSelection = false;
        _isDirty = true;

        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_isSelecting)
            return;

        var pos = PixelToCell(e.GetPosition(this));
        _selectionEnd = pos;

        // Only mark as selection if moved beyond start cell
        HasSelection = _selectionStart.Column != _selectionEnd.Column ||
                       _selectionStart.Row != _selectionEnd.Row;
        _isDirty = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        _isSelecting = false;

        // If click without drag, clear selection
        if (!HasSelection)
        {
            ClearSelection();
        }
    }

    public void ClearSelection()
    {
        HasSelection = false;
        _isSelecting = false;
        _isDirty = true;
    }

    public string GetSelectedText()
    {
        if (!HasSelection || _session == null)
            return string.Empty;

        var start = _selectionStart;
        var end = _selectionEnd;

        // Normalize
        if (start.Row > end.Row || (start.Row == end.Row && start.Column > end.Column))
            (start, end) = (end, start);

        // Adjust for scroll offset
        int topRow = _session.Terminal.ViewPort.TopRow;
        int viewStart = Math.Max(0, topRow - ScrollOffset);

        int startRow = viewStart + start.Row;
        int endRow = viewStart + end.Row;

        try
        {
            string text = _session.Terminal.GetText(start.Column, startRow, end.Column, endRow);
            // Trim trailing whitespace per line
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].TrimEnd();
            return string.Join("\n", lines).TrimEnd();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TerminalRenderer.GetSelectedText: text extraction failed: {ex.Message}");
            return string.Empty;
        }
    }

    public void CopySelectionToClipboard()
    {
        string text = GetSelectedText();
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
        }
    }
}
