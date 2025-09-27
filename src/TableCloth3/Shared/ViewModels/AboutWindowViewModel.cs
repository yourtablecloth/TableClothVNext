using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TableCloth3.Shared.Languages;
using TableCloth3.Shared.Services;

namespace TableCloth3.Shared.ViewModels;

public sealed partial class AboutWindowViewModel : BaseViewModel
{
    [ActivatorUtilitiesConstructor]
    public AboutWindowViewModel(
        IMessenger messenger,
        GitHubUpdateService updateService)
        : this()
    {
        this.messenger = messenger;
        _updateService = updateService;
    }

    public AboutWindowViewModel()
        : base()
    {
    }

    private readonly IMessenger messenger = default!;
    private readonly GitHubUpdateService _updateService = default!;

    public sealed record class VisitWebSiteButtonMessage;

    public interface IVisitWebSiteButtonMessageRecipient : IRecipient<VisitWebSiteButtonMessage>;

    public sealed record class VisitGitHubButtonMessage;

    public interface IVisitGitHubButtonMessageRecipient : IRecipient<VisitGitHubButtonMessage>;

    public sealed record class CheckUpdateButtonMessage;

    public interface ICheckUpdateButtonMessageRecipient : IRecipient<CheckUpdateButtonMessage>;

    public sealed record class ShowUpdateNotificationMessage(string Title, string Message, bool IsUpdate = false);

    public interface IShowUpdateNotificationMessageRecipient : IRecipient<ShowUpdateNotificationMessage>;

    [ObservableProperty]
    private string versionInfo = Assembly
        .GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? SharedStrings.UntaggedBuild;

    [ObservableProperty]
    private bool isCheckingUpdate = false;

    [RelayCommand]
    private void VisitWebSiteButton()
        => messenger.Send<VisitWebSiteButtonMessage>();

    [RelayCommand]
    private void VisitGitHubButton()
        => messenger.Send<VisitGitHubButtonMessage>();

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task CheckUpdateButton(CancellationToken cancellationToken = default)
    {
        if (_updateService == null)
        {
            messenger.Send<CheckUpdateButtonMessage>();
            return;
        }

        IsCheckingUpdate = true;

        try
        {
            var release = await _updateService.CheckForUpdatesAsync(cancellationToken);
            
            if (release == null)
            {
                messenger.Send(new ShowUpdateNotificationMessage(
                    SharedStrings.CheckUpdateButton,
                    SharedStrings.UpdateCheckFailedMessage));
                return;
            }

            if (_updateService.IsUpdateAvailable(release))
            {
                var message = string.Format(
                    SharedStrings.UpdateAvailableMessage, 
                    release.TagName);
                    
                messenger.Send(new ShowUpdateNotificationMessage(
                    SharedStrings.NewUpdateAvailable,
                    message,
                    true));

                // Open the release page automatically after user sees the message
                await Task.Delay(100, cancellationToken); // Small delay to ensure message is shown
                _updateService.OpenReleasePage(release);
            }
            else
            {
                messenger.Send(new ShowUpdateNotificationMessage(
                    SharedStrings.NoUpdatesAvailable,
                    SharedStrings.UpToDateMessage));
            }
        }
        catch (OperationCanceledException)
        {
            // User cancelled, ignore
        }
        catch
        {
            messenger.Send(new ShowUpdateNotificationMessage(
                SharedStrings.CheckUpdateButton,
                SharedStrings.UpdateCheckFailedMessage));
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }
}
