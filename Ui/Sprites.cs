using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RyLib
{
    public static class StockIcon
    {
        public const string Players = "hui-p-tab-players";
        public const string Rules = "hui-p-tab-rules";
        public const string Admin = "hui-p-tab-admin";
        public const string Maps = "hui-p-tab-maps";
        public const string Artillery = "hui-p-tab-arty";
        public const string Skull = "hui-p-slay";
        public const string Regiment = "hui-p-players-regiment";
        public const string Unspawned = "hui-p-players-unspawned";
        public const string Shield = "Shield";
        public const string RayGun = "hui-p-menu-ray-gun";
    }

    internal static class Sprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static readonly HashSet<string> Missing = new HashSet<string>(StringComparer.Ordinal);

        private static bool _scannedAll;

        public static Sprite Find(string name, Transform root)
        {
            if (string.IsNullOrEmpty(name)) return null;

            Sprite sprite;
            if (Cache.TryGetValue(name, out sprite) && sprite != null) return sprite;

            if (root != null)
            {
                Remember(root.GetComponentsInChildren<Image>(true));
                if (Cache.TryGetValue(name, out sprite) && sprite != null) return sprite;
            }

            if (!_scannedAll)
            {
                _scannedAll = true;

                Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && !Cache.ContainsKey(all[i].name)) Cache[all[i].name] = all[i];
                }

                if (Cache.TryGetValue(name, out sprite) && sprite != null) return sprite;
            }

            if (Missing.Add(name)) Log.Warn("no sprite named '" + name + "' is loaded, so that icon was left as it was.");
            return null;
        }

        private static void Remember(Image[] images)
        {
            for (int i = 0; i < images.Length; i++)
            {
                Sprite candidate = images[i].sprite;
                if (candidate != null && !Cache.ContainsKey(candidate.name)) Cache[candidate.name] = candidate;
            }
        }
    }
}
