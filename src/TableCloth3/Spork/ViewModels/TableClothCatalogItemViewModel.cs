using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.ViewModels;

namespace TableCloth3.Spork.ViewModels;

public sealed partial class TableClothCatalogItemViewModel : BaseViewModel
{
    public TableClothCatalogItemViewModel(
        IMessenger messenger,
        LocationService sporkLocationService,
        TableClothCatalogService catalogService)
        : base()
    {
        _messenger = messenger;
        _sporkLocationService = sporkLocationService;
        _catalogService = catalogService;
    }

    public sealed record class LaunchSiteRequest(TableClothCatalogItemViewModel ViewModel, string? TargetUrl);

    public interface ILaunchSiteRequestRecipient : IRecipient<LaunchSiteRequest>;

    private readonly IMessenger _messenger = default!;
    private readonly LocationService _sporkLocationService = default!;
    private readonly TableClothCatalogService _catalogService = default!;

    partial void OnServiceIdChanged(string value)
    {
        if (IsDesignMode)
            return;

        var targetPath = _catalogService.GetLocalImagePath(value);

        if (File.Exists(targetPath))
            ServiceIcon = new Bitmap(targetPath);
        else
            ServiceIcon = new Bitmap(AssetLoader.Open(new Uri("avares://TableCloth3/Assets/Images/Spork.png")));
    }

    [ObservableProperty]
    private string _serviceId = string.Empty;

    [ObservableProperty]
    private Bitmap? _serviceIcon = new Bitmap(AssetLoader.Open(new Uri("avares://TableCloth3/Assets/Images/Spork.png")));

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _categoryDisplayName = string.Empty;

    [ObservableProperty]
    private string _targetUrl = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TableClothPackageItemViewModel> _packages = new();

    [RelayCommand]
    private void LaunchSite(string serviceId)
        => _messenger.Send<LaunchSiteRequest>(new(this, TargetUrl));
}
