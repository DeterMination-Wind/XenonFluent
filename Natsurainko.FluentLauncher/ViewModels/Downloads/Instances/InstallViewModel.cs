using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentLauncher.Infra.UI.Navigation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Natsurainko.FluentLauncher.Services.Download;
using Natsurainko.FluentLauncher.Services.Launch;
using Natsurainko.FluentLauncher.Services.Network;
using Natsurainko.FluentLauncher.Services.UI;
using Natsurainko.FluentLauncher.Utils.Extensions;
using Nrk.FluentCore.GameManagement.Installer;
using System;
using System.IO;
using System.Linq;
using static Natsurainko.FluentLauncher.Services.UI.SearchProviderService;

namespace Natsurainko.FluentLauncher.ViewModels.Downloads.Instances;

/// <summary>
/// Mindustry rebrand: install-page view model.
///
/// The Minecraft pipeline (Forge/Fabric/Quilt loaders, Modrinth pre-install mods,
/// independent-instance toggle, Mojang manifest cross-checks) is gone. Mindustry
/// has no mod loaders and isn't on Modrinth, so the page is just:
///   - Display: source / version id / release date (read from CurrentInstance)
///   - Input:   InstanceId (folder name under versions/)
///   - Action:  Install -> DownloadService.InstallMindustryInstanceAsync(...)
/// </summary>
internal partial class InstallViewModel(
    GameService gameService,
    DownloadService downloadService,
    SearchProviderService searchProviderService) : PageVM, INavigationAware
{
    private BindedSearchProvider? _bindedSearchProvider;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanceIcon))]
    public partial VersionManifestItem CurrentInstance { get; set; } = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanceIdValidity))]
    [NotifyPropertyChangedFor(nameof(CanInstall))]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial string InstanceId { get; set; } = null!;

    public bool InstanceIdValidity =>
        !string.IsNullOrWhiteSpace(InstanceId)
        && !InstanceId.Any(c => Path.GetInvalidFileNameChars().Contains(c))
        && !gameService.Games.Any(x => x.InstanceId.Equals(InstanceId));

    public bool CanInstall => InstanceIdValidity;

    /// <summary>
    /// Single icon for Mindustry instances. Reuses the existing grass-block asset
    /// to avoid bundling new resources; if a dedicated mindustry.png is added later,
    /// switch the path here.
    /// </summary>
    public ImageSource InstanceIcon => new BitmapImage(
        new Uri("ms-appx:///Assets/Icons/grass_block_side.png", UriKind.RelativeOrAbsolute));

    void INavigationAware.OnNavigatedTo(object? parameter)
    {
        _bindedSearchProvider = searchProviderService.BindProvider(this);
        _bindedSearchProvider.BindQuerySubmition(query => GlobalNavigate("InstancesDownload/Navigation", query));

        CurrentInstance = parameter as VersionManifestItem
            ?? throw new InvalidDataException();

        // Default the editable id to the version tag (e.g. "v157.4"); user may suffix.
        InstanceId = CurrentInstance.Id;
    }

    void INavigationAware.OnNavigatedFrom() => _bindedSearchProvider?.Dispose();

    [RelayCommand(CanExecute = nameof(CanInstall))]
    void Install()
    {
        var config = new MindustryInstallConfig
        {
            InstanceId = InstanceId,
            DownloadUrl = CurrentInstance.Url,
            DisplayVersion = CurrentInstance.Id,
            Source = "Mindustry"
        };

        downloadService.InstallMindustryInstanceAsync(config).Forget();
        GlobalNavigate("Tasks/Download");
    }
}
