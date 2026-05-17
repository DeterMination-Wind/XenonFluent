using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Natsurainko.FluentLauncher.Services.Download;
using Natsurainko.FluentLauncher.ViewModels.Downloads;

namespace Natsurainko.FluentLauncher.Views.Downloads.Mods;

public sealed partial class NavigationPage : Page
{
    MindustryModsViewModel VM => (MindustryModsViewModel)DataContext;

    public NavigationPage()
    {
        this.InitializeComponent();
    }

    private void OpenInBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is MindustryModRepo repo)
            VM.OpenInBrowserCommand.Execute(repo);
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is MindustryModRepo repo)
            VM.DownloadLatestCommand.Execute(repo);
    }
}
