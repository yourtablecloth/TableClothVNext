using System.Diagnostics;

namespace TableCloth3.Shared;

public sealed class ProcessManager : IDisposable
{
    private Process? _process;
    private TaskCompletionSource<int>? _exitTaskSource;
    private readonly object _lock = new object();
    private bool _disposed = false;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<string>? ErrorReceived;
    public event EventHandler<int>? ProcessExited;

    public bool IsRunning => _process != null && !_process.HasExited;
    public int? ExitCode => _process?.HasExited == true ? _process.ExitCode : null;

    public Task<int> StartAsync(string fileName, string arguments = "", string workingDirectory = "", CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_process != null)
            throw new InvalidOperationException("Process is already started. Create a new instance to start another process.");

        lock (_lock)
        {
            if (_process != null)
                throw new InvalidOperationException("Process is already started.");

            _exitTaskSource = new TaskCompletionSource<int>();

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            _process.OutputDataReceived += OnOutputDataReceived;
            _process.ErrorDataReceived += OnErrorDataReceived;
            _process.Exited += OnProcessExited;
        }

        try
        {
            if (!_process.Start())
                throw new InvalidOperationException($"Failed to start process: {fileName}");

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            cancellationToken.Register(() =>
            {
                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                        _exitTaskSource?.TrySetCanceled(cancellationToken);
                    }
                }
                catch { }
            });

            return Task.FromResult(_process.Id);
        }
        catch
        {
            _process?.Dispose();
            _process = null;
            _exitTaskSource = null;
            throw;
        }
    }

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_process == null)
            throw new InvalidOperationException("Process has not been started.");

        if (_exitTaskSource == null)
            throw new InvalidOperationException("Process exit task is not available.");

        try
        {
            return await _exitTaskSource.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Kill();
            throw;
        }
    }

    public void Kill()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill();
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
        }
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            OutputReceived?.Invoke(this, e.Data);
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            ErrorReceived?.Invoke(this, e.Data);
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_process != null)
        {
            var exitCode = _process.ExitCode;
            ProcessExited?.Invoke(this, exitCode);
            _exitTaskSource?.TrySetResult(exitCode);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ProcessManager));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_process != null)
        {
            _process.OutputDataReceived -= OnOutputDataReceived;
            _process.ErrorDataReceived -= OnErrorDataReceived;
            _process.Exited -= OnProcessExited;

            if (!_process.HasExited)
            {
                try
                {
                    _process.Kill();
                    _process.WaitForExit(5000); // Wait for 5 seconds
                }
                catch { }
            }

            _process.Dispose();
            _process = null;
        }

        _exitTaskSource?.TrySetCanceled();
        _exitTaskSource = null;
    }
}
