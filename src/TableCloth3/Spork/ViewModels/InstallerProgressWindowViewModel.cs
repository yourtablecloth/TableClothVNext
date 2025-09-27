using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using TableCloth3.Shared.ViewModels;

namespace TableCloth3.Spork.ViewModels;

public sealed partial class InstallerProgressWindowViewModel : BaseViewModel
{
    [ActivatorUtilitiesConstructor]
    public InstallerProgressWindowViewModel(
        IMessenger messenger)
        : this()
    {
        _messenger = messenger;
    }

    public InstallerProgressWindowViewModel()
        : base()
    {
    }

    private readonly IMessenger _messenger = default!;

    public sealed record class CancelNotification(bool DueToError, Exception? FoundException);

    public interface ICancelNotificationRecipient : IRecipient<CancelNotification>;

    public sealed record class FailureNotification(Exception FoundException);

    public interface IFailureNotificationRecipient : IRecipient<FailureNotification>;

    public sealed record class FinishNotification(bool HasError);

    public interface IFinishNotificationRecipient : IRecipient<FinishNotification>;

    protected override void PrepareDesignTimePreview()
    {
        for (var i = 0; i < 100; i++)
        {
            var progress = (StepProgress)(i % Enum.GetValues<StepProgress>().Count());
            Steps.Add(new()
            {
                StepProgress = progress,
                PackageName = $"Item {i + 1}",
                PackageUrl = "https://yourtablecloth.app/",
                PackageArguments = "/S",
                StepError = progress == StepProgress.Failed ? "An error occurred while processing this step." : string.Empty,
                Percentage = Random.Shared.Next(0, 100),
            });
        }
    }

    [ObservableProperty]
    private ObservableCollection<InstallerStepItemViewModel> _steps = [];

    [ObservableProperty]
    private string _targetUrl = "https://yourtablecloth.app/";

    [RelayCommand]
    private async Task Loaded(CancellationToken cancellationToken = default)
    {
        if (IsDesignMode)
            return;

        await RunInstallerStepsAsync(cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    private void CancelButton()
        => _messenger.Send<CancelNotification>(new(false, default));

    private async Task RunInstallerStepsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var hasError = false;
            foreach (var item in Steps)
            {
                try
                {
                    item.StepProgress = StepProgress.Loading;
                    await item.LoadInstallStepCommand.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                    item.StepProgress = StepProgress.Ready;
                    await item.PerformInstallStepCommand.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                    item.StepProgress = StepProgress.Succeed;
                }
                catch (Exception ex)
                {
                    hasError = true;
                    item.StepError = ex.Message;
                    item.StepProgress = StepProgress.Failed;
                }
            }

            _messenger.Send<FinishNotification>(new(hasError));
        }
        catch (TaskCanceledException)
        {
            _messenger.Send<CancelNotification>(new(false, default));
        }
        catch (Exception ex)
        {
            _messenger.Send<FailureNotification>(new(ex));
        }
    }
}
