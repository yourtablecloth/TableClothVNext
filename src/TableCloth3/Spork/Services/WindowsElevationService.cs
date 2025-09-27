using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using TableCloth3.Spork.Contracts;

namespace TableCloth3.Spork.Services;

public sealed class WindowsElevationService : IElevationService
{
    public bool IsElevated()
    {
        if (!OperatingSystem.IsWindows())
            throw new NotSupportedException();

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void RestartAsElevated(string[] args)
    {
        var exeName = GetCurrentExecutablePath();
        if (string.IsNullOrEmpty(exeName))
            throw new InvalidOperationException("Cannot find executable file path. Please run with administrator privileges manually.");

        var startInfo = new ProcessStartInfo
        {
            FileName = exeName,
            UseShellExecute = true,
            Verb = "runas"
        };

        if (args != null && args.Length > 0)
            startInfo.Arguments = string.Join(" ", args);

        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to execute with administrator privileges: " + ex.Message, ex);
        }

        Environment.Exit(0);
    }

    private string? GetCurrentExecutablePath()
    {
        try { return Environment.ProcessPath; }
        catch { return null; }
    }
}
