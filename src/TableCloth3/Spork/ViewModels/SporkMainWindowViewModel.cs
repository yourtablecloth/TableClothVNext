using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.XPath;
using TableCloth3.Shared.Languages;
using TableCloth3.Shared.Models;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.ViewModels;
using TableCloth3.Spork.Services;
using static TableCloth3.Shared.ViewModels.AboutWindowViewModel;

namespace TableCloth3.Spork.ViewModels;

public sealed partial class SporkMainWindowViewModel : BaseViewModel
{
	[ActivatorUtilitiesConstructor]
	public SporkMainWindowViewModel(
		IMessenger messenger,
		TableClothCatalogService catalogService,
		AvaloniaViewModelManager avaloniaViewModelManager,
		LocationService sporkLocationService,
		ScenarioRouter scenarioRouter,
		DomainCompareService domainCompareService)
		: this()
	{
		_messenger = messenger;
		_catalogService = catalogService;
		_avaloniaViewModelManager = avaloniaViewModelManager;
		_sporkLocationService = sporkLocationService;
		_scenarioRouter = scenarioRouter;
		_domainCompareService = domainCompareService;
	}

	public SporkMainWindowViewModel()
		: base()
	{
		CatalogItems.CollectionChanged += (s, e) =>
		{
			OnPropertyChanged(nameof(HasCatalogItems));
			OnPropertyChanged(nameof(HasNoCatalogItems));
			OnPropertyChanged(nameof(IsLoading));
			OnPropertyChanged(nameof(FilteredCatalogItems));
		};

		AddonItems.CollectionChanged += (s, e) =>
		{
			OnPropertyChanged(nameof(HasAddonItems));
			OnPropertyChanged(nameof(HasNoAddonItems));
			OnPropertyChanged(nameof(IsLoading));
			OnPropertyChanged(nameof(FilteredAddonItems));
		};

		ApplyCatalogFilter();
		ApplyAddonFilter();
	}

	// Catalog

	partial void OnIsLoadingChanged(bool value)
		=> OnPropertyChanged(nameof(IsLoadingCompleted));

	public sealed record class LoadingFailureNotification(Exception OccurredException);

	public interface ILoadingFailureNotificationRecipient : IRecipient<LoadingFailureNotification>;

	public sealed record class CloseButtonRequest;

	public interface ICloseButtonRequestRecipient : IRecipient<CloseButtonRequest>;

	private readonly IMessenger _messenger = default!;
	private readonly TableClothCatalogService _catalogService = default!;
	private readonly AvaloniaViewModelManager _avaloniaViewModelManager = default!;
	private readonly LocationService _sporkLocationService = default!;
	private readonly ScenarioRouter _scenarioRouter = default!;
	private readonly DomainCompareService _domainCompareService = default!;

	protected override void PrepareDesignTimePreview()
	{
		for (int i = 0; i < 4; i++)
		{
			CatalogCategoryItems.Add(new() { CategoryName = "Financing", CategoryDisplayName = "Financing", IsWildcard = false, });
		}

		for (var i = 0; i < 100; i++)
		{
			CatalogItems.Add(new(default!, default!, default!)
			{
				Category = "Financing",
				DisplayName = $"Test {i + 1}",
				ServiceId = $"test{i + 1}",
				TargetUrl = "https://yourtablecloth.app",
				Packages = new()
				{
					new() { PackageName = "Test Package", PackageArguments = "/S", PackageUrl = "https://www.google.com/", },
				},
			});
		}

		for (var i = 0; i < 10; i++)
		{
			AddonItems.Add(new(default!, default!, default!)
			{
				DisplayName = $"Test {i + 1}",
				AddonId = $"test{i + 1}",
				TargetUrl = "https://yourtablecloth.app",
				Arguments = "/S",
			});
		}
	}

	[ObservableProperty]
	private ObservableCollection<TableClothCatalogItemViewModel> _catalogItems = [];

	[ObservableProperty]
	private IEnumerable<TableClothCatalogItemViewModel> _filteredCatalogItems = [];

	[ObservableProperty]
	private string _catalogFilterText = string.Empty;

	[ObservableProperty]
	private TableClothCategoryItemViewModel? _selectedCatalogCategory = default;

	[ObservableProperty]
	private ObservableCollection<TableClothCategoryItemViewModel> _catalogCategoryItems = [];

	[ObservableProperty]
	private bool _isLoading = false;

	public bool IsLoadingCompleted => !IsLoading;

	private TableClothCategoryItemViewModel? _lastCategory;
	private int _menuIndex;
	public int MenuIndex
	{
		get=>_menuIndex;
		set
		{
			if (_menuIndex != value)
			{
				_menuIndex = value;
				if (value == 0)
				{
					SelectedCatalogCategory = _lastCategory;
					_lastCategory = null;
				}

				else
				{
					_lastCategory = SelectedCatalogCategory;
					SelectedCatalogCategory = null;
				}

				OnPropertyChanged(nameof(MenuIndex));
			}

		}
	}

	[RelayCommand]
	public void SetMenu(string key)
	{
		switch (key)
		{
			case "Catalog": MenuIndex = 0; break;
			case "Addon": MenuIndex = 1; break;
			case "About": MenuIndex = 3; break;
		}
	}

	public bool HasCatalogItems
	{
		get
		{
			try { return FilteredCatalogItems.Any(); }
			catch { return false; }
		}
	}

	public bool HasNoCatalogItems => !HasCatalogItems;

	partial void OnCatalogFilterTextChanged(string value)
		=> ApplyCatalogFilter();

	partial void OnSelectedCatalogCategoryChanged(TableClothCategoryItemViewModel? value)
		=> ApplyCatalogFilter();

	[RelayCommand]
	private void ApplyCatalogFilter()
	{
		var catalogQuery = (IEnumerable<TableClothCatalogItemViewModel>)CatalogItems;
		if (!string.IsNullOrWhiteSpace(CatalogFilterText))
			catalogQuery = catalogQuery.Where(x => x.DisplayName.Contains(CatalogFilterText, StringComparison.OrdinalIgnoreCase));
		if (SelectedCatalogCategory != null && !SelectedCatalogCategory.IsWildcard)
			catalogQuery = catalogQuery.Where(x => x.Category.Equals(SelectedCatalogCategory.CategoryName, StringComparison.OrdinalIgnoreCase));
		FilteredCatalogItems = catalogQuery;
	}

	[RelayCommand]
	private async Task Refresh(CancellationToken cancellationToken = default)
	{
		try
		{
			IsLoading = true;
			CatalogItems.Clear();

			if (_scenarioRouter.GetSporkScenario() == SporkScenario.Embedded)
			{
				var launcherDataDirectory = _sporkLocationService.GetDesktopSubDirectory("Launcher");
				if (Directory.Exists(launcherDataDirectory))
				{
					var launcherImagesDirectory = Path.Combine(launcherDataDirectory, "Images");

					var destAppDataDirectory = _sporkLocationService.EnsureAppDataDirectoryCreated().FullName;
					var destImagesDirectory = _sporkLocationService.EnsureImagesDirectoryCreated().FullName;

					File.Copy(
						Path.Combine(launcherDataDirectory, "build-info.json"),
						Path.Combine(destAppDataDirectory, "build-info.json"),
						true);
					File.Copy(
						Path.Combine(launcherDataDirectory, "Catalog.xml"),
						Path.Combine(destAppDataDirectory, "Catalog.xml"),
						true);

					foreach (var eachFile in Directory.GetFiles(launcherImagesDirectory, "*.*"))
					{
						File.Copy(
							eachFile,
							Path.Combine(destImagesDirectory, Path.GetFileName(eachFile)),
							true);
					}
				}

				var npkiDirectory = _sporkLocationService.GetDesktopSubDirectory("NPKI");
				if (Directory.Exists(npkiDirectory))
				{
					var destNPKIDirectory = _sporkLocationService.EnsureLocalLowNpkiDirectoryCreated().FullName;
					CopyDirectory(npkiDirectory, destNPKIDirectory, true);
				}
			}
			else
			{
				var catalogDownloadTask = _catalogService.DownloadCatalogAsync(cancellationToken);
				var imageDownloadTask = _catalogService.DownloadImagesAsync(cancellationToken);
				await Task.WhenAll(catalogDownloadTask, imageDownloadTask).ConfigureAwait(false);
			}

			var doc = await _catalogService.LoadCatalogAsync(cancellationToken).ConfigureAwait(false);
			var services = doc.XPathSelectElements("/TableClothCatalog/InternetServices/Service");

			foreach (var eachService in services)
			{
				var id = eachService.Attribute("Id")?.Value;

				if (string.IsNullOrWhiteSpace(id))
					continue;

				var category = eachService.Attribute("Category")?.Value ?? string.Empty;
				var displayName = eachService.Attribute("DisplayName")?.Value ?? id;
				var url = eachService.Attribute("Url")?.Value;

				var viewModel = _avaloniaViewModelManager.GetAvaloniaViewModel<TableClothCatalogItemViewModel>();
				viewModel.ServiceId = id;
				viewModel.DisplayName = displayName;
				viewModel.Category = category;
				viewModel.CategoryDisplayName = _catalogService.GetCatalogDisplayName(category);
				viewModel.TargetUrl = url ?? string.Empty;

				foreach (var eachPackage in eachService?.Element("Packages")?.Elements("Package") ?? Array.Empty<XElement>())
				{
					var packageUrl = eachPackage.Attribute("Url")?.Value ?? string.Empty;

					if (string.IsNullOrWhiteSpace(packageUrl))
						continue;

					var packageName = eachPackage.Attribute("Name")?.Value ?? "UnknownPackage";
					var packageArgs = eachPackage.Attribute("Arguments")?.Value ?? string.Empty;

					var packageViewModel = _avaloniaViewModelManager.GetAvaloniaViewModel<TableClothPackageItemViewModel>();
					packageViewModel.PackageUrl = packageUrl;
					packageViewModel.PackageName = packageName;
					packageViewModel.PackageArguments = packageArgs;
					viewModel.Packages.Add(packageViewModel);
				}

				CatalogItems.Add(viewModel);
			}

			AddonItems.Clear();

			if (_scenarioRouter.GetSporkScenario() != SporkScenario.Embedded)
			{
				var catalogDownloadTask = _catalogService.DownloadCatalogAsync(cancellationToken);
				var imageDownloadTask = _catalogService.DownloadImagesAsync(cancellationToken);
				await Task.WhenAll(catalogDownloadTask, imageDownloadTask).ConfigureAwait(false);
			}
			else
			{
				var launcherDataDirectory = _sporkLocationService.GetDesktopSubDirectory("Launcher");
				if (Directory.Exists(launcherDataDirectory))
				{
					var launcherImagesDirectory = Path.Combine(launcherDataDirectory, "Images");

					var destAppDataDirectory = _sporkLocationService.EnsureAppDataDirectoryCreated().FullName;
					var destImagesDirectory = _sporkLocationService.EnsureImagesDirectoryCreated().FullName;

					File.Copy(
						Path.Combine(launcherDataDirectory, "build-info.json"),
						Path.Combine(destAppDataDirectory, "build-info.json"),
						true);
					File.Copy(
						Path.Combine(launcherDataDirectory, "Catalog.xml"),
						Path.Combine(destAppDataDirectory, "Catalog.xml"),
						true);

					foreach (var eachFile in Directory.GetFiles(launcherImagesDirectory, "*.*"))
					{
						File.Copy(
							eachFile,
							Path.Combine(destImagesDirectory, Path.GetFileName(eachFile)),
							true);
					}
				}

				var npkiDirectory = _sporkLocationService.GetDesktopSubDirectory("NPKI");
				if (Directory.Exists(npkiDirectory))
				{
					var destNPKIDirectory = _sporkLocationService.EnsureLocalLowNpkiDirectoryCreated().FullName;
					CopyDirectory(npkiDirectory, destNPKIDirectory, true);
				}
			}

			var addons = doc.XPathSelectElements("/TableClothCatalog/Companions/Companion");

			foreach (var eachAddon in addons)
			{
				var id = eachAddon.Attribute("Id")?.Value;

				if (string.IsNullOrWhiteSpace(id))
					continue;

				var displayName = eachAddon.Attribute("DisplayName")?.Value ?? id;
				var url = eachAddon.Attribute("Url")?.Value;
				var arguments = eachAddon.Attribute("Arguments")?.Value ?? string.Empty;

				var viewModel = _avaloniaViewModelManager.GetAvaloniaViewModel<TableClothAddonItemViewModel>();
				viewModel.AddonId = id;
				viewModel.DisplayName = displayName;
				viewModel.Arguments = arguments;
				viewModel.TargetUrl = url ?? string.Empty;

				AddonItems.Add(viewModel);
			}

			var targetUrl = _scenarioRouter.GetSporkTargetUri()?.AbsoluteUri;

			if (targetUrl != null)
			{
				foreach (var eachCatalogItem in CatalogItems)
				{
					if (!(await _domainCompareService.IsSameDomainAsync(targetUrl, eachCatalogItem.TargetUrl, cancellationToken).ConfigureAwait(false)))
						continue;

					_messenger.Send<TableClothCatalogItemViewModel.LaunchSiteRequest>(new(eachCatalogItem, targetUrl));
				}
			}
		}
		catch (Exception ex)
		{
			_messenger?.Send<LoadingFailureNotification>(new(ex));
		}
		finally
		{
			IsLoading = false;
		}

		CatalogCategoryItems.Clear();

		var allItemCategory = _avaloniaViewModelManager.GetAvaloniaViewModel<TableClothCategoryItemViewModel>();
		allItemCategory.CategoryName = "All";
		allItemCategory.CategoryDisplayName = SharedStrings.AllCategoryDisplayName;
		allItemCategory.IsWildcard = true;

		CatalogCategoryItems.Add(allItemCategory);
		SelectedCatalogCategory = allItemCategory;
		foreach (var eachCategory in CatalogItems.Select(x => x.Category).Distinct())
		{
			var eachItem = _avaloniaViewModelManager.GetAvaloniaViewModel<TableClothCategoryItemViewModel>();
			eachItem.CategoryName = eachCategory;
			eachItem.CategoryDisplayName = _catalogService.GetCatalogDisplayName(eachCategory);
			eachItem.IsWildcard = false;
			CatalogCategoryItems.Add(eachItem);
		}

		ApplyCatalogFilter();
		ApplyAddonFilter();
	}

	// Addons

	[RelayCommand]
	private async Task Loaded(CancellationToken cancellationToken = default)
	{
		if (IsDesignMode)
			return;

		await Refresh(cancellationToken).ConfigureAwait(false);
	}

	[ObservableProperty]
	private ObservableCollection<TableClothAddonItemViewModel> _addonItems = [];

	[ObservableProperty]
	private IEnumerable<TableClothAddonItemViewModel> _filteredAddonItems = [];

	[ObservableProperty]
	private string _addonFilterText = string.Empty;

	public bool HasAddonItems
	{
		get
		{
			try { return FilteredAddonItems.Any(); }
			catch { return false; }
		}
	}

	public bool HasNoAddonItems => !HasAddonItems;

	partial void OnAddonFilterTextChanged(string value)
		=> ApplyAddonFilter();

	[RelayCommand]
	private void ApplyAddonFilter()
	{
		var query = (IEnumerable<TableClothAddonItemViewModel>)AddonItems;
		if (!string.IsNullOrWhiteSpace(AddonFilterText))
			query = query.Where(x => x.DisplayName.Contains(AddonFilterText, StringComparison.OrdinalIgnoreCase));
		FilteredAddonItems = query;
	}

	// Shared

	[RelayCommand]
	private void CloseButton()
		=> _messenger.Send<CloseButtonRequest>();

	private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
	{
		var dir = new DirectoryInfo(sourceDir);

		if (!dir.Exists)
			throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

		var subDirs = dir.GetDirectories();

		Directory.CreateDirectory(destinationDir);

		foreach (FileInfo file in dir.GetFiles())
		{
			var targetFilePath = Path.Combine(destinationDir, file.Name);
			file.CopyTo(targetFilePath, overwrite: true);
		}

		if (recursive)
		{
			foreach (DirectoryInfo subDir in subDirs)
			{
				var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
				CopyDirectory(subDir.FullName, newDestinationDir, recursive);
			}
		}
	}

	[ObservableProperty]
	private string versionInfo = Assembly
		.GetExecutingAssembly()
		.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
		?.InformationalVersion ?? SharedStrings.UntaggedBuild;

	[RelayCommand]
	private void VisitWebSiteButton()
		=> _messenger.Send<VisitWebSiteButtonMessage>();

	[RelayCommand]
	private void VisitGitHubButton()
		=> _messenger.Send<VisitGitHubButtonMessage>();

	[RelayCommand]
	private void CheckUpdateButton()
		=> _messenger.Send<CheckUpdateButtonMessage>();
}
