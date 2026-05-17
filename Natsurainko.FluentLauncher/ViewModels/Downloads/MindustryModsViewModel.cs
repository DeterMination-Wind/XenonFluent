using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using FluentLauncher.Infra.UI.Navigation;
using Natsurainko.FluentLauncher.Services;
using Natsurainko.FluentLauncher.Services.Download;
using Natsurainko.FluentLauncher.Services.Launch;
using Natsurainko.FluentLauncher.Utils.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Natsurainko.FluentLauncher.ViewModels.Downloads;

internal partial class MindustryModsViewModel : PageVM, INavigationAware
{
    private readonly MindustryModBrowser _browser;
    private readonly GameService _gameService;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _debounceCts;

    public MindustryModsViewModel()
    {
        // TODO: register MindustryModBrowser in DependencyInjectionExtensions and inject it.
        // For now, instantiate it ad-hoc so the page works without DI plumbing.
        _browser = new MindustryModBrowser(new HttpClient());
        _gameService = App.GetService<GameService>();

        Mods = new ObservableCollection<MindustryModRepo>();
        SortOptions = new List<string> { "stars", "updated" };
        Sort = SortOptions[0];
    }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Sort { get; set; }

    public List<string> SortOptions { get; }

    [ObservableProperty]
    public partial ObservableCollection<MindustryModRepo> Mods { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>True while a mod jar download is in flight (any repo).</summary>
    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    /// <summary>0..1 progress for the active mod download.</summary>
    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    /// <summary>Human-readable speed text, e.g. "1.23 MB/s". Empty when idle.</summary>
    [ObservableProperty]
    public partial string DownloadSpeed { get; set; } = string.Empty;

    /// <summary>"12.3 MB / 45.6 MB" or just received bytes when length unknown.</summary>
    [ObservableProperty]
    public partial string DownloadProgressText { get; set; } = string.Empty;

    /// <summary>FullName of the repo currently being downloaded, for UI labeling.</summary>
    [ObservableProperty]
    public partial string ActiveDownloadRepo { get; set; } = string.Empty;

    void INavigationAware.OnNavigatedTo(object? parameter) { }

    void INavigationAware.OnNavigatedFrom()
    {
        _searchCts?.Cancel();
        _debounceCts?.Cancel();
    }

    protected override void OnLoaded()
    {
        // Kick off the initial top-stars list when the page first appears.
        _ = SearchAsync(force: true);
    }

    partial void OnSearchTextChanged(string value)
    {
        // 500ms debounce so we don't hammer the public GitHub search endpoint.
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await Dispatcher.EnqueueAsync(() => _ = SearchAsync(force: false));
        }, token);
    }

    partial void OnSortChanged(string value) => _ = SearchAsync(force: true);

    [RelayCommand]
    private Task Search() => SearchAsync(force: true);

    private async Task SearchAsync(bool force)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        await Dispatcher.EnqueueAsync(() =>
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            IsEmpty = false;
        });

        List<MindustryModRepo>? result = null;
        string? errorMessage = null;

        try
        {
            result = await _browser.SearchAsync(SearchText, Sort, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (HttpRequestException ex)
        {
            errorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        await Dispatcher.EnqueueAsync(() =>
        {
            IsLoading = false;

            if (errorMessage is not null)
            {
                HasError = true;
                ErrorMessage = errorMessage;
                Mods.Clear();
                IsEmpty = true;
                return;
            }

            Mods.Clear();
            if (result is not null)
            {
                foreach (var repo in result)
                    Mods.Add(repo);
            }

            IsEmpty = Mods.Count == 0;
        });
    }

    [RelayCommand]
    private void OpenInBrowser(MindustryModRepo? repo)
    {
        if (repo is null || string.IsNullOrEmpty(repo.HtmlUrl)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = repo.HtmlUrl,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Swallow — opening a URL is best-effort.
        }
    }

    [RelayCommand]
    private async Task DownloadLatestAsync(MindustryModRepo? repo)
    {
        if (repo is null) return;

        await Dispatcher.EnqueueAsync(() =>
        {
            HasError = false;
            ErrorMessage = string.Empty;
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadSpeed = string.Empty;
            DownloadProgressText = string.Empty;
            ActiveDownloadRepo = repo.FullName;
        });

        var progress = new Progress<Services.Network.DownloadProgressInfo>(p =>
        {
            // Marshal back to UI thread.
            _ = Dispatcher.EnqueueAsync(() =>
            {
                DownloadProgress = p.Percent;
                DownloadSpeed = p.FormatSpeed();
                DownloadProgressText = p.FormatProgress();
            });
        });

        try
        {
            var destFolder = ResolveDestinationFolder();
            var path = await _browser.DownloadLatestReleaseAsync(repo, destFolder, progress, CancellationToken.None)
                .ConfigureAwait(false);

            await Dispatcher.EnqueueAsync(() =>
            {
                HasError = false;
                ErrorMessage = $"Saved to {path}";
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.EnqueueAsync(() =>
            {
                HasError = true;
                ErrorMessage = ex.Message;
            });
        }
        finally
        {
            await Dispatcher.EnqueueAsync(() =>
            {
                IsDownloading = false;
                ActiveDownloadRepo = string.Empty;
            });
        }
    }

    /// <summary>
    /// Pick the right mods folder for the download:
    /// — if there's an active Mindustry instance with a configured GameJarPath,
    ///   use its isolated <c>{workingDir}\.data\Mindustry\mods</c> (matches the
    ///   AppData env override used at launch);
    /// — else fall back to the launcher root's shared <c>mods</c> folder.
    /// Caller has already created the destination folder by the time we hand
    /// it to <see cref="MindustryModBrowser"/>; <see cref="StreamingDownload"/>
    /// also <c>Directory.CreateDirectory</c>s defensively.
    /// </summary>
    private string ResolveDestinationFolder()
    {
        var active = _gameService.ActiveGame;
        var jarPath = active?.GetConfig()?.GameJarPath;

        if (!string.IsNullOrWhiteSpace(jarPath))
        {
            var workingDir = Path.GetDirectoryName(jarPath);
            if (!string.IsNullOrEmpty(workingDir))
                return Path.Combine(workingDir, ".data", "Mindustry", "mods");
        }

        // No active instance configured. Park downloads in the launcher root.
        return Path.Combine(MindustryDataLocator.LauncherRoot, "mods");
    }
}
