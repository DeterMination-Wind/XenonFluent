using FluentLauncher.Infra.UI.Navigation;
using Microsoft.UI.Xaml.Controls;
using Natsurainko.FluentLauncher.ViewModels.Downloads.Instances;

namespace Natsurainko.FluentLauncher.Views.Downloads.Instances;

/// <summary>
/// Mindustry rebrand: code-behind is now empty of install-page-specific logic.
/// Loader/mod selection handlers and the OptiFine/Forge install-data formatter
/// were dropped together with the loader/mod UI in the XAML.
/// </summary>
public sealed partial class InstallPage : Page, IBreadcrumbBarAware
{
    string IBreadcrumbBarAware.Route => "Install";

    InstallViewModel VM => (InstallViewModel)DataContext;

    public InstallPage()
    {
        this.InitializeComponent();
    }
}
