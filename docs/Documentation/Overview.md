# Overview

FileSurfer is a cross-platform file manager built with Avalonia UI and ReactiveUI,
targeting Windows and Linux on .NET 8.0. This page gives a high-level orientation
to the codebase. For member-level detail, browse the generated class list and
namespace index; this page focuses on how the pieces fit together.

## Project Structure

The solution `FileSurfer.sln` consists of three projects: one shared library
containing most of the application logic, and two thin platform-specific
executable projects.

| Project              | Responsibility                                                                 |
|-----------------------|---------------------------------------------------------------------------------|
| `FileSurfer.Core`     | Platform-independent logic: business logic, ViewModels, and Views             |
| `FileSurfer.Linux`    | Linux-specific functionality and the application entry point (`Main`)         |
| `FileSurfer.Windows`  | Windows-specific functionality and the application entry point (`Main`)       |

Both platform projects reference `FileSurfer.Core` and produce an executable
named `FileSurfer`. `FileSurfer.Core` has no dependency on either platform
project — all platform selection is resolved through a single interface,
`IPlatformBootstrap` (see [Central Abstractions](#central-abstractions)).

## Namespace Overview

Namespaces in `FileSurfer.Linux` and `FileSurfer.Windows` mirror those of
`FileSurfer.Core`, containing only the classes required by the respective
platform. The namespaces below belong to `FileSurfer.Core`.

- **(root)** — Avalonia application entry point, platform bootstrap contract,
  and settings loading/persistence (JSON).
- **Assets** — Generic icons, application logo, bundled fonts.
- **Extensions** — Generic, non-domain-specific C# extension methods.
- **Models** — Data representation and retrieval. Defines the central
  `IFileSystem` and `IResult` abstractions.
- **Services** — Behavior that operates on Model data: file system
  modification, version control, and other external integrations.
- **ViewModels** — Mediates between Models/Services and Views. `MainWindowViewModel`
  is the central point of the application, holding the current Location,
  displayed entries, navigation history, and command handling.
- **Views** — Avalonia windows and dialogs, written in AXAML with C# code-behind.

Models and Services together form the Model layer of MVVM, split to separate
data representation from behavior.

## Central Abstractions {#central-abstractions}

### Platform Bootstrap
`IPlatformBootstrap` decouples the shared logic from platform specifics. Each
platform project implements it and assigns an instance to the static
`App.Bootstrap` before Avalonia startup. It builds the main ViewModel and
supplies a platform-specific default settings provider. Platform services are
then injected into shared classes through interfaces such as
`IFileIoHandler` (implemented by `LinuxFileIoHandler` / `WindowsFileIoHandler`).

### File System Abstraction
`IFileSystem` is the central abstraction for file system interaction. It is a
facade combining file information retrieval, icon provision, archive
management, file operations, trash handling, file properties, shell command
execution, and Git integration.

- `LocalFileSystem` — composes platform-specific providers from bootstrapping.
- `SftpFileSystem` — remote access via SSH.NET; unsupported capabilities
  (e.g. archive management, trash) fall back to stub implementations.

### Location and Path Handling
A `Location` pairs a directory path with the `IFileSystem` instance it
belongs to, letting ViewModels treat local and remote directories uniformly.
Path manipulation is abstracted behind `IPathTools`, with `LocalPathTools`
for native paths and `RemoteUnixPathTools` for POSIX-style SFTP paths.

### Error Handling
Expected failure conditions are represented via `IResult` (success flag plus
error messages) rather than exceptions, with `ValueResult<T>` additionally
carrying a return value on success. Exceptions from underlying libraries are
caught and converted to `IResult` at the lowest implementation levels;
exceptions from programmer errors (null references, invalid arguments)
propagate normally.

## Core Subsystems

### File Operations
Implemented with the command pattern. `FileOperation` provides a shared
execution pipeline (iteration over targeted entries, progress reporting,
cancellation); `UndoableFileOperation` extends it with an inverse execution
method. Concrete undoable operations include move, copy, duplicate, move to
trash, batch rename, flatten directory, and create file/directory. Deletion
is a plain (non-undoable) `FileOperation`. `UndoRedoHandler<T>` stores the
history of executed undoable operations.

### Clipboard Interaction
`IClipboardManager` (`ClipboardManager`) coordinates an internal clipboard
and the OS clipboard, tracking metadata the OS clipboard cannot represent
(source file system, copy vs. cut). It depends on `IOsClipboardProxy`
(`AvaloniaClipboardProxy`) to stay synchronized with the OS clipboard when
entries originate from the local file system. On paste, if OS clipboard
content no longer matches what was last stored, the source is assumed
external and the operation defaults to copy.

### SFTP Integration
Built on SSH.NET's SSH/SFTP client classes. `SftpConnection` models
connection parameters and credentials; `SftpFileSystemFactory` constructs
the corresponding `SftpFileSystem`. Separate classes handle file information
retrieval, file transfer, and file property access over SFTP, plus a shell
handler for commands over the SSH channel. `LocalToSftpSynchronizer` handles
directory synchronization between a local and a remote location.

### Version Control Integration
`IGitIntegration` (`LocalGitIntegration`, backed by LibGit2Sharp) detects
whether a local location belongs to a repository and exposes entry/directory
status and ahead/behind counts relative to the tracked remote branch. It
supports staging/unstaging, committing, branch switching, and
fetch/pull/push. Only local repositories are supported; SFTP-hosted
repositories use a stub `IGitIntegration` implementation.

## MVVM Implementation

`MainWindowViewModel` holds the primary application state (current Location,
displayed entries, navigation history) and exposes it via ReactiveUI's
`ReactiveObject`/`IObservable` mechanisms. Its command handling logic lives
in a separate partial class file, split from initialization and property
declarations. Additional ViewModels cover the properties window, settings
window, individual SFTP connections, the directory synchronizer, individual
file system entries, and sidebar entries. `ViewLocator` resolves the View
for a given ViewModel, avoiding explicit wiring between the two layers.
