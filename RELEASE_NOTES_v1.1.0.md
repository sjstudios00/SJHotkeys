# SJ Hotkeys v1.1.0

This release fixes a startup crash that prevented the previous Windows executable from opening its main window.

## Highlights

- Fixed WPF logo resource URIs that caused an unhandled `XamlParseException` before the first window appeared.
- Fixed the close-window exception by routing tray exit, window close, and shutdown through one idempotent cleanup path.
- Fixed second-launch activation using a compatible, one-way named-pipe message. A second launch now raises the existing window instead of silently exiting.
- Added a local, rotating startup error log at `%LOCALAPPDATA%\SJ Studios\SJ Hotkeys\logs\startup.log` and a clear error dialog for startup failures.
- Fixed `Launch Minimized` so the app initializes and hides to the tray without leaving an unshown window.
- Enabled Windows autostart on a new installation only. Existing saved preferences are preserved, and users can turn autostart off in Settings.
- Replaced the window icon with a proper multi-resolution SJ Studios ICO and used the original SJ Studios logo in About.
- Kept program paths out of the shortcut cards. Program names, hotkeys, and inline Edit/Delete actions remain visible without horizontal scrolling; the full path is still available by hovering the card.
- Added a once-per-day, non-blocking GitHub release check and an in-app updater. Update files are SHA-256 checked before the app closes, updates, and restarts.
- Replaced the README's main-window illustration with a real screenshot captured from the running Windows app. Removed the non-real settings illustration.

## Downloads

- **Portable:** self-contained, no .NET Desktop Runtime required. Compressed single-file x64 executable.
- **Lightweight:** smaller framework-dependent x64 executable. Requires the .NET 9 Desktop Runtime.

Both variants target Windows 10 and Windows 11 x64.
