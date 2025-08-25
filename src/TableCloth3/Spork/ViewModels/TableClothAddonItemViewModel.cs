using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.ViewModels;

namespace TableCloth3.Spork.ViewModels;

public sealed partial class TableClothAddonItemViewModel : BaseViewModel
{
    public TableClothAddonItemViewModel(
        IMessenger messenger,
        LocationService sporkLocationService,
        TableClothCatalogService catalogService)
        : base()
    {
        _messenger = messenger;
        _sporkLocationService = sporkLocationService;
        _catalogService = catalogService;

        AddonIcon = new Bitmap(AssetLoader.Open(new Uri("avares://TableCloth3/Assets/Images/Spork.png")));
    }

    [ObservableProperty]
    private string _addonId = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _targetUrl = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private Bitmap? _addonIcon = null;

    public sealed record class LaunchAddonRequest(TableClothAddonItemViewModel ViewModel);

    public interface ILaunchAddonRequestRecipient : IRecipient<LaunchAddonRequest>;

    private readonly IMessenger _messenger = default!;
    private readonly LocationService _sporkLocationService = default!;
    private readonly TableClothCatalogService _catalogService = default!;

    partial void OnAddonIdChanged(string value)
    {
        if (IsDesignMode)
            return;

        AddonIcon = new Bitmap(AssetLoader.Open(new Uri("avares://TableCloth3/Assets/Images/Spork.png")));
    }

    [RelayCommand]
    private void LaunchAddon()
        => _messenger.Send(new LaunchAddonRequest(this));
}
