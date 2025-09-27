using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using TableCloth3.Launcher.ViewModels;
using static TableCloth3.Launcher.ViewModels.DisclaimerWindowViewModel;

namespace TableCloth3.Launcher.Windows;

public partial class DisclaimerWindow :
    Window,
    IAcceptButtonMessageRecipient
{
    [ActivatorUtilitiesConstructor]
    public DisclaimerWindow(
        DisclaimerWindowViewModel viewModel,
        IMessenger messenger)
        : this()
    {
        _viewModel = viewModel;
        _messenger = messenger;

        _messenger.Register<AcceptButtonMessage>(this);

        DataContext = _viewModel;

        //ShowAsDialog = true;
    }

    public DisclaimerWindow()
        : base()
    {
        InitializeComponent();
    }

    private readonly DisclaimerWindowViewModel _viewModel = default!;
    private readonly IMessenger _messenger = default!;

    override protected void OnClosed(EventArgs e)
    {
        _messenger.UnregisterAll(this);
        base.OnClosed(e);
    }

    void IRecipient<AcceptButtonMessage>.Receive(AcceptButtonMessage message)
    {
        Close();
    }
}