using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using FluentLauncher.Infra.UI.Navigation;
using Natsurainko.FluentLauncher.Utils;
using Natsurainko.FluentLauncher.Utils.Extensions;
using Nrk.FluentCore.GameManagement.Instances;
using Nrk.FluentCore.GameManagement.Saves;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;

#nullable disable
namespace Natsurainko.FluentLauncher.ViewModels.Instances;

internal partial class SaveViewModel : PageVM, INavigationAware
{
    public MinecraftInstance MinecraftInstance { get; private set; }

    public string SavesFolder { get; private set; }

    public ObservableCollection<SaveInfo> Saves { get; private set; } = [];

    async void INavigationAware.OnNavigatedTo(object parameter)
    {
        MinecraftInstance = parameter as MinecraftInstance;

        // Mindustry rebrand: saves live next to the launched jar at
        // {workingDir}\.data\Mindustry\saves (matches the AppData env override
        // we set in LaunchService for per-instance isolation).
        var jarPath = MinecraftInstance.GetConfig()?.GameJarPath;
        if (!string.IsNullOrWhiteSpace(jarPath))
        {
            var workingDir = Path.GetDirectoryName(jarPath);
            if (!string.IsNullOrEmpty(workingDir))
                SavesFolder = Path.Combine(workingDir, ".data", "Mindustry", "saves", "saves");
        }
        SavesFolder ??= MinecraftInstance.GetSavesDirectory();

        Directory.CreateDirectory(SavesFolder);

        // Mindustry persists each save as a single .msav file (and an optional
        // sibling .png preview), unlike Minecraft's per-world subfolder layout.
        // Enumerate them directly instead of going through FluentCore's
        // SaveManager (which expects level.dat).
        await Task.Run(() =>
        {
            foreach (var msav in Directory.EnumerateFiles(SavesFolder, "*.msav", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(msav);
                var icon = Path.ChangeExtension(msav, ".png");
                var info = new SaveInfo
                {
                    Folder = msav,
                    FolderName = Path.GetFileName(msav),
                    LevelName = name,
                    Version = string.Empty,
                    LastPlayed = File.GetLastWriteTime(msav),
                    IconFilePath = File.Exists(icon) ? icon : null,
                };
                _ = Dispatcher.EnqueueAsync(() => Saves.Add(info));
            }
        });
    }

    [RelayCommand]
    void OpenSavesFolder() => ExplorerHelper.OpenFolder(SavesFolder);

    [RelayCommand]
    void OpenSaveFolder(SaveInfo saveInfo) => ExplorerHelper.ShowAndSelectFile(saveInfo.Folder);
}
