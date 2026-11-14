using System;
using System.Linq;
using Photon.Pun;
using PEAKMenu.Core;
using PEAKMenu.UI;
using UnityEngine;
using Zorro.Core;

namespace PEAKMenu.Features
{
    // The run itself: luggage, campfires, the summit, the Scoutmaster, bees, the end. The client
    // cards use messages the game accepts from anyone; the host cards use the host's own controls
    // (jumping between areas, time of day, the fog) and lock when you are a guest.
    internal static class World
    {
        private static float armedUntil;
        private static string armed;
        private static bool fogPaused;
        private static float fogSpeed = -1f;
        private static int maxPlayers = 8;

        public static void Register()
        {
            var tab = Registry.Tab("World", "⛰", "Luggage, campfires, the summit, the Scoutmaster and the run itself. Green cards work as a guest, orange ones need you to be host.");

            tab.Action("world.openall", "Open all luggage", "Opens every closed suitcase, chest and basket in the level. The host's game drops the loot.", Scope.Client, "Open all", () =>
            {
                int n = 0;

                foreach (var l in WorldCache.Bags.ToList())
                {
                    if (l && !(l is RespawnChest))
                    {
                        Net.Rpc(l.photonView, "OpenLuggageRPC", RpcTarget.All, true);
                        n++;
                    }
                }

                Toasts.Show($"Opened {n} luggage");
            }).When(SelfMods.NeedWorld);

            tab.Action("world.opennear", "Open nearby luggage", "Opens the luggage within 20 m of you.", Scope.Client, "Open", () =>
            {
                int n = 0;

                foreach (var l in WorldCache.Bags.ToList())
                {
                    if (l && !(l is RespawnChest) && Vector3.Distance(l.Center(), Game.LocalPosition) < 20f)
                    {
                        Net.Rpc(l.photonView, "OpenLuggageRPC", RpcTarget.All, true);
                        n++;
                    }
                }

                Toasts.Show(n == 0 ? "No closed luggage within 20 m" : $"Opened {n} luggage");
            }).When(SelfMods.NeedWorld);

            tab.Action("world.tpcampfire", "Teleport to this area's campfire", "Takes you to the campfire at the top of the area you're in.", Scope.Client, "Go", () =>
            {
                var cf = MapHandler.CurrentCampfire;

                if (cf)
                {
                    Teleport.To(cf.transform.position + Vector3.up * 2f + Vector3.forward * 2f, "the campfire");
                }
                else
                {
                    Toasts.Show("No campfire found for this area");
                }
            }).When(SelfMods.NeedWorld);

            tab.Action("world.tppeak", "Teleport to the summit", "Takes you to the very top of the mountain.", Scope.Client, "Go", () =>
            {
                var mph = Singleton<MountainProgressHandler>.Instance;
                var top = mph && mph.progressPoints != null && mph.progressPoints.Length > 0 ? mph.progressPoints.Last().transform : null;

                if (top)
                {
                    Teleport.To(top.position + Vector3.up * 2f, "the summit");
                }
                else
                {
                    Toasts.Show("Couldn't find the summit in this level");
                }
            }).When(SelfMods.NeedWorld);

            tab.Action("world.light", "Light this area's campfire", "Lights the campfire for everyone and moves the whole run on to the next area, just like resting there. Anyone can send this.", Scope.Client, "Light", () =>
            {
                var cf = MapHandler.CurrentCampfire;

                if (!cf)
                {
                    Toasts.Show("No campfire found for this area");
                    return;
                }

                cf.DebugLight();
                Toasts.Show("Campfire lit: on to the next area");
            }).When(SelfMods.NeedWorld);

            tab.Action("world.putout", "Put out every campfire", "Douses all lit campfires for everyone.", Scope.Client, "Put out", () =>
            {
                int n = 0;

                foreach (var cf in WorldCache.Campfires)
                {
                    if (cf && cf.state == Campfire.FireState.Lit)
                    {
                        Net.Rpc(cf.view, "Extinguish_Rpc", RpcTarget.AllBuffered);
                        n++;
                    }
                }

                Toasts.Show(n == 0 ? "No lit campfire" : $"Put out {n} campfire(s)");
            }).When(SelfMods.NeedWorld);

            tab.Action("world.bees", "Anger every bee swarm", "Every beehive's bees go angry and chase whoever is near.", Scope.Client, "Anger", () =>
            {
                int n = 0;

                foreach (var b in WorldCache.Beehives)
                {
                    if (b && b.currentBees)
                    {
                        Net.Rpc(b.currentBees.photonView, "SetBeesAngryRPC", RpcTarget.AllBuffered, true);
                        n++;
                    }
                }

                Toasts.Show(n == 0 ? "No bees around" : $"Angered {n} swarm(s)");
            }).When(SelfMods.NeedWorld);

            tab.Action("world.win", "Win the run", "Ends the run as a win for everyone (the game's developer win). Anyone can send this.", Scope.Client, "Win", () => Confirm("win", () =>
            {
                Character.TestWin();
                Toasts.Show("Run won");
            })).When(SelfMods.NeedWorld);

            tab.Action("world.end", "End the run", "Ends the run for everyone, as if all scouts died. The biggest troll there is.", Scope.Client, "End", () => Confirm("end", () =>
            {
                Net.Rpc(Game.Local.photonView, "RPCEndGame", RpcTarget.All);
                Toasts.Show("Run ended");
            })).When(SelfMods.NeedWorld);

            // ---- host ----

            tab.Panel("world.segment", "Jump to an area", "Moves everyone to the start of that area, the game's own developer jump.", Scope.Host, () =>
            {
                GUILayout.BeginHorizontal();

                foreach (Segment s in Enum.GetValues(typeof(Segment)))
                {
                    if (Ui.Small(s.ToString()))
                    {
                        var seg = s;
                        Safe.Run("world.segment", () => MapHandler.JumpToSegment(seg));
                        Toasts.Show("Jumping to " + seg);
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }).When(SelfMods.NeedWorld);

            tab.Panel("world.time", "Time of day", "Sets the time for everyone (the host's clock is the one that counts).", Scope.Host, () =>
            {
                var dn = DayNightManager.instance;

                if (!dn)
                {
                    Ui.Hint("No day and night in this level.");
                    return;
                }

                float now = Ui.Slider("Hour", dn.timeOfDay % 24f, 0f, 24f, "0.0", " h");

                if (Mathf.Abs(now - dn.timeOfDay % 24f) > 0.05f)
                {
                    DayNightManager.SetTimeOfDay(now);
                }

                GUILayout.BeginHorizontal();

                if (Ui.Small("Morning", 80))
                {
                    DayNightManager.SetTimeOfDay(7f);
                }

                if (Ui.Small("Noon", 80))
                {
                    DayNightManager.SetTimeOfDay(12f);
                }

                if (Ui.Small("Night", 80))
                {
                    DayNightManager.SetTimeOfDay(23f);
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }).When(SelfMods.NeedWorld);

            tab.Switch("world.fog", "Stop the fog", "The rising fog stops where it is (it starts again when you turn this off).", Scope.Host, () => fogPaused, v =>
            {
                fogPaused = v;
                var fog = Singleton<OrbFogHandler>.Instance;

                if (!fog)
                {
                    return;
                }

                if (v)
                {
                    if (fogSpeed < 0f)
                    {
                        fogSpeed = fog.speed;
                    }

                    fog.speed = 0f;
                }
                else if (fogSpeed >= 0f)
                {
                    fog.speed = fogSpeed;
                    fogSpeed = -1f;
                }
            });

            tab.Action("world.scoutmaster", "Spawn the Scoutmaster", "Spawns him near the scout picked in the Players tab (or near you).", Scope.Host, "Spawn", SpawnScoutmaster).When(SelfMods.NeedWorld);

            tab.Panel("world.maxplayers", "Lobby size", "How many scouts can join your lobby (the game normally allows 4).", Scope.Host, () =>
            {
                maxPlayers = Ui.IntSlider("Max scouts", maxPlayers, 1, 20);

                if (Ui.Small("Set", 60))
                {
                    var n = maxPlayers;
                    Safe.Run("world.maxplayers", () => Peak.Network.NetworkingUtilities.SetMaxPlayers(n));
                    Toasts.Show($"Lobby now takes {n} scouts");
                }
            });
        }

        // big, run-ending buttons need a second click within 4 seconds
        private static void Confirm(string id, Action act)
        {
            if (armed == id && Time.unscaledTime < armedUntil)
            {
                armed = null;
                act();
                return;
            }

            armed = id;
            armedUntil = Time.unscaledTime + 4f;
            Toasts.Show("<color=#E8A860>Click again within 4 seconds to confirm</color>");
        }

        private static void SpawnScoutmaster()
        {
            var near = Players.Selected ? Players.Selected : Game.Local;
            var origin = Game.Where(near) + new Vector3(UnityEngine.Random.Range(-10f, 10f), 25f, UnityEngine.Random.Range(-10f, 10f));
            var at = Physics.Raycast(origin, Vector3.down, out var hit, 100f) ? hit.point + Vector3.up : Game.Where(near) + Vector3.forward * 5f;
            var go = PhotonNetwork.InstantiateRoomObject("Character_Scoutmaster", at, Quaternion.identity, 0);

            if (go)
            {
                var c = go.GetComponent<Character>();

                if (c)
                {
                    c.data.spawnPoint = c.transform;
                }

                Toasts.Show("The Scoutmaster is here");
            }
        }
    }
}
