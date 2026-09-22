# Mark of Tiger Save Patcher

A minimal cross-platform tool for **Heroes of Might and Magic IV** that fixes a soft-lock on the campaign map **Znak Tygrysa** / **Mark of the Tiger**.

## The bug

On *Mark of the Tiger*, if any of the troll armies **flees from battle**, the Orc Tower script can break. The gate condition then fails and the player is soft-locked, unable to open the gate and continue the map.

## The patch

The save file stores campaign script logic as a gzipped binary blob. On this map, the Orc Gate check uses a script token **`and`** where **`or`** is needed.

This tool applies one surgical change in the decompressed save payload:

| | Bytes at offset `0xB6D03` | Meaning |
|---|---------------------------|---------|
| Before | `03 00 61 6E 64` | length-prefixed `"and"` |
| After | `02 00 6F 72` | length-prefixed `"or"` |

Script context:

```text
...seq....and..has_hero......Elwin..is_eliminated...
          ↓
...seq....or..has_hero......Elwin..is_eliminated...
```

That lets Elwin open the Orc Gate regardless if the trolls are present or not.

## What the app does

1. Prints a short explanation of the bug and fix
2. Opens the **native OS file picker** for your `.h4s` save (or prompts for a path if cancelled)
3. Creates a **timestamped backup** in the same folder: `{name}_{yyyyMMdd_HHmmss}.h4s.bak`
4. Decompresses the save, applies the patch, and writes a sibling file: `{name}_patched.h4s`
5. Leaves your original save untouched

If the save is **already patched**, the app exits safely with no backup and no new output file.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build/run from source, or the .NET 10 runtime for a published binary
- Works on **Windows, macOS, and Linux**
- Uses the OS native file picker via [NativeFileDialogNET](https://www.nuget.org/packages/NativeFileDialogNET) (not WinForms/WPF)
- Falls back to typing a path if the picker is unavailable or cancelled

**Linux note:** the native picker needs GTK3 or an xdg-desktop-portal file dialog (standard on most desktop distros).

## Run from source

From the repo root:

```bash
dotnet run
```

With a save path:

```bash
dotnet run -- "fixtures/saves/m2 - Copy.h4s"
```

Windows PowerShell:

```powershell
dotnet run -- "fixtures\saves\m2 - Copy.h4s"
```

## Build a standalone binary

Framework-dependent (requires .NET 10 runtime):

```bash
dotnet publish -c Release
```

Self-contained (no separate runtime required):

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained true

# macOS (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained true

# Linux
dotnet publish -c Release -r linux-x64 --self-contained true
```

Output is under `bin/Release/net10.0/<rid>/publish/`.

## Using the patched save

1. Run the patcher and provide your broken `.h4s` save
2. Load `{YourSave}_patched.h4s` in Heroes IV
3. Keep the timestamped `.h4s.bak` backup until you confirm the fix worked

## Safety

- Original save is never overwritten
- Backup is always created before patching an unpatched save
- Already-patched saves are detected and skipped
- Wrong saves (missing the expected `and` token) are rejected before any backup

## Project layout

```text
HOMM4/
  MarkOfTigerPatcher.csproj
  MarkOfTigerPatcher.slnx
  README.md
  src/
    Program.cs
    MarkOfTigerSavePatcher.cs
    H4SaveHelpers.cs
    Interaction.cs
  fixtures/saves/
```
