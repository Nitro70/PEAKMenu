using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PEAKMenu.Core;
using PEAKMenu.Features;
using UnityEngine;

namespace PEAKMenu
{
    [BepInPlugin(Brand.Guid, Brand.Name, Brand.Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony Harmony;

        private void Awake()
        {
            Log = Logger;
            Settings.Init(Config);

            Harmony = new Harmony(Brand.Guid);
            Features.Patches.Install(Harmony);

            Hub.RegisterAll();

            // our own object, kept across scenes, so the game's scene changes never take the menu with them
            var go = new GameObject("PEAKMenu.Runner");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<Runner>();

            Log.LogInfo($"{Brand.Name} {Brand.Version} by {Brand.Author} loaded. Press {Settings.MenuKey.Value} to open it.");
        }
    }
}
