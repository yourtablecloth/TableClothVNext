using System.Diagnostics;

namespace TableCloth3.Shared.Services;

public sealed class ProcessManagerFactory
{
    public ProcessManager Create()
        => new ProcessManager();

    public Process CreateCmdShellProcess(string fileName, string arguments = "")
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
            Arguments = $"/c start /wait \"\" \"{fileName}\" \"{arguments}\"",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized,
        };

        var process = new Process()
        {
            EnableRaisingEvents = true,
            StartInfo = startInfo,
        };

        return process;
    }

    public Process CreateShellExecuteProcess(string fileName, string arguments = "")
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            throw new PlatformNotSupportedException("This method is only supported on Windows.");

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
        };

        var process = new Process()
        {
            EnableRaisingEvents = true,
            StartInfo = startInfo,
        };

        return process;
    }
}
