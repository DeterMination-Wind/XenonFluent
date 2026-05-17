using Microsoft.UI.Xaml.Data;
using Nrk.FluentCore.GameManagement.Mods;
using System;
using System.Collections.Generic;
using System.IO;

#nullable disable
namespace Natsurainko.FluentLauncher.Xaml.Converters;

internal partial class ModInfoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is MinecraftMod mod)
        {
            var strings = new List<string>();

            if (!string.IsNullOrEmpty(mod.Version))
                strings.Add(mod.Version);

            if (!string.IsNullOrEmpty(mod.Description))
                strings.Add(mod.Description);

            if (mod.Authors != null && mod.Authors.Length != 0)
                strings.Add(string.Join(", ", mod.Authors));

            // Mindustry rebrand: dropped the SupportedModLoaders branch (Forge/Fabric concept).
            // Only show "Unable to parse mod details" when we genuinely couldn't extract anything.
            if (strings.Count == 0)
                return "Unable to parse mod details";

            return string.Join(" | ", strings);
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
