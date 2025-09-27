using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using TableCloth3.Launcher.ViewModels;
using TableCloth3.Shared.Services;

namespace TableCloth3.Launcher.Services;

public sealed class WindowsSandboxComposer
{
    public WindowsSandboxComposer(
        LocationService locationService)
        : base()
    {
        _locationService = locationService;
    }

    private readonly LocationService _locationService = default!;

    private KeyValuePair<string, XElement>? CreateHostFolderMappingElement(string hostFolderPath, string? sandboxFolder = default, bool? readOnly = default)
    {
        if (!Directory.Exists(hostFolderPath))
            return null;

        var mappedFolderElem = new XElement("MappedFolder");
        var hostFolderElem = new XElement("HostFolder", hostFolderPath);
        mappedFolderElem.Add(hostFolderElem);

        if (!string.IsNullOrWhiteSpace(sandboxFolder))
        {
            var sandboxFolderElem = new XElement("SandboxFolder", sandboxFolder);
            mappedFolderElem.Add(sandboxFolderElem);
        }

        if (readOnly.HasValue)
        {
            var readOnlyElem = new XElement("ReadOnly", readOnly.Value ? "true" : "false");
            mappedFolderElem.Add(readOnlyElem);
        }

        if (string.IsNullOrWhiteSpace(sandboxFolder))
        {
            var alias = Path.GetFileName(hostFolderPath.Trim(Path.DirectorySeparatorChar));
            sandboxFolder = $"C:\\Users\\WDAGUtilityAccount\\Desktop\\{alias}";
        }

        return new(sandboxFolder, mappedFolderElem);
    }

    private string GeneratePowerShellScript(string thisFolder, string execFileName, string? targetUri)
    {
        var processFilePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(processFilePath))
            throw new Exception("Cannot determine application executable file path.");

        var thisDirectory = Path.GetDirectoryName(processFilePath);
        if (string.IsNullOrWhiteSpace(thisDirectory))
            throw new Exception("Cannot determine application directory path.");

        var targetUriSwitch = string.IsNullOrWhiteSpace(targetUri) ? string.Empty : $"--targetUri={targetUri}";

        return $$"""
            $desktopPath = [Environment]::GetFolderPath("Desktop")
            $sourcePath = Join-Path $desktopPath "dotnet"
            $programFilesPath = [Environment]::GetFolderPath("ProgramFiles")
            $targetPath = Join-Path $programFilesPath "dotnet"

            if (Test-Path $sourcePath) {
                if (-Not (Test-Path $targetPath)) {
                    cmd.exe /c "mklink /D `"$targetPath`" `"$sourcePath`""
                }
            }

            $desktopPath = [Environment]::GetFolderPath('Desktop')
            $shortcutPath = Join-Path $desktopPath "Launch Spork.lnk"
            $targetPath = "{{thisFolder}}\{{execFileName}}"
            $arguments = "--mode=Spork --sporkMode=Embedded {{targetUriSwitch}}"
            $workingDirectory = "{{thisFolder}}"
            $wshShell = New-Object -ComObject WScript.Shell

            $shortcut = $wshShell.CreateShortcut($shortcutPath)
            $shortcut.TargetPath = $targetPath
            $shortcut.Arguments = $arguments
            $shortcut.WorkingDirectory = $workingDirectory
            $shortcut.WindowStyle = 1
            $shortcut.IconLocation = "$targetPath,0"
            $shortcut.Save()

            Start-Process -FilePath $targetPath -ArgumentList $arguments -WorkingDirectory $workingDirectory -Verb RunAs
            """;
    }

    public async Task<string> GenerateWindowsSandboxProfileAsync(
        LauncherMainWindowViewModel launcherViewModel,
        List<string> warnings,
        string? targetUri,
        CancellationToken cancellationToken = default)
    {
        var root = new XElement("Configuration");
        root.Add(new XElement("vGPU", "Disable"));
        root.Add(new XElement("Networking", "Enable"));
        root.Add(new XElement("AudioInput", launcherViewModel.UseMicrophone ? "Enable" : "Disable"));
        root.Add(new XElement("VideoInput", launcherViewModel.UseWebCamera ? "Enable" : "Disable"));
        root.Add(new XElement("ProtectedClient", "Disable"));
        root.Add(new XElement("PrinterRedirection", launcherViewModel.SharePrinters ? "Enable" : "Disable"));
        root.Add(new XElement("ClipboardRedirection", "Enable"));
        root.Add(new XElement("MemoryInMB", 2048));

        var mappedFoldersElem = new XElement("MappedFolders");
        var foldersToMount = new Dictionary<string, XElement>();

        var processFilePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(processFilePath))
            throw new Exception("Cannot determine application executable file path.");

        var thisDirectory = Path.GetDirectoryName(processFilePath);
        if (string.IsNullOrWhiteSpace(thisDirectory))
            throw new Exception("Cannot determine application directory path.");
        var thisFolder = CreateHostFolderMappingElement(thisDirectory);
        if (thisFolder == null)
            throw new Exception($"Cannot create host folder mapping element for '{thisDirectory}'.");
        foldersToMount.Add(thisFolder.Value.Key, thisFolder.Value.Value);

        var launcherAppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Launcher");
        if (string.IsNullOrWhiteSpace(launcherAppDataFolder))
            throw new Exception("Cannot determine application data path.");
        var launcherAppDataFolderElem = CreateHostFolderMappingElement(launcherAppDataFolder);
        if (!launcherAppDataFolderElem.HasValue)
            throw new Exception($"Cannot create host folder mapping element for '{launcherAppDataFolder}'.");
        foldersToMount.Add(launcherAppDataFolderElem.Value.Key, launcherAppDataFolderElem.Value.Value);

        var dotnetCoreDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet");

        if (Directory.Exists(dotnetCoreDirectory))
        {
            var dotnetCoreFolder = CreateHostFolderMappingElement(dotnetCoreDirectory);
            if (dotnetCoreFolder.HasValue)
                foldersToMount.Add(dotnetCoreFolder.Value.Key, dotnetCoreFolder.Value.Value);
        }

        var logonCommandElem = new XElement("LogonCommand");
        var commandElem = new XElement("Command");
        commandElem.Value = $"powershell.exe -ExecutionPolicy Bypass -File {launcherAppDataFolderElem.Value.Key}\\Launch.ps1";
        logonCommandElem.Add(commandElem);
        root.Add(logonCommandElem);

        if (launcherViewModel.MountNpkiFolders)
        {
            var npkiPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "NPKI");
            var npkiFolder = CreateHostFolderMappingElement(npkiPath);

            if (npkiFolder.HasValue)
                foldersToMount.Add(npkiFolder.Value.Key, npkiFolder.Value.Value);
            else
                warnings.Add($"Selected directory '{npkiPath}' does not exists.");
        }

        if (launcherViewModel.MountSpecificFolders)
        {
            foreach (var eachFolder in launcherViewModel.Folders)
            {
                var targetPath = Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(eachFolder));
                var targetItem = CreateHostFolderMappingElement(targetPath);

                if (targetItem.HasValue)
                    foldersToMount.Add(targetItem.Value.Key, targetItem.Value.Value);
                else
                    warnings.Add($"Selected directory '{targetPath}' does not exists.");
            }
        }

        if (foldersToMount.Any())
        {
            foreach (var eachMountPoint in foldersToMount)
                mappedFoldersElem.Add(eachMountPoint.Value);
        }

        root.Add(mappedFoldersElem);

        var doc = new XDocument(root);
        _locationService.EnsureAppDataDirectoryCreated();
        using var fileStream = File.Open(_locationService.WindowsSandboxProfilePath, FileMode.Create);

        using var xw = XmlWriter.Create(fileStream, new XmlWriterSettings()
        {
            Indent = true,
            OmitXmlDeclaration = true,
            Async = true,
        });

        await doc.SaveAsync(xw, cancellationToken).ConfigureAwait(false);

        var scriptContent = GeneratePowerShellScript(thisFolder.Value.Key, Path.GetFileName(processFilePath), targetUri);
        _locationService.EnsureAppDataDirectoryCreated();
        await File.WriteAllTextAsync(
            _locationService.WindowsSandboxLauncherPath,
            scriptContent,
            cancellationToken).ConfigureAwait(false);

        return _locationService.WindowsSandboxProfilePath;
    }
}
