# SJ Hotkeys v1.1.0

This release fixes a startup crash that prevented the previous Windows executable from opening its main window.

## Highlights

- Fixed WPF logo resource URIs that caused an unhandled `XamlParseException` before the first window appeared.
- Fixed second-launch activation using a compatible, one-way named-pipe message. A second launch now raises the existing window instead of silently exiting.
- Added a local, rotating startup error log at `%LOCALAPPDATA%\SJ Studios\SJ Hotkeys\logs\startup.log` and a clear error dialog for startup failures.
- Fixed `Launch Minimized` so the app initializes and hides to the tray without leaving an unshown window.
- Enabled Windows autostart on a new installation only. Existing saved preferences are preserved, and users can turn autostart off in Settings.
- Reduced the embedded logo from about 660 KB to about 32 KB and disabled horizontal scrolling in the shortcut list.
- Updated README previews to Windows-style illustrations, clearly labeled as illustrations rather than runtime screenshots.

## Downloads

- **Portable:** self-contained, no .NET Desktop Runtime required. Compressed single-file x64 executable.
- **Lightweight:** smaller framework-dependent x64 executable. Requires the .NET 9 Desktop Runtime.

Both variants target Windows 10 and Windows 11 x64.
