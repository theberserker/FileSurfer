# CLAUDE.md

Guidance for working in this repo. FileSurfer is a cross-platform (Windows + Linux)
desktop file manager built with **Avalonia UI 11** + **ReactiveUI** on **.NET 10**,
using the MVVM pattern.

## Commands

All projects live under `src/`. The solution is `src/FileSurfer.sln`.

```bash
# Build everything
dotnet build src/FileSurfer.sln

# Run the app (pick the host platform's project)
dotnet run --project src/FileSurfer.Windows
dotnet run --project src/FileSurfer.Linux

# Test — whole suite, or one project
dotnet test src/FileSurfer.sln
dotnet test src/tests/Tests.Core
dotnet test src/tests/Tests.Windows        # net10.0-windows, Windows only
dotnet test src/tests/Tests.Linux

# Release build of the Windows executable
dotnet publish src/FileSurfer.Windows/FileSurfer.Windows.csproj -c Release
# -> src/FileSurfer.Windows/bin/Release/net10.0-windows/publish/FileSurfer.exe
```

Plain `dotnet` is enough on both platforms — no VS Build Tools / `msbuild.exe` needed
(see "Windows COM" below for why this matters).

A single command-line arg is accepted: the directory to open in.

### Runtime dependencies (Linux only)
`trash-cli`, `resvg`, `xdg-mime` must be on `PATH` for trash, SVG icons, and mimetype
resolution respectively.

## Project layout

| Project              | Role |
|----------------------|------|
| `FileSurfer.Core`    | Almost everything: Models, Services, ViewModels, Views (AXAML). No reference to the platform projects. |
| `FileSurfer.Windows` | Thin. `Main` entry point + Windows implementations of platform interfaces. Produces `FileSurfer.exe`. `net10.0-windows`, WinForms enabled. |
| `FileSurfer.Linux`   | Thin. `Main` entry point + Linux implementations. Produces `FileSurfer`. |
| `tests/Mocks`        | Shared mock implementations of the service interfaces, used by the test projects. |
| `tests/Tests.Core` / `Tests.Linux` / `Tests.Windows` | xUnit tests. |

Namespaces in the platform projects mirror `FileSurfer.Core`'s, holding only the
platform-specific classes.

`FileSurfer.Core/Models` + `FileSurfer.Core/Services` together are the MVVM "Model"
layer — Models = data representation/retrieval, Services = behavior over that data.

## Architecture — the load-bearing abstractions

- **`IPlatformBootstrap`** (`Core/App.axaml.cs`) decouples shared code from platform
  specifics. Each `Program.Main` sets `App.Bootstrap = new {Windows,Linux}PlatformBootstrap()`
  before Avalonia starts. The bootstrap builds `MainWindowViewModel`, composing a
  `LocalFileSystem` from platform service implementations, and supplies the default
  settings provider. **This is where platform wiring happens** — start here to trace
  how a platform service reaches the shared code.

- **`IFileSystem`** (`Core/Models/IFileSystem.cs`) is the central facade: file info,
  icons, archives, file I/O, trash, properties, shell, Git.
  - `LocalFileSystem` — `sealed`, composed via `required init` properties from the bootstrap.
  - `SftpFileSystem` — remote over SSH.NET; capabilities it can't support (archives,
    trash, Git) use stub implementations (`Core/Services/Sftp/SftpStubs.cs`).

- **`Location`** (`Core/Models/Location.cs`) = a directory path + the `IFileSystem` it
  belongs to. ViewModels operate on `Location`, so local and SFTP dirs are uniform.

- **`IPathTools`** — `LocalPathTools` (native) vs `RemoteUnixPathTools` (POSIX/SFTP).
  Never hand-roll path string manipulation; go through these.

- **`IResult` / `SimpleResult` / `ValueResult<T>`** (`Core/Models/IResult.cs`) —
  expected failures are returned, not thrown. Library exceptions are caught and
  converted to `IResult` at the lowest level. Programmer-error exceptions
  (null, invalid arg) propagate normally.

- **File operations** use the command pattern (`Core/Services/FileOperations/`):
  `FileOperation` (execution pipeline: iteration, progress, cancellation) →
  `UndoableFileOperation` (+ inverse). `UndoRedoHandler<T>` holds the history.
  Delete is non-undoable.

- **Clipboard**: `IClipboardManager` reconciles an internal clipboard with the OS
  clipboard via `IOsClipboardProxy` (`AvaloniaClipboardProxy`), tracking cut-vs-copy
  and source file system that the OS clipboard can't represent.

- **Git**: `IGitIntegration` → `LocalGitIntegration` backed by LibGit2Sharp. Local
  repos only.

- **MVVM plumbing**: `ViewLocator` maps a ViewModel to its View by naming convention.
  `MainWindowViewModel` is `sealed partial` — state/init in
  `ViewModels/MainWindowViewModel.cs`, command implementations in
  `ViewModels/MWVMCommandImpl.cs`.

- **Path bar**: `Views/Helpers/BreadcrumbBar.cs` is a custom `Panel` (measure/arrange +
  "…" overflow menu) that renders `MainWindowViewModel.PathSegments`
  (`Models/PathSegment` list, rebuilt in the `CurrentLocation` setter via
  `IPathTools.ToPathSegments`). Clicking a segment / the editable box (Ctrl+L, Alt+D,
  or clicking the empty strip) both route to `SetNewLocationCommand`. The editable
  `TextBox` and the "Searching…" text are sibling controls; `MainWindow.RefreshPathArea`
  toggles which of the three is visible.

- **Settings**: `FileSurferSettings` (static) reads/writes
  `%AppData%/FileSurfer/settings.json` and `sftp-connections.json`. Defaults come
  from the platform's `IDefaultSettingsProvider`. `SettingsRecord` is the JSON shape.

## Conventions

- Code style is enforced by `src/.editorconfig` (note: it lives in `src/`, not the
  repo root). Highlights: file-scoped namespaces, expression-bodied members where
  possible, `using` outside namespace, CRLF, 4-space indent, wrapped binary operators
  go at the **start** of the continuation line. Formatting matches CSharpier defaults.
- Interfaces are `I`-prefixed; the platform implementation is `Windows*` / `Linux*`
  (e.g. `IFileIoHandler` → `WindowsFileIoHandler` / `LinuxFileIoHandler`).
- XML doc comments on public members are the norm here — match that.
- Tests are xUnit (`[Fact]` / `[Theory]`), using `tests/Mocks` for dependencies.

## Windows COM — do not regress

`FileSurfer.Windows` talks to the Windows shell (`Shell.Application`) and Windows
Script Host (`WScript.Shell`) via **late-bound COM** (`Type.GetTypeFromProgID` +
`Activator.CreateInstance` + `dynamic`), in:
- `Services/Shell/WindowsBinInteraction.cs` (restore from Recycle Bin)
- `Services/Shell/WindowsShellHandler.cs` (`.lnk` creation)
- `Models/FileInformation/WindowsFileInfoProvider.cs` (`.lnk` target resolution)

Do **not** reintroduce `<COMReference Include="Shell32" />` / `IWshRuntimeLibrary`
in the csproj. Those need the `ResolveComReference` task, which only .NET Framework
`msbuild.exe` has — `dotnet build` fails with `MSB4803`, and VS fails with a missing
`FileSurfer.dll` metadata error. `<BuiltInComInteropSupport>` in the csproj is what
the `dynamic` COM dispatch relies on; keep it.

COM objects are released with `Marshal.FinalReleaseComObject` in a `finally`. Shell
calls run on an STA thread via `StaWorkerSync`.

## Docs

- `README.md` — features, install, build-from-source, dependency list.
- `docs/Documentation/Overview.md` — architecture orientation (the source for much of
  the above; read it for more detail).
- `docs/UserGuide/UserGuide.md` — end-user feature guide.
- No CI workflows in this repo.
