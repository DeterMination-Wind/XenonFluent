using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FluentLauncher.Infra.UI.Dialogs;
using FluentLauncher.Infra.UI.Navigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Natsurainko.FluentLauncher.Models;
// Mindustry rebrand: AccountService is still injected so FluentCore's launch
// pipeline keeps a valid singleton, but the Home page no longer surfaces any
// account UI/binding.
using Natsurainko.FluentLauncher.Services.Accounts;
using Natsurainko.FluentLauncher.Services.Launch;
using Natsurainko.FluentLauncher.Services.Settings;
using Natsurainko.FluentLauncher.Services.UI;
using Natsurainko.FluentLauncher.Services.UI.Messaging;
using Natsurainko.FluentLauncher.Utils;
using Natsurainko.FluentLauncher.Utils.Extensions;
// Account types only referenced inside the commented-out account region below.
//using Nrk.FluentCore.Authentication;
using Nrk.FluentCore.GameManagement.Instances;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Numerics;
using System.Threading.Tasks;
using static Natsurainko.FluentLauncher.Services.UI.SearchProviderService;

#nullable disable
namespace Natsurainko.FluentLauncher.ViewModels.Home;

internal partial class HomeViewModel : PageVM, INavigationAware,
    IRecipient<TrackLaunchTaskChangedMessage>
{
    private readonly GameService _gameService;
    private readonly AccountService _accountService;
    private readonly LaunchService _launchService;
    private readonly SettingsService _settingsService;
    private readonly SearchProviderService _searchProviderService;
    private readonly IDialogActivationService<ContentDialogResult> _dialogService;

    private bool _registeredListener = false;
    private static LaunchTaskViewModel _trackingTask = null;
    private BindedSearchProvider _bindedSearchProvider;

    public ReadOnlyObservableCollection<MinecraftInstance> MinecraftInstances { get; private set; }

    public HomeViewModel(
        GameService gameService,
        AccountService accountService,
        LaunchService launchService,
        SettingsService settingsService,
        SearchProviderService searchProviderService,
        IDialogActivationService<ContentDialogResult> dialogService)
    {
        _accountService = accountService;
        _gameService = gameService;
        _launchService = launchService;
        _settingsService = settingsService;
        _searchProviderService = searchProviderService;
        _dialogService = dialogService;

        // Mindustry rebrand: account list / active account no longer surfaced on Home.

        MinecraftInstances = _gameService.Games;
        ActiveMinecraftInstance = _gameService.ActiveGame;
    }

    #region Removed account bindings (Mindustry rebrand)
    /*
    // Originally surfaced through HomePage.xaml's account selector (now commented
    // out). The AccountAvatar user-control + AuthenticationWizardDialog +
    // Settings/Account page are all excluded from compilation in the csproj.
    // Kept here as a diff/restore reference only.

    public ReadOnlyObservableCollection<Account> Accounts { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountTag))]
    public partial Account ActiveAccount { get; set; }

    public Visibility AccountTag => ActiveAccount is null ? Visibility.Collapsed : Visibility.Visible;

    partial void OnActiveAccountChanged(Account value) => _accountService.ActivateAccount(value);

    [RelayCommand]
    void GoToAccountSettings() => GlobalNavigate("Settings/Navigation", "Settings/Account");

    [RelayCommand]
    async Task AddAccount() => await _dialogService.ShowAsync("AuthenticationWizardDialog");

    void IRecipient<ActiveAccountChangedMessage>.Receive(ActiveAccountChangedMessage message)
        => Dispatcher.TryEnqueue(() => ActiveAccount = message.Value);
    */
    #endregion

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstanceSelectorText))]
    public partial MinecraftInstance ActiveMinecraftInstance { get; set; }

    [ObservableProperty]
    public partial LaunchTaskViewModel TrackingTask { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaunchButtonIcon))]
    public partial bool IsTrackingTask { get; set; }

    [ObservableProperty]
    public partial Vector3 LaunchingInfoGridVector3 { get; set; } = new(480, 0, 0);

    [ObservableProperty]
    public partial Vector3 InstanceSelectorGridVector3 { get; set; } = new();

    [ObservableProperty]
    public partial double LaunchingInfoGridOpacity { get; set; } = 0;

    [ObservableProperty]
    public partial double InstanceSelectorGridOpacity { get; set; } = 1;

    [ObservableProperty]
    public partial string LaunchButtonText { get; set; } = LocalizedStrings.Home_HomePage_LaunchButton_Text;

    // Mindustry rebrand: AccountTag removed (was bound to the old account selector).

    public string InstanceSelectorText => ActiveMinecraftInstance == null
        ? LocalizedStrings.Home_HomePage__NoInstanceSelected
        : ActiveMinecraftInstance.GetDisplayName();

    public string LaunchButtonIcon => IsTrackingTask ? "\uEE95" : "\uF5B0";

    partial void OnIsTrackingTaskChanged(bool value)
    {
        if (IsTrackingTask)
        {
            InstanceSelectorGridVector3 = new Vector3(Convert.ToSingle(App.MainWindow.Width) + 120, 0, 0);
            LaunchingInfoGridVector3 = new Vector3(0, 0, 0);

            InstanceSelectorGridOpacity = 0;
            LaunchingInfoGridOpacity = 1;
        }
        else
        {
            LaunchingInfoGridVector3 = new Vector3(480, 0, 0);
            InstanceSelectorGridVector3 = new Vector3(0, 0, 0);

            InstanceSelectorGridOpacity = 1;
            LaunchingInfoGridOpacity = 0;
        }
    }

    partial void OnActiveMinecraftInstanceChanged(MinecraftInstance value)
    {
        if (value is not null)
            _gameService.ActivateGame(value);
    }

    // Mindustry rebrand: OnActiveAccountChanged removed — see commented account region.

    [RelayCommand(CanExecute = nameof(CanExecuteLaunch))]
    async Task Launch()
    {
        if (_settingsService.HomePageLaunchButtonBehavior == 0)
        {
            _launchService.LaunchFromUI(ActiveMinecraftInstance);
            return;
        }

        if (IsTrackingTask)
        {
            if (TrackingTask.ProcessLaunched)
                TrackingTask.KillProcess();
            else if (TrackingTask.CanCancel)
                await TrackingTask.Cancel();

            return;
        }

        _launchService.LaunchFromUIWithTrack(ActiveMinecraftInstance);
    }

    [RelayCommand]
    void GoToInstancesManage() => GlobalNavigate("Instances/Navigation");

    // Mindustry rebrand: GoToAccountSettings / AddAccount commands removed —
    // their UI buttons are gone from HomePage.xaml and the target page/dialog
    // are excluded from compilation in the csproj.

    [RelayCommand]
    void Continue() => WeakReferenceMessenger.Default.Send(new TrackLaunchTaskChangedMessage(null));

    [RelayCommand]
    void ShowDetails() => GlobalNavigate("Tasks/Launch");

    void TrackingTask_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "ProcessLaunched")
            UpdateLaunchButtonText();
    }

    IEnumerable<Suggestion> ProviderSuggestions(string searchText)
    {
        yield return new Suggestion
        {
            Title = LocalizedStrings.SearchSuggest__T1.Replace("{searchText}", searchText),
            Description = LocalizedStrings.SearchSuggest__D1,
            InvokeAction = () => GlobalNavigate("InstancesDownload/Navigation", searchText)
        };

        foreach (var item in MinecraftInstances)
        {
            if (item.InstanceId.Contains(searchText))
            {
                yield return SuggestionHelper.FromMinecraftInstance(item,
                    LocalizedStrings.SearchSuggest__D4,
                    () => _launchService.LaunchFromUI(item));
            }
        }
    }

    void INavigationAware.OnNavigatedTo(object parameter)
    {
        _bindedSearchProvider = _searchProviderService.BindProvider(this);
        _bindedSearchProvider.BindSuggestionsSource(ProviderSuggestions);

        App.MainWindow.SizeChanged += SizeChanged;

        if (_trackingTask != null && _trackingTask.TaskState == TaskState.Running)
        {
            TrackingTask = _trackingTask;
            IsTrackingTask = true;
            UpdateLaunchButtonText();

            TrackingTask.PropertyChanged += TrackingTask_PropertyChanged;
            _registeredListener = true;
        }
        else _trackingTask = null;

#if FLUENT_LAUNCHER_PREVIEW_CHANNEL
        App.GetService<Services.Network.UpdateService>().CheckLaunchUpdateAfterApplicationStarted(_dialogService);
#endif
    }

    void INavigationAware.OnNavigatedFrom()
    {
        _bindedSearchProvider.Dispose();

        if (_registeredListener)
        {
            TrackingTask.PropertyChanged -= TrackingTask_PropertyChanged;
            _registeredListener = false;
        }

        App.MainWindow.SizeChanged -= SizeChanged;
    }

    void IRecipient<TrackLaunchTaskChangedMessage>.Receive(TrackLaunchTaskChangedMessage message)
    {
        if (_registeredListener)
        {
            TrackingTask.PropertyChanged -= TrackingTask_PropertyChanged;
            _registeredListener = false;
        }

        _trackingTask = message.Value;

        Dispatcher.TryEnqueue(() =>
        {
            TrackingTask = message.Value;
            IsTrackingTask = message.Value != null;
            UpdateLaunchButtonText();

            if (IsTrackingTask)
            {
                TrackingTask.PropertyChanged += TrackingTask_PropertyChanged;
                _registeredListener = true;
            }
        });
    }

    // Mindustry rebrand: ActiveAccountChangedMessage recipient removed —
    // ActiveAccount no longer exists on this view-model.

    void SizeChanged(object s, WindowSizeChangedEventArgs e)
    {
        if (!IsTrackingTask) return;

        InstanceSelectorGridVector3 = new Vector3(Convert.ToSingle(App.MainWindow.Width) + 120, 0, 0);
    }

    bool CanExecuteLaunch() => ActiveMinecraftInstance is not null;

    void UpdateLaunchButtonText()
    {
        if (IsTrackingTask)
        {
            if (TrackingTask.ProcessLaunched)
                LaunchButtonText = LocalizedStrings.Home_HomePage__KillProcess.Replace("Mindustry", TrackingTask.Title);
            else LaunchButtonText = LocalizedStrings.Home_HomePage__CancelLaunch.Replace("Mindustry", TrackingTask.Title);

            return;
        }

        LaunchButtonText = LocalizedStrings.Home_HomePage_LaunchButton_Text;
    }
}
