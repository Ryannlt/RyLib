using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine.SceneManagement;

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace RyLib
{
    [BepInPlugin(Guid, "RyLib", "1.0.0")]
    public class RyLibPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.ryannlt.rylib";

        private Driver _driver;

        private void Awake()
        {
            GlowStyle.Bind(Config);
            MapStyle.Bind(Config);
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureDriver();

            try
            {
                Harmony.CreateAndPatchAll(typeof(RyLibPlugin).Assembly, Guid);
            }
            catch (Exception error)
            {
                Log.Error("patches could not be applied: " + error.Message);
            }

            Log.Info("Ready.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureDriver();
        }

        private void EnsureDriver()
        {
            if (_driver != null) return;
            _driver = Driver.Attach(this);
        }

        internal void Tick()
        {
            GlowStyle.Poll();
            Menu.Tick();
            PlayerRow.Tick();
            World.Tick();
            Minimap.Tick();
            MapView.TickAll();
            KeyHints.Tick();
        }
    }
}
