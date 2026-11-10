# ComboBox UIA selection reads empty after the first change (Uno 6.7.359, Win32 Skia)

Minimal repro for the upstream issue. Two projects:

- `ComboUiaRepro.App` - an Uno single-project app (`net10.0-desktop`, Win32 host) whose only content is a
  `ComboBox` with 20 string items, `SelectedIndex = 0`, AutomationId `Combo`.
- `UiaClient` - a .NET 10 console UIA client (COM `IUIAutomation`). For each launch it focuses the combo,
  presses Down (first selection change of the launch), reads `SelectionPattern.GetCurrentSelection()` at +1.5 s
  and then every 250 ms until it names the new item, then does the same for a second Down.

## Run (Windows 10/11)

```powershell
dotnet build ComboUiaRepro.App -c Release
dotnet build UiaClient -c Release
.\UiaClient\bin\Release\net10.0-windows\UiaClient.exe .\ComboUiaRepro.App\bin\Release\net10.0-desktop\ComboUiaRepro.App.exe 10
# optional: also subscribe to StructureChanged on the window subtree, as many UIA clients do
.\UiaClient\bin\Release\net10.0-windows\UiaClient.exe .\ComboUiaRepro.App\bin\Release\net10.0-desktop\ComboUiaRepro.App.exe 10 --structure-events
```

Keep the desktop unlocked and do not touch the keyboard while it runs (the client uses `SendInput`).

To compare against the earlier 6.7 build, change `global.json` from `"Uno.Sdk": "6.7.45"` (Uno.WinUI 6.7.359)
to `"Uno.Sdk": "6.7.30"` (Uno.WinUI 6.7.135), delete `bin`/`obj`, rebuild and rerun.

## Expected vs actual

- Expected (6.7.135): every read at +1.5 s names the newly selected item.
- Actual (6.7.359): in some launches the first change's read at +1.5 s returns an EMPTY selection (no error),
  and the item becomes readable 3-6 s after the key press. The second change in the same launch reads at once.
