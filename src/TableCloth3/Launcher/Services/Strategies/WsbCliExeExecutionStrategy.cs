using System.Diagnostics;
using TableCloth3.Launcher.Contracts;
using TableCloth3.Shared.Services;

namespace TableCloth3.Launcher.Services.Strategies;

/// <summary>
/// WindowsSandbox.exe를 직접 실행하는 전략 (기본 권장 방식)
/// </summary>
public sealed class WsbCliExeExecutionStrategy : IWindowsSandboxExecutionStrategy
{
    private readonly ProcessManagerFactory _processManagerFactory;

    public WsbCliExeExecutionStrategy(ProcessManagerFactory processManagerFactory)
    {
        _processManagerFactory = processManagerFactory;
    }

    public string Name => "WindowsSandbox.exe Direct Execution";

    public bool IsPreferred => true;

    public Task<bool> CanExecuteAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(WindowsSandbox.CanUseSandboxCli());

    public async Task ExecuteAsync(string profilePath, CancellationToken cancellationToken = default)
    {
        if (!WindowsSandbox.CanUseSandboxCli())
            throw new FileNotFoundException($"wsb.exe is not available.");

        var currentId = await WindowsSandbox.GetSingleSandboxIdAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(currentId))
        {
            var startResult = await WindowsSandbox.StartSandboxAsync(
                await File.ReadAllTextAsync(profilePath, cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(startResult.Error))
                throw new InvalidOperationException($"Failed to start Windows Sandbox: {startResult.Error}");

            if (string.IsNullOrWhiteSpace(startResult.SandboxId))
                throw new InvalidOperationException("Sandbox was not initialized.");

            currentId = startResult.SandboxId;
        }

        _ = WindowsSandbox.ConnectToSandboxAsync(currentId, cancellationToken).ConfigureAwait(false);

        await WindowsSandbox.WaitUntilSandboxReadyAsync(currentId, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
