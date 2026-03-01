using System.Text;
using System.Windows;
using System.Windows.Input;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace JustRDP.Presentation.Controls.Terminal;

public sealed class TerminalInputHandler
{
    private readonly TerminalSession _session;
    private readonly TerminalRenderer _renderer;

    // Manual fallback mapping: WPF key name -> VT escape sequence
    private static readonly Dictionary<Key, string> EscapeSequences = new()
    {
        [Key.Up] = "\x1b[A",
        [Key.Down] = "\x1b[B",
        [Key.Right] = "\x1b[C",
        [Key.Left] = "\x1b[D",
        [Key.Home] = "\x1b[H",
        [Key.End] = "\x1b[F",
        [Key.Insert] = "\x1b[2~",
        [Key.Delete] = "\x1b[3~",
        [Key.PageUp] = "\x1b[5~",
        [Key.PageDown] = "\x1b[6~",
        [Key.F1] = "\x1bOP",
        [Key.F2] = "\x1bOQ",
        [Key.F3] = "\x1bOR",
        [Key.F4] = "\x1bOS",
        [Key.F5] = "\x1b[15~",
        [Key.F6] = "\x1b[17~",
        [Key.F7] = "\x1b[18~",
        [Key.F8] = "\x1b[19~",
        [Key.F9] = "\x1b[20~",
        [Key.F10] = "\x1b[21~",
        [Key.F11] = "\x1b[23~",
        [Key.F12] = "\x1b[24~",
    };

    // Application cursor mode sequences (DECCKM)
    private static readonly Dictionary<Key, string> AppCursorSequences = new()
    {
        [Key.Up] = "\x1bOA",
        [Key.Down] = "\x1bOB",
        [Key.Right] = "\x1bOC",
        [Key.Left] = "\x1bOD",
        [Key.Home] = "\x1bOH",
        [Key.End] = "\x1bOF",
    };

    // VtNetCore key name mapping
    private static readonly Dictionary<Key, string> VtNetCoreKeyNames = new()
    {
        [Key.Up] = "Up",
        [Key.Down] = "Down",
        [Key.Left] = "Left",
        [Key.Right] = "Right",
        [Key.Home] = "Home",
        [Key.End] = "End",
        [Key.Insert] = "Insert",
        [Key.Delete] = "Delete",
        [Key.PageUp] = "PageUp",
        [Key.PageDown] = "PageDown",
        [Key.F1] = "F1",
        [Key.F2] = "F2",
        [Key.F3] = "F3",
        [Key.F4] = "F4",
        [Key.F5] = "F5",
        [Key.F6] = "F6",
        [Key.F7] = "F7",
        [Key.F8] = "F8",
        [Key.F9] = "F9",
        [Key.F10] = "F10",
        [Key.F11] = "F11",
        [Key.F12] = "F12",
        [Key.Tab] = "Tab",
        [Key.Escape] = "Escape",
        [Key.Back] = "Backspace",
        [Key.Enter] = "Enter",
    };

    public TerminalInputHandler(TerminalSession session, TerminalRenderer renderer)
    {
        _session = session;
        _renderer = renderer;
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (!_session.IsConnected)
            return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ctrl+Shift+V or Ctrl+V — paste
        if (ctrl && key == Key.V)
        {
            PasteClipboard();
            e.Handled = true;
            return;
        }

        // Ctrl+C with selection — copy
        if (ctrl && key == Key.C && _renderer.HasSelection)
        {
            _renderer.CopySelectionToClipboard();
            _renderer.ClearSelection();
            e.Handled = true;
            return;
        }

        // Clear selection on any keypress (except modifiers)
        if (key is not (Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt))
        {
            _renderer.ClearSelection();
        }

        // Ctrl+A through Ctrl+Z -> send control character
        if (ctrl && key >= Key.A && key <= Key.Z)
        {
            byte controlByte = (byte)(key - Key.A + 1);
            _session.Write([controlByte]);
            e.Handled = true;
            return;
        }

        // Try VtNetCore key mapping first
        if (VtNetCoreKeyNames.TryGetValue(key, out string? vtKeyName))
        {
            var sequence = _session.Terminal.GetKeySequence(vtKeyName, ctrl, shift);
            if (sequence != null && sequence.Length > 0)
            {
                _session.Write(sequence);
                e.Handled = true;
                return;
            }
        }

        // Fallback: direct escape sequences
        bool appCursorMode = _session.Terminal.CursorState.ApplicationCursorKeysMode;
        if (appCursorMode && AppCursorSequences.TryGetValue(key, out string? appSeq))
        {
            _session.Write(Encoding.UTF8.GetBytes(appSeq));
            e.Handled = true;
            return;
        }

        if (EscapeSequences.TryGetValue(key, out string? escSeq))
        {
            _session.Write(Encoding.UTF8.GetBytes(escSeq));
            e.Handled = true;
            return;
        }

        // Simple keys handled by TextInput
        if (key is Key.Enter)
        {
            _session.Write("\r"u8.ToArray());
            e.Handled = true;
        }
        else if (key is Key.Tab)
        {
            _session.Write("\t"u8.ToArray());
            e.Handled = true;
        }
        else if (key is Key.Escape)
        {
            _session.Write([0x1b]);
            e.Handled = true;
        }
        else if (key is Key.Back)
        {
            _session.Write([0x7f]);
            e.Handled = true;
        }
    }

    public void HandleTextInput(TextCompositionEventArgs e)
    {
        if (!_session.IsConnected || string.IsNullOrEmpty(e.Text))
            return;

        // Don't send control characters that were already handled
        if (e.Text.Length == 1 && e.Text[0] < 0x20)
            return;

        _session.Write(Encoding.UTF8.GetBytes(e.Text));
        e.Handled = true;
    }

    private void PasteClipboard()
    {
        if (Clipboard.ContainsText())
        {
            string text = Clipboard.GetText();
            if (!string.IsNullOrEmpty(text))
            {
                if (_session.Terminal.BracketedPasteMode)
                {
                    _session.Write(Encoding.UTF8.GetBytes("\x1b[200~"));
                    _session.Write(Encoding.UTF8.GetBytes(text));
                    _session.Write(Encoding.UTF8.GetBytes("\x1b[201~"));
                }
                else
                {
                    _session.Write(Encoding.UTF8.GetBytes(text));
                }
            }
        }
    }
}
