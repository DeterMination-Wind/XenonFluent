using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Natsurainko.FluentLauncher.ViewModels;

#nullable disable
namespace Natsurainko.FluentLauncher.Xaml.Selectors;

internal partial class TaskTemplateSelector : DataTemplateSelector
{
    public DataTemplate LaunchTask { get; set; }

    public DataTemplate DownloadResourceTask { get; set; }

    public DataTemplate InstallInstanceTask { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        if (item is LaunchTaskViewModel)
            return LaunchTask;
        else if (item is DownloadResourceTaskViewModel)
            return DownloadResourceTask;
        else if (item is InstallInstanceTaskViewModel)
            return InstallInstanceTask;
        // Mindustry rebrand: route the simplified Mindustry install task through the
        // same UI shell as the legacy multi-stage installer. Bindings for properties
        // that don't exist on the new VM (IsLaunching, CurrentStage) silently no-op.
        else if (item is MindustryInstallTaskViewModel)
            return InstallInstanceTask;

        return base.SelectTemplateCore(item);
    }
}
