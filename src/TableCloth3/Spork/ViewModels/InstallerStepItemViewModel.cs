using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DotNext.Threading;
using Microsoft.Extensions.DependencyInjection;
using TableCloth3.Shared;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.ViewModels;
using TableCloth3.Spork.Languages;

namespace TableCloth3.Spork.ViewModels;

public sealed partial class InstallerStepItemViewModel : BaseViewModel, IProgress<int>, IDisposable
{
    public sealed record class UserConfirmationRequest(InstallerStepItemViewModel ViewModel);

    public interface IUserConfirmationRecipient : IRecipient<UserConfirmationRequest>;

    public sealed record class ShowErrorRequest(InstallerStepItemViewModel ViewModel);

    public interface IShowErrorRequestRecipient : IRecipient<ShowErrorRequest>;

    private readonly LocationService _sporkLocationService = default!;
    private readonly IHttpClientFactory _httpClientFactory = default!;
    private readonly ProcessManagerFactory _processManagerFactory = default!;
    private readonly IMessenger _messenger = default!;

    [ActivatorUtilitiesConstructor]
    public InstallerStepItemViewModel(
        LocationService sporkLocationService,
        IHttpClientFactory httpClientFactory,
        ProcessManagerFactory processManagerFactory,
        IMessenger messenger)
        : this()
    {
        _sporkLocationService = sporkLocationService;
        _httpClientFactory = httpClientFactory;
        _processManagerFactory = processManagerFactory;
        _messenger = messenger;
    }

    public InstallerStepItemViewModel()
        : base()
    {
        _confirmEvent = new AsyncAutoResetEvent(false);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _confirmEvent.Dispose();
            }

            _disposed = true;
        }
    }

    ~InstallerStepItemViewModel()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private AsyncAutoResetEvent _confirmEvent;
    private bool _disposed;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private string _serviceId = string.Empty;

    [ObservableProperty]
    private string _packageName = string.Empty;

    [ObservableProperty]
    private string _packageUrl = string.Empty;

    [ObservableProperty]
    private string _packageArguments = string.Empty;

    [ObservableProperty]
    private string _stepError = string.Empty;

    [ObservableProperty]
    private StepProgress _stepProgress = StepProgress.None;

    [ObservableProperty]
    private string _localFilePath = string.Empty;

    [ObservableProperty]
    private bool _requireUserConfirmation = false;

    [ObservableProperty]
    private string _userConfirmationText = SporkStrings.UserConfirmationMessage;

    [ObservableProperty]
    private bool _requireIndirectExecute = false;

    [ObservableProperty]
    private int _percentage = 0;

    partial void OnStepErrorChanged(string value)
        => OnPropertyChanged(nameof(HasError));

    partial void OnStepProgressChanged(StepProgress value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ShowPercentage));
    }

    partial void OnPercentageChanged(int value)
        => OnPropertyChanged(nameof(ShowPercentage));

    [RelayCommand]
    private async Task LoadInstallStep(CancellationToken cancellationToken = default)
    {
        Report(0);

        if (!string.IsNullOrWhiteSpace(PackageUrl))
        {
            var tempFileName = $"{ServiceId}_{PackageName.Replace(" ", "_")}";
            var extension = string.Empty;

            if (Uri.TryCreate(PackageUrl, UriKind.Absolute, out var parsedUri) && parsedUri != null)
                extension = Path.GetExtension(parsedUri.LocalPath).TrimStart('.');

            if (string.IsNullOrWhiteSpace(extension))
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    extension = "exe";
                else
                    extension = "bin";
            }

            var filePath = Path.Combine(
                _sporkLocationService.EnsureDownloadsDirectoryCreated().FullName,
                $"{tempFileName}.{extension}");

            var client = _httpClientFactory.CreateChromeHttpClient();
            using var remoteStream = await client.GetStreamAsync(PackageUrl, cancellationToken).ConfigureAwait(false);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            var remoteLength = default(long?);
            try { remoteLength = remoteStream.Length; }
            catch { remoteLength = default; }

            Report(30);

            await remoteStream.CopyToAsync(fileStream, remoteLength, cancellationToken: cancellationToken).ConfigureAwait(false);
            LocalFilePath = filePath;
        }

        Report(60);
    }

    [RelayCommand]
    private async Task PerformInstallStep(CancellationToken cancellationToken = default)
    {
        Report(60);

        if (!string.IsNullOrWhiteSpace(LocalFilePath) && File.Exists(LocalFilePath))
        {
            if (RequireIndirectExecute)
            {
                using var process = this._processManagerFactory.CreateCmdShellProcess(LocalFilePath, PackageArguments);

                if (process.Start())
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var processManager = _processManagerFactory.Create();
                await processManager.StartAsync(
                    LocalFilePath,
                    PackageArguments,
                    Path.GetDirectoryName(LocalFilePath) ?? string.Empty,
                    cancellationToken)
                    .ConfigureAwait(false);
                await processManager.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (RequireUserConfirmation)
        {
            _messenger.Send(new UserConfirmationRequest(this));
            await _confirmEvent.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        Report(100);
    }

    [RelayCommand]
    private async Task Confirm(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        _confirmEvent.Set();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    [RelayCommand]
    private void ShowError()
    {
        if (_disposed)
            return;

        if (string.IsNullOrWhiteSpace(StepError))
            return;

        _messenger.Send<ShowErrorRequest>(new(this));
    }

    public string StatusText => StepProgress switch
    {
        StepProgress.Loading => "\u23f3",
        StepProgress.Ready => "\ud83d\udce6",
        StepProgress.Installing => "\ud83d\udee0\ufe0f",
        StepProgress.Succeed => "\u2714\ufe0f",
        StepProgress.Failed => "\u274c",
        StepProgress.Unknown => "\u2754",
        _ => "\u2b1c",
    };

    public bool HasError => !string.IsNullOrWhiteSpace(StepError);

    public bool ShowPercentage => StepProgress is StepProgress.Installing or StepProgress.Loading;

    public void Report(int value)
    {
        Percentage = value;
    }
}
