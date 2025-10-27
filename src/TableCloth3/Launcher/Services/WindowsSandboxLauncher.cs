using System.Diagnostics;
using TableCloth3.Launcher.ViewModels;
using TableCloth3.Shared.Services;

namespace TableCloth3.Launcher.Services;

public sealed class WindowsSandboxLauncher
{
    public WindowsSandboxLauncher(
        LauncherSettingsManager launcherSettingsManager,
        WindowsSandboxComposer windowsSandboxComposer,
        ProcessManagerFactory processManagerFactory,
        WindowsSandboxExecutionStrategyFactory strategyFactory)
        : base()
    {
        _launcherSettingsManager = launcherSettingsManager;
        _windowsSandboxComposer = windowsSandboxComposer;
        _processManagerFactory = processManagerFactory;
        _strategyFactory = strategyFactory;
    }

    private readonly LauncherSettingsManager _launcherSettingsManager = default!;
    private readonly WindowsSandboxComposer _windowsSandboxComposer = default!;
    private readonly ProcessManagerFactory _processManagerFactory = default!;
    private readonly WindowsSandboxExecutionStrategyFactory _strategyFactory = default!;

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

        // 실행 전략 선택
        var (strategy, isPreferredAvailable) = await _strategyFactory.SelectStrategyAsync(cancellationToken).ConfigureAwait(false);

        // 기본 전략을 사용할 수 없는 경우 경고 메시지 추가
        if (!isPreferredAvailable)
        {
            warnings.Add($"The preferred Windows Sandbox execution method is not available. Using fallback method: {strategy.Name}");
        }

        // 선택된 전략으로 Windows Sandbox 실행
        using var process = await strategy.ExecuteAsync(wsbPath, cancellationToken).ConfigureAwait(false);

        if (process != null)
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return warnings.ToArray();
    }
}
