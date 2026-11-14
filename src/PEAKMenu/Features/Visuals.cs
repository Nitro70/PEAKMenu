using PEAKMenu.Core;
using PEAKMenu.UI;
using UnityEngine;
using Zorro.Core;

namespace PEAKMenu.Features
{
    // What you see: field of view, fog, light, the HUD, a free camera. All of it is on your screen
    // only. The game rewrites fog and light every frame, so ours goes on afterwards (LateUpdate and
    // a hook after the camera), and when a switch goes off the game's own values come straight back.
    internal static class Visuals
    {
        public static bool Fov, NoFog, Fullbright, HideHud, Freecam, AlwaysDay;
        private static bool hudHidden;

        private static float FovValue => Settings.Float("vis.fov", 100f);
        private static float Brightness => Settings.Float("vis.bright", 1f);

        public static void Register()
        {
            var tab = Registry.Tab("Visuals", "☀", "Field of view, fog, light, HUD and a free camera. Only your screen changes.");

            tab.Switch("vis.fov", "Field of view", "Wider than the game's settings allow.", Scope.Client, () => Fov, v => Fov = v)
                .WithBody(() => Settings.SetFloat("vis.fov", Ui.Slider("Degrees", FovValue, 40f, 150f, "0", "°")));

            tab.Switch("vis.nofog", "No fog", "Removes the haze and distance fog so you can see the whole mountain.", Scope.Client, () => NoFog, v => NoFog = v);

            tab.Switch("vis.bright", "Fullbright", "Lights everything up, even at night and in caves.", Scope.Client, () => Fullbright, v => Fullbright = v)
                .WithBody(() => Settings.SetFloat("vis.bright", Ui.Slider("Brightness", Brightness, 0.3f, 3f, "0.0")));

            tab.Switch("vis.day", "Always daytime (just you)", "Your game keeps the sun up. Others keep their own time.", Scope.Client, () => AlwaysDay, v => AlwaysDay = v);

            tab.Switch("vis.hud", "Hide the HUD", "Clean screen for screenshots.", Scope.Client, () => HideHud, v => HideHud = v);

            tab.Switch("vis.freecam", "Free camera", "The game's own developer camera: WASD, mouse, Space/E up, Ctrl/Q down, Shift faster. Your scout stands still.", Scope.Client,
                () => Freecam, v =>
                {
                    Freecam = v;
                    var mcm = Singleton<MainCameraMovement>.Instance;

                    if (mcm)
                    {
                        mcm.isGodCam = v;
                    }
                });
        }

        // after the game's camera has set its own field of view for this frame
        public static void AfterCamera()
        {
            if (!Fov)
            {
                return;
            }

            var cam = Game.Camera;

            if (cam)
            {
                cam.fieldOfView = FovValue;
            }
        }

        public static void LateTick()
        {
            if (NoFog)
            {
                Shader.SetGlobalFloat("USEFOG", 0f);
                Shader.SetGlobalFloat("EXTRAFOG", 0f);
                Shader.SetGlobalFloat("HeightFogAmount", 0f);
                RenderSettings.fog = false;
            }

            if (Fullbright)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.white * Brightness;
            }

            if (AlwaysDay && DayNightManager.instance)
            {
                DayNightManager.instance.timeOfDay = 12f;
            }

            if (HideHud != hudHidden && GUIManager.instance && GUIManager.instance.hudCanvas)
            {
                GUIManager.instance.hudCanvas.enabled = !HideHud;
                hudHidden = HideHud;
            }

            // the free camera switches itself off when a level loads; keep the menu's switch honest
            if (Freecam)
            {
                var mcm = Singleton<MainCameraMovement>.Instance;

                if (mcm && !mcm.isGodCam)
                {
                    mcm.isGodCam = true;
                }
            }
        }
    }
}
