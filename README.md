# KaSe_soft – Desktop Configuration Tool for the KaSe Keyboard

KaSe_soft is the desktop configuration and management tool for the **KaSe** custom mechanical keyboard.
It provides a graphical interface to visualize the layout, edit keymaps and layers, and push the configuration
to the keyboard over USB (CDC ACM). This README has been updated with practical build/run instructions and debugging tips.

![KaSe_soft main window](capture.jpg)

---

## Résumé / Summary

KaSe_soft permet :
- visualiser le layout du clavier KaSe,
- éditer les keymaps / layers et les envoyer au clavier sur CDC ACM,
- gérer plusieurs dispositions de clavier (layout) côté UI,
- debugger et dépanner les problèmes de build et d'exécution.

---

## Build & Run (debug and creating an executable)

Notes importantes :
- Le projet cible `net10.0`. Selon la configuration du SDK/IDE, l'artefact produit par `dotnet build` peut être seulement une DLL (build framework-dependent).
- Pour obtenir un exécutable natif (apphost) ou une publication autonome, il faut fournir un Runtime Identifier (RID) ou publier en self-contained.

Common commands (Linux, example `linux-x64`):

- Restore and build (framework-dependent):

```bash
dotnet restore
dotnet build
dotnet run
```

- Build for a RID to get an apphost (executable) next to the DLL:

```bash
# Debug build with RID -> apphost executable is generated in bin/.../linux-x64
dotnet build -r linux-x64 -c Debug
```

- Publish a single self-contained executable (slow, larger output):

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
# result in bin/Release/net10.0/linux-x64/publish/
```

If Rider generates only the DLL and not the executable, check that either:
- your project has a `<RuntimeIdentifier>linux-x64</RuntimeIdentifier>` (or similar) in the `.csproj` or
- you pass the `-r` argument to `dotnet build` / `dotnet publish` as shown above.

Known NuGet / SDK errors you may encounter:
- NU1101 Unable to find package `Microsoft.NETCore.App.Host.arch-x64`: this happens when the SDK / restore expects host runtime packs that are not available in your NuGet sources. Solution:
  - prefer `dotnet build -r linux-x64` which uses local runtime packs under `/usr/share/dotnet/packs` when targeting an installed runtime, or
  - install the appropriate runtime packs for the SDK, or
  - remove an explicit invalid `<RuntimeIdentifier>` from the csproj if it points to a non-existing pack.

---

## Keyboard layouts & KeyConverter

The UI supports different keyboard layouts (example: `qwerty`, `azerty`, `qwertz`). The `KeyConverter` class is responsible for turning a `K_Keys` enumeration value into a user-friendly label for `Keycap` controls.

Design notes and recommendations:
- Keep a per-layout mapping table instead of trying to do a single two-way map that swaps keys in place. A common bug is doing a naive swap where `Q -> A` and `A -> Q` are both applied in a single pass; depending on iteration order you end up with all `Q` or all `A`.
  - Correct approach: build a mapping that maps from logical HID key code to the label to show for the selected layout (lookup table from source HID -> display string). Do not mutate the source key codes in place; compute the label only.
- Example strategy:
  - KeyConverter holds a Dictionary<Layout, Dictionary<K_Keys, string>> or a function that returns the label for a given `K_Keys` and current layout.
  - When the layout changes (ComboBox selection), raise PropertyChanged for the property used by the `Keycap` binding or force a refresh of the keycap visuals so the converter is re-run.

Refreshing the keycap display when layout changes:
- In `MainWindow`, the ComboBox for layout selection should be bound to a `SelectedLayout` property (TwoWay). When that property changes, notify listeners (INotifyPropertyChanged).
- Options to refresh visuals:
  - Rebuild the keyboard grid (`KeyboardGrid.Children.Clear()` and call the renderer again) after the layout change.
  - Or, have `Keycap` listen to a global `CurrentLayout` property and re-evaluate the binding (trigger `PropertyChanged` for the value used by the converter).

Avoiding the "swap loop" bug (Q <-> A):
- Never perform an in-place two-way swap by iterating over the same collection and writing results back to it using the original values as keys. Always read from the original map and write to a new result map.

---

## Combobox for layout selection (UI)

A ComboBox was added to the main UI to pick the keyboard layout (e.g. `qwerty`, `azerty`, `qwertz`). Ensure the data bindings are correct in `MainWindow.axaml`:

- ItemsSource should point to the list of available layouts (string or enum collection).
- SelectedItem should bind to `SelectedLayout` on the window / view model.

When the selection changes, either:
- rebuild the keyboard UI (to re-run KeyConverter per key), or
- raise a PropertyChanged event that causes each `Keycap` to re-evaluate its displayed label.

---

## CDC logs and terminal view

The UI exposes a "Terminal" tab that displays incoming serial messages. To redirect logs to the CDC terminal view:
- Ensure `SerialPortManager` fires a `RawDataReceived` or similar event with incoming data.
- Subscribe to that event from `MainWindow` and append text to the `TerminalOutput` TextBlock (make sure to marshal to the UI thread when updating UI elements).

Example pattern in code (UI thread marshaling):

```csharp
// pseudo-code
_serialPortManager.RawDataReceived += (s, bytes) =>
{
    Dispatcher.UIThread.Post(() => TerminalOutput.Text += Encoding.UTF8.GetString(bytes) + "\n");
};
```

---

## Troubleshooting: app blocks on startup (SettingsManager.LoadAsync)

You reported the program is blocked on this call in `MainWindow`:

```csharp
SettingsManager.LoadAsync().GetAwaiter().GetResult();
```

Root cause:
- Calling `GetResult()` / `.Result` or `.GetAwaiter().GetResult()` on an async method from the UI thread can deadlock if that async method captures the synchronization context and awaits something that needs the UI thread to finish.

Recommended fixes (pick one):
1) Make the caller asynchronous and `await` the call from an async event handler. For example, instead of calling `GetResult()` in the constructor or synchronously on load, use an async loaded handler:

```csharp
private async void Control_OnLoaded(object? sender, RoutedEventArgs e)
{
    await SettingsManager.LoadAsync();
    // follow-up initialization
}
```

2) If you must run sync, run the load on a background thread and then marshal any UI updates back to the UI thread, e.g.:

```csharp
Task.Run(async () =>
{
    await SettingsManager.LoadAsync();
    Dispatcher.UIThread.Post(() => { /* update UI */ });
}).Wait();
```

3) Change `LoadAsync` implementation to avoid capturing the UI context (use `ConfigureAwait(false)` on awaited calls inside `LoadAsync`) so the method does not require the UI thread to continue. This is a less invasive workaround but depends on the internals of `LoadAsync`.

Why the deadlock happens:
- The UI thread synchronously blocks waiting for `LoadAsync` to complete. `LoadAsync` uses awaits that by default try to resume on the captured context (UI thread). That resume cannot run because the UI thread is blocked — deadlock.

Actionable recommendation: change the load call in `MainWindow` to an `await` from an async loaded handler. This is the simplest and safest approach.

---

## Common runtime/debug tips

- If Rider debugs but can't find PDBs for framework assemblies, that's normal for system/shared assemblies; IDEs show messages like "Pdb file was not found or failed to read" for framework packages.
- If build sometimes does not produce an `apphost` executable:
  - ensure you pass `-r linux-x64` to build/publish, or add a `<RuntimeIdentifier>` in the `.csproj`.
  - check that `PublishSingleFile` and `SelfContained` settings aren't interfering with what you expect.

---

## Known issues and quick fixes

- Duplicate key in `KeyConverter` initialization (exception: "An item with the same key has already been added"):
  - Use `dictionary.TryAdd(key, value)` or check `ContainsKey` before `Add`, or deduplicate the source data.

- `MainWindow.axaml` binding errors such as "Unable to resolve property or method of name 'Layouts' on type 'Avalonia.Controls.Window'" mean you bound to `ElementName=main` expecting properties on the code-behind. Make sure `MainWindow` defines public properties `Layouts`, `SelectedLayout`, etc., or use a view model as DataContext.

---

## How to contribute / next steps

If you'd like, I can:
- add an example implementation of per-layout mapping in `KeyConverter` and unit tests to validate swaps (avoid Q↔A bug),
- change the synchronous `LoadAsync().GetAwaiter().GetResult()` call into an async `await` in `Control_OnLoaded`, and update `MainWindow` code accordingly,
- add an explicit `RuntimeIdentifier` to the csproj or update the README with a step-by-step fix for Rider's build settings.

Tell me which of these you want me to do next and I will apply small, safe changes and validate via a build.

---

## License

See the `LICENSE` file in this repository.
