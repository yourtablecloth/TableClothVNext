using System.Diagnostics;
using TableCloth3.Launcher.Contracts;
using TableCloth3.Shared.Services;

namespace TableCloth3.Launcher.Services.Strategies;

/// <summary>
/// cmd.exe를 통해 WindowsSandbox.exe를 실행하는 전략 (대체 방식)
/// </summary>
public sealed class CmdShellExecutionStrategy : IWindowsSandboxExecutionStrategy
{
    private readonly ProcessManagerFactory _processManagerFactory;

    public CmdShellExecutionStrategy(ProcessManagerFactory processManagerFactory)
    {
        _processManagerFactory = processManagerFactory;
    }

    public string Name => "CMD Shell Execution";

    public bool IsPreferred => false;

    public Task<bool> CanExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var windowsSandboxExecPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
                 "WindowsSandbox.exe");

            var cmdPath = Path.Combine(
                  Environment.GetFolderPath(Environment.SpecialFolder.System),
                "cmd.exe");

            // WindowsSandbox.exe와 cmd.exe 둘 다 존재하는지 확인
            var exists = File.Exists(windowsSandboxExecPath) && File.Exists(cmdPath);

            return Task.FromResult(exists);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public async Task<Process> ExecuteAsync(string profilePath, CancellationToken cancellationToken = default)
    {
        var windowsSandboxExecPath = Path.Combine(
           Environment.GetFolderPath(Environment.SpecialFolder.System),
           "WindowsSandbox.exe");

        if (!File.Exists(windowsSandboxExecPath))
            throw new FileNotFoundException($"WindowsSandbox.exe not found at: {windowsSandboxExecPath}");

        // cmd.exe를 통해 WindowsSandbox.exe 실행 (기존 방식)
        var process = _processManagerFactory.CreateCmdShellProcess(windowsSandboxExecPath, profilePath);

        if (!process.Start())
            throw new InvalidOperationException("Failed to start Windows Sandbox process.");

        await Task.CompletedTask;
        return process;
    }
}
