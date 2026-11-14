using System;
using HarmonyLib;
using PEAKMenu.Core;
using UnityEngine;

namespace PEAKMenu.Features
{
    // Every hook into the game's own code. Each one is installed on its own and looked up by name,
    // so a game update that renames one method only switches off the feature that needs it.
    internal static class Patches
    {
        public static readonly System.Collections.Generic.List<string> Failed = new();

        public static void Install(Harmony h)
        {
            Try(h, typeof(Character), "Update", postfix: nameof(CharacterUpdatePost));
            Try(h, typeof(CharacterAfflictions), "UpdateWeight", postfix: nameof(UpdateWeightPost));
            Try(h, typeof(CharacterAfflictions), "AddStatus", prefix: nameof(AddStatusPre));
            Try(h, typeof(Character), "Fall", prefix: nameof(FallPre));
            Try(h, typeof(PointPinger), "ReceivePoint_Rpc", postfix: nameof(PingPost));
            Try(h, typeof(GUIManager), "UpdateWindowStatus", postfix: nameof(WindowStatusPost));
            Try(h, typeof(MainCameraMovement), "LateUpdate", postfix: nameof(CameraLatePost));
        }

        // the menu counts as one of the game's windows: free mouse, no walking or looking
        private static void WindowStatusPost(GUIManager __instance)
        {
            if (UI.MainWindow.Open && __instance)
            {
                __instance.windowShowingCursor = true;
                __instance.windowBlockingInput = true;
            }
        }

        // the game sets the field of view every frame; ours goes on after it
        private static void CameraLatePost()
        {
            Safe.Run("visuals.fov", Visuals.AfterCamera);
        }

        private static void Try(Harmony h, Type type, string method, string prefix = null, string postfix = null)
        {
            try
            {
                var target = AccessTools.Method(type, method);

                if (target == null)
                {
                    Failed.Add(type.Name + "." + method);
                    Plugin.Log.LogWarning($"Hook {type.Name}.{method} not found in this game version; the features using it stay off.");
                    return;
                }

                h.Patch(target,
                    prefix == null ? null : new HarmonyMethod(typeof(Patches), prefix),
                    postfix == null ? null : new HarmonyMethod(typeof(Patches), postfix));
            }
            catch (Exception e)
            {
                Failed.Add(type.Name + "." + method);
                Plugin.Log.LogError($"Hook {type.Name}.{method} failed: {e}");
            }
        }

        private static void CharacterUpdatePost(Character __instance)
        {
            if (__instance && __instance.IsLocal)
            {
                Safe.Run("move.fly", () => Movement.FlyTick(__instance));
            }
        }

        private static void UpdateWeightPost(CharacterAfflictions __instance)
        {
            if (SelfMods.NoWeight && __instance && __instance.character && __instance.character.IsLocal)
            {
                __instance.SetStatus(CharacterAfflictions.STATUSTYPE.Weight, 0f);
            }
        }

        // god mode: nothing bad can be added to you (healing, a negative amount, still works)
        private static bool AddStatusPre(CharacterAfflictions __instance, CharacterAfflictions.STATUSTYPE statusType, float amount, ref bool __result)
        {
            if (!SelfMods.God || amount <= 0f || !__instance || !__instance.character || !__instance.character.IsLocal)
            {
                return true;
            }

            __result = false;
            return false;
        }

        // never knocked over: skip the ragdoll fall (falls from height, getting hit, the beach wake-up)
        private static bool FallPre(Character __instance)
        {
            return !(SelfMods.NoFallOver && __instance && __instance.IsLocal);
        }

        private static void PingPost(PointPinger __instance, Vector3 point)
        {
            if (!Teleport.ToPing || !__instance)
            {
                return;
            }

            var owner = Safe.Get("ping.owner", () => __instance.character ? __instance.character : null, null);

            if (owner && owner.IsLocal)
            {
                Teleport.To(point + Vector3.up * 1.2f, "your ping");
            }
        }
    }
}
