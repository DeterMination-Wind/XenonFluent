using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using FluentLauncher.Infra.UI.Navigation;
using FluentLauncher.Infra.UI.Notification;
using Microsoft.UI.Xaml.Controls;
using Natsurainko.FluentLauncher.Services.Download;
using Natsurainko.FluentLauncher.Services.Launch;
using Natsurainko.FluentLauncher.Services.Network;
using Natsurainko.FluentLauncher.Services.UI;
using Natsurainko.FluentLauncher.Services.UI.Notification;
using Natsurainko.FluentLauncher.Utils;
using Nrk.FluentCore.GameManagement.Installer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static Natsurainko.FluentLauncher.Services.UI.SearchProviderService;

namespace Natsurainko.FluentLauncher.ViewModels.Downloads.Instances;

internal partial class DefaultViewModel(
    MindustryReleaseService mindustryReleaseService,
    SearchProviderService searchProviderService,
    INavigationService navigationService,
    GameService gameService,
    INotificationService notificationService) : PageVM, INavigationAware
{
    private BindedSearchProvider? _bindedSearchProvider;

    public List<VersionManifestItem> AllInstances { get; private set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<VersionManifestItem> FilteredInstances { get; set; }

    [ObservableProperty]
    public partial VersionManifestItem LatestRelease { get; set; }

    [ObservableProperty]
    public partial VersionManifestItem LatestSnapshot { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuery))]
    public partial string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// Selected Mindustry source (0=Mindustry, 1=MindustryX, 2=CN-ARC, 3=Foo).
    /// Bound to the source dropdown. Changing this triggers a reload from the
    /// corresponding GitHub repo via <see cref="MindustryReleaseService"/>.
    /// (Property name kept as <c>ReleaseTypeFilterIndex</c> so the XAML binding
    /// in <c>DefaultPage.xaml</c> stays valid; the semantic is now "source".)
    /// </summary>
    [ObservableProperty]
    public partial int ReleaseTypeFilterIndex { get; set; }

    [ObservableProperty]
    public partial bool Loading { get; set; } = true;

    public bool HasQuery => !string.IsNullOrEmpty(SearchQuery);

    partial void OnReleaseTypeFilterIndexChanged(int value)
    {
        if (this.IsActive)
        {
            // User picked a different source; refetch.
            _ = LoadMindustryReleasesAsync();
        }
    }

    void INavigationAware.OnNavigatedTo(object? parameter)
    {
        _bindedSearchProvider = searchProviderService.BindProvider(this);
        _bindedSearchProvider.BindQuerySubmition(SearchReceiveHandle);

        if (parameter is string searchInstanceId)
            SearchQuery = searchInstanceId;

        _ = LoadMindustryReleasesAsync();
    }

    void INavigationAware.OnNavigatedFrom()
    {
        _bindedSearchProvider?.Dispose();

        FilteredInstances = null!;
        AllInstances = null!;
        LatestRelease = LatestSnapshot = null!;

        GC.Collect();
    }

    [RelayCommand]
    void CardClick(VersionManifestItem instance)
    {
        if (string.IsNullOrEmpty(gameService.ActiveMinecraftFolder))
        {
            notificationService.Show(new ActionNotification
            {
                Title = LocalizedStrings.Notifications__NoMinecraftDataFolder,
                Message = LocalizedStrings.Notifications__NoMinecraftDataFolderDescription,
                Type = NotificationType.Warning,
                GetActionButton = () => new HyperlinkButton()
                {
                    Command = this.GoToSettingsCommand,
                    Content = LocalizedStrings.Instances_DefaultPage__GoToSettings
                }
            });

            return;
        }

        navigationService.NavigateTo("InstancesDownload/Install", instance);
    }

    [RelayCommand]
    void GoToSettings() => GlobalNavigate("Settings/Navigation", "Settings/Launch");

    [RelayCommand]
    void ClearSearchQuery()
    {
        _bindedSearchProvider?.ClearInput();
        SearchReceiveHandle(string.Empty);
    }

    private async Task LoadMindustryReleasesAsync()
    {
        await Dispatcher.EnqueueAsync(() => Loading = true);

        try
        {
            var source = MindustryReleaseService.SourceFromIndex(ReleaseTypeFilterIndex);
            var releases = await mindustryReleaseService.GetReleasesAsync(source);
            var latestRelease = releases.FirstOrDefault(r => r.Type == "release")!;
            var latestSnapshot = releases.FirstOrDefault(r => r.Type == "snapshot")!;

            await Dispatcher.EnqueueAsync(() =>
            {
                AllInstances = releases;
                LatestRelease = latestRelease;
                LatestSnapshot = latestSnapshot;
                SearchReceiveHandle(SearchQuery);
            });
        }
        catch (Exception ex)
        {
            notificationService.LoadInstancesFailed(ex);
        }
        finally
        {
            await Dispatcher.EnqueueAsync(() => Loading = false);
        }
    }

    async void SearchReceiveHandle(string query)
    {
        // The dropdown is now a source selector, not a release-type filter.
        // Show all releases from the chosen source, optionally filtered by query.
        var filteredInstances = AllInstances?
            .Where(i => string.IsNullOrEmpty(query) || i.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray() ?? [];

        await Dispatcher.EnqueueAsync(() =>
        {
            FilteredInstances = new(filteredInstances);
            SearchQuery = query;
        });
    }
}

internal static partial class DefaultViewModelNotifications
{
    [ExceptionNotification(Title = "Notifications__InsatnceListLoadFailed")]
    public static partial void LoadInstancesFailed(this INotificationService notificationService, Exception exception);
}
