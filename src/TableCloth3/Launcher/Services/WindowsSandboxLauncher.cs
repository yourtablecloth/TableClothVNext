using System.Diagnostics;
using TableCloth3.Launcher.ViewModels;
using TableCloth3.Shared.Services;

namespace TableCloth3.Launcher.Services;

public sealed class WindowsSandboxLauncher
{
    public WindowsSandboxLauncher(
        LauncherSettingsManager launcherSettingsManager,
        WindowsSandboxComposer windowsSandboxComposer,
        ProcessManagerFactory processManagerFactory)
        : base()
    {
        _launcherSettingsManager = launcherSettingsManager;
        _windowsSandboxComposer = windowsSandboxComposer;
        _processManagerFactory = processManagerFactory;
    }

    private readonly LauncherSettingsManager _launcherSettingsManager = default!;
    private readonly WindowsSandboxComposer _windowsSandboxComposer = default!;
    private readonly ProcessManagerFactory _processManagerFactory = default!;

    public async Task<string[]> LaunchWindowsSandboxAsync(
        LauncherMainWindowViewModel viewModel,
        string? targetUri,
        CancellationToken cancellationToken = default)
    {
        var processList = Process.GetProcesses().Select(x => x.ProcessName);
        var lookupList = new List<string> { "WindowsSandbox", "WindowsSandboxRemoteSession", "WindowsSandboxServer", };
        foreach (var eachProcessName in processList)
            if (lookupList.Contains(eachProcessName, StringComparer.OrdinalIgnoreCase))
                throw new Exception("Only one Windows Sandbox session allowed.");

        var warnings = new List<string>();
        var config = await _launcherSettingsManager.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
        var wsbPath = await _windowsSandboxComposer.GenerateWindowsSandboxProfileAsync(
            viewModel, warnings, targetUri, cancellationToken).ConfigureAwait(false);

        var windowsSandboxExecPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsSandbox.exe");

        using var process = _processManagerFactory.CreateCmdShellProcess(windowsSandboxExecPath, wsbPath);

        if (process.Start())
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return warnings.ToArray();
    }
}
