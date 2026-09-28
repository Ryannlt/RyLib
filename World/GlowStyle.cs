using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace RyLib
{
    public static class GlowStyle
    {
        public static ConfigEntry<bool> ScaleWithDistance;
        public static ConfigEntry<float> OutlineWidth;
        public static ConfigEntry<float> Brightness;
        public static ConfigEntry<float> Glow;
        public static ConfigEntry<float> GlowWidth;

        internal static int Version = 1;

        private static ConfigFile _config;
        private static DateTime _stamp;
        private static float _nextCheck;

        internal static void Bind(ConfigFile config)
        {
            _config = config;

            ScaleWithDistance = config.Bind("Glow", "ScaleWithDistance", true,
                "Shrink the outline and halo with distance, so a far player's halo is not bigger than they are.");
            OutlineWidth = config.Bind("Glow", "OutlineWidth", 0.6f, new ConfigDescription(
                "Outline thickness. 0 turns the outline off.", new AcceptableValueRange<float>(0f, 3f)));
            Brightness = config.Bind("Glow", "Brightness", 0.7f, new ConfigDescription(
                "Colour multiplier. Above 1 lets the game's bloom make the outline glow.", new AcceptableValueRange<float>(0.5f, 6f)));
            Glow = config.Bind("Glow", "Glow", 2f, new ConfigDescription(
                "Halo strength around the outline. 0 turns the halo off.", new AcceptableValueRange<float>(0f, 3f)));
            GlowWidth = config.Bind("Glow", "GlowWidth", 0.04f, new ConfigDescription(
                "Halo width.", new AcceptableValueRange<float>(0f, 1f)));

            config.SettingChanged += delegate { Version++; };
            _stamp = Stamp();
        }

        internal static void Poll()
        {
            if (_config == null || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 1f;

            DateTime stamp = Stamp();
            if (stamp == _stamp) return;

            _stamp = stamp;
            _config.Reload();
            Version++;
        }

        internal static void Saved()
        {
            _stamp = Stamp();
        }

        private static DateTime Stamp()
        {
            try
            {
                return File.GetLastWriteTimeUtc(_config.ConfigFilePath);
            }
            catch (Exception)
            {
                return _stamp;
            }
        }
    }
}
