using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using TableCloth3.Help.ViewModels;
using TableCloth3.Shared.Languages;
using TableCloth3.Shared.Services;
using static TableCloth3.Help.ViewModels.HelpMainWindowViewModel;

namespace TableCloth3.Help.Windows;

public partial class HelpMainWindow :
    Window,
    ICloseButtonMessageRecipient,
    ISponsorButtonMessageRecipient
{
    [ActivatorUtilitiesConstructor]
    public HelpMainWindow(
        HelpMainWindowViewModel viewModel,
        IMessenger messenger,
        ProcessManagerFactory processManagerFactory)
        : this()
    {
        _viewModel = viewModel;
        _messenger = messenger;
        _processManagerFactory = processManagerFactory;

        DataContext = _viewModel;

        _messenger.Register<CloseButtonMessage>(this);
        _messenger.Register<SponsorButtonMessage>(this);

        //ShowAsDialog = true;
    }

    public HelpMainWindow()
        : base()
    {
        InitializeComponent();
    }

    private readonly HelpMainWindowViewModel _viewModel = default!;
    private readonly IMessenger _messenger = default!;
    private readonly ProcessManagerFactory _processManagerFactory = default!;

    protected override void OnClosed(EventArgs e)
    {
        _messenger.UnregisterAll(this);
        base.OnClosed(e);
    }

    void IRecipient<CloseButtonMessage>.Receive(CloseButtonMessage message)
    {
        Close();
    }

    void IRecipient<SponsorButtonMessage>.Receive(SponsorButtonMessage message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!Uri.TryCreate(SharedStrings.GitHubSponsorsUrl, UriKind.Absolute, out var parsedUri) ||
                parsedUri == null)
                return;

            using var process = _processManagerFactory.CreateShellExecuteProcess(parsedUri.AbsoluteUri);
            process.Start();
        });
    }
}
