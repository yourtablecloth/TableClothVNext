using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace TableCloth3.Shared.Services;

/// <summary>
/// Service to ensure only one instance of the application runs per user session
/// </summary>
[SupportedOSPlatform("windows")]
public class SingleInstanceService : ISingleInstanceService, IDisposable
{
    private readonly ILogger<SingleInstanceService> _logger;
    private Mutex? _mutex;
    private bool _isDisposed;
    private static readonly string MutexName = $"Global\\TableCloth3_{GetCurrentUserSid()}";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public SingleInstanceService(ILogger<SingleInstanceService> logger)
    {
        _logger = logger;
        _mutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            _logger.LogInformation("Another instance of TableCloth3 is already running");
        }
        else
        {
            _logger.LogInformation("Created new singleton instance with mutex: {MutexName}", MutexName);
        }
    }

    public bool IsAnotherInstanceRunning()
    {
        if (_mutex == null)
            return true;

        try
        {
            // Try to acquire the mutex with a very short timeout
            return !_mutex.WaitOne(TimeSpan.FromMilliseconds(100), false);
        }
        catch (AbandonedMutexException)
        {
            // Previous instance terminated abnormally, we can proceed
            _logger.LogWarning("Previous instance terminated abnormally, taking over");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for existing instance");
            return true;
        }
    }

    public bool BringExistingInstanceToForeground()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName);

            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id && !process.HasExited)
                {
                    var mainWindowHandle = process.MainWindowHandle;
                    if (mainWindowHandle != IntPtr.Zero)
                    {
                        _logger.LogInformation("Bringing existing instance to foreground");
                        ShowWindow(mainWindowHandle, SW_RESTORE);
                        return SetForegroundWindow(mainWindowHandle);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bringing existing instance to foreground");
        }

        return false;
    }

    public void ReleaseLock()
    {
        if (_mutex != null && !_isDisposed)
        {
            try
            {
                _mutex.ReleaseMutex();
                _logger.LogInformation("Released singleton mutex lock");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing mutex lock");
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static string GetCurrentUserSid()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            ReleaseLock();
            _mutex?.Dispose();
            _mutex = null;
            _isDisposed = true;
        }
    }
}