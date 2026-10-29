using System.Windows;
using System.Windows.Input;
using System.IO;
using Microsoft.Win32;
using SJHotkeys.Models;

namespace SJHotkeys.Dialogs;

public partial class HotkeyDialog : Window
{
    private readonly Guid? _editingId;
    public string ProgramPath => ProgramPathBox.Text.Trim();
    public uint VirtualKey { get; private set; }
    public uint Modifiers { get; private set; }
    public HotkeyDialog(HotkeyEntry? existing = null)
    {
        InitializeComponent();
        if (existing is not null)
        {
            _editingId = existing.Id;
            Title = "Edit Hotkey";
            Heading.Text = "Edit Hotkey";
            ProgramPathBox.Text = existing.ProgramPath;
            VirtualKey = existing.VirtualKey;
            Modifiers = existing.Modifiers;
            ShortcutBox.Text = ShortcutFormatter.Format(Modifiers, VirtualKey);
        }
    }
    public Guid? EditingId => _editingId;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Choose a program", Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*", CheckFileExists = true };
        if (picker.ShowDialog(this) == true) ProgramPathBox.Text = picker.FileName;
    }
    private void ShortcutBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => ShortcutBox.Text = "Press a shortcut…";
    private void ShortcutBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (VirtualKey != 0) ShortcutBox.Text = ShortcutFormatter.Format(Modifiers, VirtualKey);
        else ShortcutBox.Text = "Click and press a shortcut";
    }
    private void ShortcutBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifier = Keyboard.Modifiers;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            ShortcutBox.Text = "Press a shortcut…";
            return;
        }
        var mods = 0u;
        if (modifier.HasFlag(ModifierKeys.Control)) mods |= 2;
        if (modifier.HasFlag(ModifierKeys.Alt)) mods |= 1;
        if (modifier.HasFlag(ModifierKeys.Shift)) mods |= 4;
        if (modifier.HasFlag(ModifierKeys.Windows)) mods |= 8;
        if (mods == 0) { Error("Add Ctrl, Alt, Shift or Win to your shortcut."); return; }
        Modifiers = mods;
        VirtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        ShortcutBox.Text = ShortcutFormatter.Format(Modifiers, VirtualKey);
        ErrorText.Visibility = Visibility.Collapsed;
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProgramPath) || !File.Exists(ProgramPath)) { Error("Choose a valid program file first."); return; }
        if (VirtualKey == 0 || Modifiers == 0) { Error("Set a valid shortcut first."); return; }
        DialogResult = true;
    }
    private void Error(string message) { ErrorText.Text = message; ErrorText.Visibility = Visibility.Visible; }
}
