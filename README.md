# Yellow Rabbit And Penguin Desktop Pets

Windows desktop pet executable containing:

- Emperor Chick
- Yellow Rabbit

Run:

```powershell
.\dist\YellowRabbitAndPenguinPets.exe
```

Build from source:

```powershell
.\build.ps1
```

All penguin and rabbit animation frames are included under `src/Assets`, so the project does not depend on local Codex or hatch-run paths. The generated `build/` and `dist/` folders are ignored by git.

## Controls

- Default: `idle`.
- Drag a pet left/right: `running-left` or `running-right`.
- Single click: `jumping`.
- Double click: `waving`.
- No interaction for a while: `waiting`, then `review`.
- Longer idle moments: occasional `waving`, `jumping`, or `running`.
- Shake quickly while dragging, or release at the screen edge: `failed`.
- Tray icon menu: show/hide pets, manually trigger animations, or exit.
- Ctrl + mouse wheel over a pet: resize it.
- Top-right `X` on each pet: close that pet.
- Closing both pets exits the program automatically.

Default display scale is 65%. Use Ctrl + mouse wheel over a pet if you want it smaller or larger.

## Packaging Notes

The executable is built as a plain .NET Framework WPF app with embedded PNG animation frames. It does not use PyInstaller, obfuscation, network access, auto-start, keyboard hooks, injection, or registry persistence.

No one can guarantee a Windows executable will never be flagged by every antivirus engine. To reduce false positives further for public distribution, code-sign the exe with a trusted certificate and distribute it in a normal installer or zip with a clear publisher name.
