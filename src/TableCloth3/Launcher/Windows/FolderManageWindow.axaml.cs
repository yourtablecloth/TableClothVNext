using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using TableCloth3.Launcher.Models;
using TableCloth3.Launcher.ViewModels;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.Windows;
using static TableCloth3.Launcher.ViewModels.FolderManageWindowViewModel;

namespace TableCloth3.Launcher.Windows;

public partial class FolderManageWindow :
    Window,
    IDoubleTappedMessageRecipient,
    IAddFolderButtonMessageRecipient,
    IRemoveFolderButtonMessageRecipient,
    IClearAllFoldersButtonMessageRecipient,
    ICloseButtonMessageRecipient
{
    private readonly FolderManageWindowViewModel _viewModel = default!;
    private readonly IMessenger _messenger = default!;
    private readonly AppSettingsManager _appSettingsManager = default!;

    [ActivatorUtilitiesConstructor]
    public FolderManageWindow(
        FolderManageWindowViewModel viewModel,
        IMessenger messenger,
        AppSettingsManager appSettingsManager)
        : this()
    {
        _viewModel = viewModel;
        _messenger = messenger;
        _appSettingsManager = appSettingsManager;

        DataContext = _viewModel;

        _messenger.Register<DoubleTappedMessage>(this);
        _messenger.Register<AddFolderButtonMessage>(this);
        _messenger.Register<RemoveFolderButtonMessage>(this);
        _messenger.Register<ClearAllFoldersButtonMessage>(this);
        _messenger.Register<CloseButtonMessage>(this);
    }

    public FolderManageWindow()
        : base()
    {
        InitializeComponent();
    }

    public FolderManageWindowViewModel ViewModel
        => _viewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        _appSettingsManager?.LoadAsync<LauncherSerializerContext, LauncherSettingsModel>(LauncherSerializerContext.Default, "launcherConfig.json")
            .ContinueWith(x =>
            {
                var config = x.Result ?? new LauncherSettingsModel();
            })
            .SafeFireAndForget();
        base.OnLoaded(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _messenger?.UnregisterAll(this);
        base.OnClosed(e);
    }

    void IRecipient<AddFolderButtonMessage>.Receive(AddFolderButtonMessage message)
        => ReceiveInternal(message).SafeFireAndForget();

    private async Task ReceiveInternal(AddFolderButtonMessage message)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel != null)
        {
            var folderList = await topLevel.StorageProvider
                .OpenFolderPickerAsync(new FolderPickerOpenOptions() { AllowMultiple = true, })
                .ConfigureAwait(true);

            var pathList = folderList.Select(x => x.Path.LocalPath).Distinct().ToList();

            var existings = _viewModel.Folders.ToHashSet();

            foreach (var eachPath in pathList)
            {
                if (existings.Contains(eachPath)) // Skip if already exists
                {
                    continue;
                } 
                _viewModel.Folders.Add(eachPath);
            }
        }
    }

    void IRecipient<RemoveFolderButtonMessage>.Receive(RemoveFolderButtonMessage message)
    {
        var items = FolderList.SelectedItems?.Cast<object>()?.ToList() ??
            new List<object>();

        foreach (var eachSelectedItem in items)
        {
            var path = Convert.ToString(eachSelectedItem);

            if (path == null)
                continue;

            _viewModel.Folders.Remove(path);
        }
    }

    void IRecipient<ClearAllFoldersButtonMessage>.Receive(ClearAllFoldersButtonMessage message)
    {
        _viewModel.Folders.Clear();
    }

    void IRecipient<CloseButtonMessage>.Receive(CloseButtonMessage message)
    {
        Close();
    }

    void IRecipient<DoubleTappedMessage>.Receive(DoubleTappedMessage message)
    {
        var path = Convert.ToString(FolderList.SelectedItem);

        if (path == null || !Directory.Exists(path))
            return;

        var startInfo = new ProcessStartInfo(path) { UseShellExecute = true, };
        Process.Start(startInfo);
    }
}