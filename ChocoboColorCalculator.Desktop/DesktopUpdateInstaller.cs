using System.Diagnostics;
using System.IO;

namespace ChocoboColorCalculator.Desktop;

public static class DesktopUpdateInstaller
{
    private const string InstallerScriptName = "install-update.ps1";

    public static void Launch(
        PreparedDesktopUpdate preparedUpdate,
        string currentExecutablePath,
        int parentProcessId)
    {
        var startInfo = CreateStartInfo(preparedUpdate, currentExecutablePath, parentProcessId);
        if (Process.Start(startInfo) is null)
            throw new InvalidOperationException("Windows could not start the update installer.");
    }

    public static ProcessStartInfo CreateStartInfo(
        PreparedDesktopUpdate preparedUpdate,
        string currentExecutablePath,
        int parentProcessId,
        bool skipRelaunch = false)
    {
        currentExecutablePath = Path.GetFullPath(currentExecutablePath);
        if (!string.Equals(
                Path.GetFileName(currentExecutablePath),
                DesktopUpdateService.DesktopExecutableName,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The running desktop executable has an unexpected name.");
        if (!File.Exists(preparedUpdate.ExecutablePath))
            throw new FileNotFoundException("The prepared update executable is missing.", preparedUpdate.ExecutablePath);

        var targetDirectory = Path.GetDirectoryName(currentExecutablePath)
            ?? throw new InvalidOperationException("The application directory could not be determined.");
        var scriptPath = Path.Combine(preparedUpdate.WorkingDirectory, InstallerScriptName);
        File.WriteAllText(scriptPath, InstallerScript);

        var powerShellPath = Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (!File.Exists(powerShellPath))
            powerShellPath = "powershell.exe";

        var startInfo = new ProcessStartInfo
        {
            FileName = powerShellPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = preparedUpdate.WorkingDirectory,
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-ParentProcessId");
        startInfo.ArgumentList.Add(parentProcessId.ToString());
        startInfo.ArgumentList.Add("-SourceDirectory");
        startInfo.ArgumentList.Add(preparedUpdate.PayloadDirectory);
        startInfo.ArgumentList.Add("-TargetDirectory");
        startInfo.ArgumentList.Add(targetDirectory);
        startInfo.ArgumentList.Add("-TargetExecutable");
        startInfo.ArgumentList.Add(currentExecutablePath);
        startInfo.ArgumentList.Add("-WorkingDirectory");
        startInfo.ArgumentList.Add(preparedUpdate.WorkingDirectory);
        if (skipRelaunch)
            startInfo.ArgumentList.Add("-SkipRelaunch");

        if (!CanWriteToDirectory(targetDirectory))
            startInfo.Verb = "runas";
        return startInfo;
    }

    private static bool CanWriteToDirectory(string directory)
    {
        var testPath = Path.Combine(directory, $".chocobo-update-write-test-{Guid.NewGuid():N}");
        try
        {
            using var stream = new FileStream(
                testPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(testPath))
                    File.Delete(testPath);
            }
            catch
            {
                // The elevated installer will handle a protected application directory.
            }
        }
    }

    private const string InstallerScript = """
param(
    [Parameter(Mandatory = $true)][int]$ParentProcessId,
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$TargetDirectory,
    [Parameter(Mandatory = $true)][string]$TargetExecutable,
    [Parameter(Mandatory = $true)][string]$WorkingDirectory,
    [switch]$SkipRelaunch
)

$ErrorActionPreference = 'Stop'
$backupDirectory = Join-Path $TargetDirectory '.chocobo-update-backup'
$logPath = Join-Path $WorkingDirectory 'install.log'
$createdFiles = New-Object 'System.Collections.Generic.List[string]'

try {
    if ($ParentProcessId -gt 0) {
        $parent = Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue
        if ($null -ne $parent -and -not $parent.WaitForExit(60000)) {
            throw 'The application did not close before the update timeout.'
        }
    }

    if (Test-Path -LiteralPath $backupDirectory) {
        Remove-Item -LiteralPath $backupDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null

    $payloadFiles = @(Get-ChildItem -LiteralPath $SourceDirectory -File)
    if ($payloadFiles.Count -eq 0) {
        throw 'The prepared update contains no files.'
    }

    foreach ($file in $payloadFiles) {
        $targetPath = Join-Path $TargetDirectory $file.Name
        $newPath = $targetPath + '.update-new'
        $backupPath = Join-Path $backupDirectory $file.Name
        Copy-Item -LiteralPath $file.FullName -Destination $newPath -Force
        if (Test-Path -LiteralPath $targetPath) {
            [System.IO.File]::Replace($newPath, $targetPath, $backupPath, $true)
        }
        else {
            Move-Item -LiteralPath $newPath -Destination $targetPath -Force
            $createdFiles.Add($targetPath)
        }
    }

    Set-Content -LiteralPath $logPath -Value 'success' -Encoding UTF8
    if (-not $SkipRelaunch) {
        Start-Process -FilePath $TargetExecutable -WorkingDirectory $TargetDirectory
    }
    Remove-Item -LiteralPath $backupDirectory -Recurse -Force -ErrorAction SilentlyContinue
    exit 0
}
catch {
    $message = $_.Exception.ToString()
    try {
        if (Test-Path -LiteralPath $backupDirectory) {
            foreach ($backup in Get-ChildItem -LiteralPath $backupDirectory -File) {
                $targetPath = Join-Path $TargetDirectory $backup.Name
                Copy-Item -LiteralPath $backup.FullName -Destination $targetPath -Force
            }
        }
        foreach ($createdFile in $createdFiles) {
            Remove-Item -LiteralPath $createdFile -Force -ErrorAction SilentlyContinue
        }
        Get-ChildItem -LiteralPath $TargetDirectory -Filter '*.update-new' -File -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
        Set-Content -LiteralPath $logPath -Value ('failed: ' + $message) -Encoding UTF8
        if (-not $SkipRelaunch -and (Test-Path -LiteralPath $TargetExecutable)) {
            Start-Process -FilePath $TargetExecutable -WorkingDirectory $TargetDirectory
        }
    }
    catch {
        Set-Content -LiteralPath $logPath -Value ('rollback failed: ' + $_.Exception.ToString()) -Encoding UTF8
    }
    exit 1
}
""";
}
