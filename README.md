# Auto Typer

A tiny Windows app (about 45 KB, no install, no dependencies) that types a block of text into any window as if it were typed on the keyboard, at a speed you control.

## Features

- Types any text: any language (including CJK without an IME), emoji, newlines and tabs
- Adjustable speed (10–3000 characters per minute), changeable while typing
- Random variation in the delay between keys, for a more natural rhythm
- Extra pause after punctuation and newlines
- Countdown before typing starts, so you can switch to the target window
- Global hotkeys: **F9** start / pause / resume, **F10** stop
- Optional Shift+Enter for newlines, so chat apps don't send each line
- Pauses automatically if Auto Typer itself has focus, so it never types into its own window
- Highlights the part already typed, with a progress bar and time left
- Clean dark interface that stays sharp at any display scale; English or 中文
- Always-on-top option; settings are remembered in `%APPDATA%\AutoTyper\settings.ini`

## Download

Get `AutoTyper.exe` from the [latest release](../../releases/latest) and run it. It needs the .NET Framework 4.x, which comes with Windows 10 and 11.

## Usage

1. Paste or type your text into the box.
2. Set the speed and other options.
3. Press **Start** or **F9**, then switch to the target window and click where the text should go before the countdown ends.
4. Press **F9** to pause or resume, **F10** to stop.

## Notes

- Windows blocks simulated input into programs running as administrator unless Auto Typer also runs as administrator.
- Editors with auto-closing brackets or auto-indent may add extra characters, because they react to the typed keys as usual.

## Building from source

Run `build.bat`. It compiles `AutoTyper.cs` (twice: the first build draws the icon, the second embeds it) with the C# compiler that ships with Windows (`csc.exe` from .NET Framework 4.x), so nothing else needs to be installed.

## License

[MIT](LICENSE)
