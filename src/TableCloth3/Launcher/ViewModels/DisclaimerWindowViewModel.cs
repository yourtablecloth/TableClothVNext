using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using TableCloth3.Shared.ViewModels;

namespace TableCloth3.Launcher.ViewModels;

public sealed partial class DisclaimerWindowViewModel : BaseViewModel
{
    public sealed record class AcceptButtonMessage;

    public interface IAcceptButtonMessageRecipient : IRecipient<AcceptButtonMessage>;

    [ActivatorUtilitiesConstructor]
    public DisclaimerWindowViewModel(
        IMessenger messenger)
        : this()
    {
        _messenger = messenger;
    }

    public DisclaimerWindowViewModel()
        : base()
    {
    }

    private readonly IMessenger _messenger = default!;

    [RelayCommand]
    private void Accept()
        => _messenger.Send<AcceptButtonMessage>();
}
