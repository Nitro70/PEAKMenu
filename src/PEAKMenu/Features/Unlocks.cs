using System;
using PEAKMenu.Core;
using PEAKMenu.UI;
using UnityEngine;

namespace PEAKMenu.Features
{
    // Cosmetics, badges and ascents. Cosmetics can be opened on this machine only (nothing saved).
    // Badges and ascents are real Steam achievements and stats on your account: those buttons say
    // so and need a second click.
    internal static class Unlocks
    {
        private static float armedUntil;
        private static string armed;
        private static int ascent = 8;

        public static void Register()
        {
            var tab = Registry.Tab("Unlocks", "★", "Cosmetics, badges and ascents.");

            tab.Switch("unlock.cosmetics", "All cosmetics (this session)", "Every hat, outfit and colour shows as unlocked in the passport while the game runs. Nothing is written to Steam.", Scope.Client,
                () => PassportManager.instance && PassportManager.instance.testUnlockAll,
                v =>
                {
                    if (PassportManager.instance)
                    {
                        PassportManager.instance.testUnlockAll = v;
                    }
                    else
                    {
                        Toasts.Show("Open the passport once (or load into the airport) first");
                    }
                });

            tab.Action("unlock.badges", "Unlock every badge", "Grants every badge as a real Steam achievement on your account. Permanent: Steam keeps it.", Scope.Client, "Unlock", () => Confirm("badges", () =>
            {
                AchievementManager.UnlockAll();
                Toasts.Show("Badges granted");
            }));

            tab.Panel("unlock.ascent", "Ascent level", "Unlocks ascents up to this level (8 also unlocks the Goat hat). Saved to your Steam stats.", Scope.Client, () =>
            {
                ascent = Ui.IntSlider("Up to", ascent, 1, 9);

                if (Ui.Small("Unlock", 70))
                {
                    var level = ascent;
                    Confirm("ascent", () =>
                    {
                        AchievementManager.GiveAscentLevel(level);
                        Toasts.Show($"Ascents unlocked up to {level}");
                    });
                }
            });
        }

        private static void Confirm(string id, Action act)
        {
            if (armed == id && Time.unscaledTime < armedUntil)
            {
                armed = null;
                Safe.Run("unlock." + id, act);
                return;
            }

            armed = id;
            armedUntil = Time.unscaledTime + 4f;
            Toasts.Show("<color=#E8A860>This writes to your Steam account. Click again within 4 seconds to confirm.</color>");
        }
    }
}
