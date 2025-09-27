using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using TableCloth3.Shared.Languages;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.ViewModels;
using static TableCloth3.Shared.ViewModels.AboutWindowViewModel;

namespace TableCloth3.Shared.Windows;

public partial class AboutWindow :
    Window,
    IVisitWebSiteButtonMessageRecipient,
    IVisitGitHubButtonMessageRecipient,
    ICheckUpdateButtonMessageRecipient,
    IShowUpdateNotificationMessageRecipient
{
    [ActivatorUtilitiesConstructor]
    public AboutWindow(
        AboutWindowViewModel viewModel,
        IMessenger messenger,
        ProcessManagerFactory processManagerFactory)
        : this()
    {
        _viewModel = viewModel;
        _messenger = messenger;
        _processManagerFactory = processManagerFactory;

        DataContext = _viewModel;

        _messenger.Register<VisitWebSiteButtonMessage>(this);
        _messenger.Register<VisitGitHubButtonMessage>(this);
        _messenger.Register<CheckUpdateButtonMessage>(this);
        _messenger.Register<ShowUpdateNotificationMessage>(this);

        //ShowAsDialog = true;
    }

    public AboutWindow()
        : base()
    {
        InitializeComponent();
    }

    private readonly AboutWindowViewModel _viewModel = default!;
    private readonly IMessenger _messenger = default!;
    private readonly ProcessManagerFactory _processManagerFactory = default!;

    protected override void OnClosed(EventArgs e)
    {
        _messenger.UnregisterAll(this);
        base.OnClosed(e);
    }

    void IRecipient<VisitWebSiteButtonMessage>.Receive(VisitWebSiteButtonMessage message)
    {
        if (!Uri.TryCreate(SharedStrings.ProjectWebSiteUrl, UriKind.Absolute, out var parsedUri) ||
            parsedUri == null)
            return;

        using var process = _processManagerFactory.CreateShellExecuteProcess(parsedUri.AbsoluteUri);
        process.Start();
    }

    void IRecipient<VisitGitHubButtonMessage>.Receive(VisitGitHubButtonMessage message)
    {
        if (!Uri.TryCreate(SharedStrings.GitHubUrl, UriKind.Absolute, out var parsedUri) ||
            parsedUri == null)
            return;

        using var process = _processManagerFactory.CreateShellExecuteProcess(parsedUri.AbsoluteUri);
        process.Start();
    }

    void IRecipient<CheckUpdateButtonMessage>.Receive(CheckUpdateButtonMessage message)
    {
        // This is now handled by the ViewModel using GitHubUpdateService
        // Fallback to opening releases page if service is not available
        using var process = _processManagerFactory.CreateShellExecuteProcess("https://github.com/yourtablecloth/TableCloth/releases");
        process.Start();
    }

    void IRecipient<ShowUpdateNotificationMessage>.Receive(ShowUpdateNotificationMessage message)
    {
        var icon = message.IsUpdate ? MsBox.Avalonia.Enums.Icon.Info : MsBox.Avalonia.Enums.Icon.Warning;
        var messageBox = MessageBoxManager.GetMessageBoxStandard(
            message.Title,
            message.Message,
            ButtonEnum.Ok,
            icon);

        _ = messageBox.ShowWindowDialogAsync(this);
    }
}
