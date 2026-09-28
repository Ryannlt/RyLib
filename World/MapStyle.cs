using BepInEx.Configuration;

namespace RyLib
{
    public static class MapStyle
    {
        public static ConfigEntry<float> IconSize;

        internal static void Bind(ConfigFile config)
        {
            IconSize = config.Bind("Map", "IconSize", 22f, new ConfigDescription(
                "Size of the class icons on the P menu map, in pixels.", new AcceptableValueRange<float>(10f, 40f)));
        }
    }
}
