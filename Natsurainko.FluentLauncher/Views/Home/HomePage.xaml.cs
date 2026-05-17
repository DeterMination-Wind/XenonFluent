using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
// Mindustry rebrand: account-related types and globalization helpers are no
// longer used because the account selector / Account converters are gone.
//using Microsoft.Windows.Globalization;
using Natsurainko.FluentLauncher.Services.Settings;
//using Natsurainko.FluentLauncher.Utils;
using Natsurainko.FluentLauncher.ViewModels.Home;
//using Nrk.FluentCore.Authentication;
using System;
using Windows.Foundation;
using Windows.UI;

namespace Natsurainko.FluentLauncher.Views.Home;

public sealed partial class HomePage : Page
{
    private readonly SettingsService _settingsService = App.GetService<SettingsService>();

    HomeViewModel VM => (HomeViewModel)DataContext;

    public HomePage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        var themeDictionaries = (App.Current.Resources.ThemeDictionaries[this.ActualTheme == ElementTheme.Light ? "Light" : "Dark"] as ResourceDictionary)!;

        if (_settingsService.UseHomeControlsMask)
        {
            // Mindustry rebrand: AccountSelectorButton + AccountSelectorArea are gone
            // (account UI removed). The mask only adjusts the remaining instance/launching
            // areas and the launch button.
            LaunchButton.Translation += new System.Numerics.Vector3(0, 0, 16);

            foreach (var border in new Border[] { InstanceSelectorArea, LaunchingInfoArea })
            {
                border.Translation += new System.Numerics.Vector3(0, 0, 16);
                border.Background = themeDictionaries["NavigationViewUnfoldedPaneBackground"] as AcrylicBrush;
                border.BorderThickness = new Thickness(1);
                border.BorderBrush = themeDictionaries["ButtonBorderBrushPointerOver"] as Brush;
            }

            this.ActualThemeChanged += (_, e) =>
            {
                var themeDictionaries = (App.Current.Resources.ThemeDictionaries[this.ActualTheme == ElementTheme.Light ? "Light" : "Dark"] as ResourceDictionary)!;

                foreach (var border in new Border[] { InstanceSelectorArea, LaunchingInfoArea })
                {
                    border.Background = themeDictionaries["NavigationViewUnfoldedPaneBackground"] as AcrylicBrush;
                    border.BorderThickness = new Thickness(1);
                    border.BorderBrush = themeDictionaries["ButtonBorderBrushPointerOver"] as Brush;
                }
            };
        }

        if (_settingsService.HomeLaunchButtonSize == 1)
        {
            LaunchButtonIcon.FontSize = 18;
            LaunchButton.FontSize = 16;

            LaunchButton.VerticalAlignment = VerticalAlignment.Stretch;
        }

        InstanceSelectorGrid.TranslationTransition = new Vector3Transition()
        {
            Duration = TimeSpan.FromMilliseconds(500)
        };
        LaunchingInfoGrid.TranslationTransition = new Vector3Transition()
        {
            Duration = TimeSpan.FromMilliseconds(500)
        };

        InstanceSelectorGrid.OpacityTransition = new ScalarTransition()
        {
            Duration = TimeSpan.FromMilliseconds(250)
        };
        LaunchingInfoGrid.OpacityTransition = new ScalarTransition()
        {
            Duration = TimeSpan.FromMilliseconds(250)
        };

        LaunchButton.Focus(FocusState.Programmatic);
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        this.DataContext = null;

        InstancesListView.ItemsSource = null;
        // Mindustry rebrand: AccountsListView removed alongside the account selector.
    }

    private void Flyout_Opened(object sender, object e) => InstancesListView.ScrollIntoView(VM.ActiveMinecraftInstance);

    // Mindustry rebrand: HideAccountFlyoutHandler removed — the account flyout
    // (and the AccountAvatar / DropDownButton that hosted it) are gone.

    private void DropDownButton_Click(object sender, RoutedEventArgs e)
    {
        var transform = DropDownButton.TransformToVisual(Grid);
        var absolutePosition = transform.TransformPoint(new Point(0, 0));

        InstancesListView.MaxHeight = absolutePosition.Y - 50;

        if (this.ActualWidth > 550)
        {
            InstancesListView.MaxWidth = 400;
            InstancesListView.Width = double.NaN;
        }
        else
        {
            InstancesListView.MaxWidth = 430;
            InstancesListView.Width = 430;
        }
    }

    // Mindustry rebrand: GetAccountTypeName / TryGetYggdrasilServerName converters
    // were only used by the now-removed account selector XAML. Dropped to keep
    // the code-behind free of Microsoft / Yggdrasil account terminology.
}
