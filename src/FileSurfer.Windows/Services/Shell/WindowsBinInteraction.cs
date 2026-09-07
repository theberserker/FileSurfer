using System;
using System.Runtime.InteropServices;
using FileSurfer.Core.Models;
using FileSurfer.Core.Services.Shell;
using Microsoft.VisualBasic.FileIO;

namespace FileSurfer.Windows.Services.Shell;

/// <summary>
/// Interacts with the Windows shell (<c>Shell.Application</c>) via late-bound COM
/// in order to restore files and directories from the system trash.
/// </summary>
public class WindowsBinInteraction : IBinInteraction
{
    private const int BinFolderId = 10;
    private const string DeletedFromProperty = "System.Recycle.DeletedFrom";
    private const string RestoreVerb = "ESTORE";

    private readonly StaWorkerSync _workerSync = new("Bin worker thread");

    public IResult RestoreFile(string originalFilePath) =>
        _workerSync.Invoke(() => RestoreInternal(originalFilePath));

    public IResult RestoreDir(string originalDirPath) =>
        _workerSync.Invoke(() => RestoreInternal(originalDirPath));

    private static SimpleResult RestoreInternal(string originalPath)
    {
        Type? shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
            return SimpleResult.Error("The Windows shell (Shell.Application) is not available.");

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic bin = shell.NameSpace(BinFolderId);
        SimpleResult result = SimpleResult.Error($"Entry: \"{originalPath}\" not found.");
        try
        {
            foreach (dynamic item in bin.Items())
            {
                if (
                    TryGetOriginalPath(item, out string? itemOriginalPath)
                    && LocalPathTools.PathsAreEqual(itemOriginalPath, originalPath)
                )
                {
                    DoVerb(item, RestoreVerb);
                    result = SimpleResult.Ok();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            result = SimpleResult.Error(ex.Message);
        }
        Marshal.FinalReleaseComObject(bin);
        Marshal.FinalReleaseComObject(shell);

        return result;
    }

    private static bool TryGetOriginalPath(dynamic item, out string? originalPath)
    {
        originalPath = null;
        try
        {
            string? deletedFrom = item.ExtendedProperty(DeletedFromProperty) as string;
            string? name = item.Name as string;
            if (string.IsNullOrWhiteSpace(deletedFrom) || string.IsNullOrWhiteSpace(name))
                return false;

            originalPath = LocalPathTools.NormalizePath(
                LocalPathTools.Combine(deletedFrom, name)
            );
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DoVerb(dynamic item, string verb)
    {
        foreach (dynamic verbObject in item.Verbs())
        {
            string? verbName = verbObject.Name as string;
            if (
                verbName is not null
                && verbName.Contains(verb, StringComparison.CurrentCultureIgnoreCase)
            )
            {
                verbObject.DoIt();
                return;
            }
        }
    }

    public IResult MoveFileToTrash(string filePath)
    {
        try
        {
            FileSystem.DeleteFile(
                filePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public IResult MoveDirToTrash(string dirPath)
    {
        try
        {
            FileSystem.DeleteDirectory(
                dirPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException
            );
            return SimpleResult.Ok();
        }
        catch (Exception ex)
        {
            return SimpleResult.Error(ex.Message);
        }
    }

    public void Dispose() => _workerSync.Dispose();
}
