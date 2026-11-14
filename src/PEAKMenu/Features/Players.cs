using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using PEAKMenu.Core;
using PEAKMenu.UI;
using UnityEngine;

namespace PEAKMenu.Features
{
    // Other scouts: help them or mess with them. Every action here is a message the game itself
    // sends between players and that the receiving game accepts from anyone (checked in the 3.1
    // code: no host check on any of these), so they work when you are a guest too. The game does
    // refuse a plain kill or warp from a guest, so "hurt" and "bring" go the long way round.
    internal static class Players
    {
        private static Character selected;
        public static Character Selected => selected;
        private static readonly Ui.Picker<Character> picker = new();
        private static bool includeMe;

        private static CharacterAfflictions.STATUSTYPE statusType = CharacterAfflictions.STATUSTYPE.Poison;
        private static readonly HashSet<int> pinned = new();

        public static bool LoopKnock, LoopLaunch, LoopRocket, LoopHover;
        private static float nextKnock, nextLaunch, nextRocket, nextHover;

        private static float LaunchForce => Settings.Float("troll.launch", 350f);
        private static float KnockSeconds => Settings.Float("troll.knock", 4f);
        private static float StatusAmount => Settings.Float("troll.amount", 0.5f);

        private static readonly CharacterAfflictions.STATUSTYPE[] StatusChoices =
        {
            CharacterAfflictions.STATUSTYPE.Poison, CharacterAfflictions.STATUSTYPE.Injury, CharacterAfflictions.STATUSTYPE.Cold,
            CharacterAfflictions.STATUSTYPE.Hot, CharacterAfflictions.STATUSTYPE.Hunger, CharacterAfflictions.STATUSTYPE.Drowsy,
            CharacterAfflictions.STATUSTYPE.Spores, CharacterAfflictions.STATUSTYPE.Curse, CharacterAfflictions.STATUSTYPE.Web,
            CharacterAfflictions.STATUSTYPE.Crab, CharacterAfflictions.STATUSTYPE.Petrify
        };

        public static void Register()
        {
            var tab = Registry.Tab("Players", "☺", "Pick a scout, then help them or troll them. Everything here works as a guest: the game accepts these messages from any player.");

            tab.Panel("players.pick", "Scouts in this lobby", "Click one to pick it. Green = alive, grey = dead, orange = passed out.", Scope.Client, () =>
            {
                var list = Game.Players;
                var me = Game.LocalPosition;
                var clicked = picker.Draw(list, c => Label(c), 150f,
                    c => c.IsLocal ? "you" : $"{Vector3.Distance(me, Game.Where(c)):0} m",
                    c => !Game.IsDead(c) && !Game.IsDown(c));

                if (clicked)
                {
                    selected = clicked;
                }

                if (selected && !list.Contains(selected))
                {
                    selected = null;
                }

                GUILayout.Label(selected ? $"Picked: <b>{Game.Name(selected)}</b>" : "<color=#E8A860>Nobody picked yet</color>", Theme.Label);
            }).When(SelfMods.NeedWorld);

            tab.Panel("players.help", "Help", "Friendly things for the picked scout.", Scope.Client, () =>
            {
                GUILayout.BeginHorizontal();
                Btn("Revive", () => Revive(selected));
                Btn("Bring to me", () => Bring(selected, true));
                Btn("Teleport to them", () => Teleport.To(Game.Where(selected) + Vector3.up * 1.5f, Game.Name(selected)));
                Btn("Wake up", () => WakeUp(selected));
                Btn("Free (unpin)", () => Unpin(selected));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                Ui.Hint("Bring to me uses the game's revive-and-move, the only move a guest may send: they arrive healed, but they drop what they carry.");
            }).When(NeedPick);

            tab.Panel("players.troll", "Troll", "Mess with the picked scout.", Scope.Client, () =>
            {
                GUILayout.BeginHorizontal();
                Btn("Knock over", () => KnockOver(selected, KnockSeconds));
                Btn("Launch up", () => Launch(selected, Vector3.up));
                Btn("Fling", () => Launch(selected, RandomFling()));
                Btn("Rocket", () => Rocket(selected));
                Btn("Big jump", () => Net.Rpc(selected.view, "JumpRpc", RpcTarget.All, true));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                Btn("Pass out", () => PassOut(selected));
                Btn("Zombify", () => Zombify(selected));
                Btn("Pin in place", () => Pin(selected));
                Btn("Throw their item", () => ThrowHeldItem(selected));
                Btn("Carry them", () => Carry(selected));
                Btn("Let go", () => Drop(selected));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                Btn("Explode", () => Explode(selected));
                Btn("Banana slip", () => Slip(selected));
                Btn("Play dead", () => Net.Rpc(selected.view, "RPCA_PlayRemove", RpcTarget.All, "A_Scout_Emote_PlayDead", true));
                Btn("Backflip", () => Net.Rpc(selected.view, "RPCA_PlayRemove", RpcTarget.All, "A_Scout_Emote_BackFlip", true));
                Btn("Force crouch", () => Net.Rpc(selected.view, "RPCA_SetCrouch", RpcTarget.All, true));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                Btn("Scoutmaster hunts them", () => ScoutmasterTarget(selected));
                Btn("Scoutmaster throws them", () => ScoutmasterThrow(selected));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                Settings.SetFloat("troll.knock", Ui.Slider("Knock over for", KnockSeconds, 1f, 30f, "0", " s"));
                Settings.SetFloat("troll.launch", Ui.Slider("Launch strength", LaunchForce, 50f, 2500f, "0"));
                Ui.Hint("Pass out makes them faint and drop their stuff. Zombify turns them into a mushroom zombie (the host's game spawns it). Pin glues them where they stand until you free them.");
            }).When(NeedPick);

            tab.Panel("players.status", "Give a status", "Adds a status to the picked scout's bar on their own machine. Enough injury, poison or curse and they pass out.", Scope.Client, () =>
            {
                int idx = System.Array.IndexOf(StatusChoices, statusType);
                int pick = Ui.ButtonGrid(StatusChoices.Select(s => s.ToString()).ToList(), idx, 74, 560);

                if (pick >= 0)
                {
                    statusType = StatusChoices[pick];
                }

                Settings.SetFloat("troll.amount", Ui.Slider("Amount", StatusAmount, 0.05f, 2f, "0.00"));
                GUILayout.BeginHorizontal();
                Btn($"Give {statusType}", () => GiveStatus(selected, statusType, StatusAmount));
                Btn("Full injury (down)", () => GiveStatus(selected, CharacterAfflictions.STATUSTYPE.Injury, 2f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }).When(NeedPick);

            tab.Switch("troll.loopknock", "Keep knocking them over", "Knocks the picked scout over again every few seconds.", Scope.Client, () => LoopKnock, v => LoopKnock = v).When(NeedPick);
            tab.Switch("troll.looplaunch", "Keep launching them", "Launches the picked scout up every 3 seconds.", Scope.Client, () => LoopLaunch, v => LoopLaunch = v).When(NeedPick);
            tab.Switch("troll.looprocket", "Rocket spam", "Rockets the picked scout again every 4 seconds.", Scope.Client, () => LoopRocket, v => LoopRocket = v).When(NeedPick);
            tab.Switch("troll.hover", "Make them float", "Keeps a little jetpack push on the picked scout so they drift up.", Scope.Client, () => LoopHover, v => LoopHover = v).When(NeedPick);

            tab.Panel("players.all", "Everyone at once", "The same actions on every scout in the lobby.", Scope.Client, () =>
            {
                includeMe = Ui.SwitchRow("Include me", includeMe);
                GUILayout.BeginHorizontal();
                Btn("Revive all", () => ForAll(Revive, true), false);
                Btn("Bring all to me", () => ForAll(c => Bring(c, false), false), false);
                Btn("Wake all", () => ForAll(WakeUp, true), false);
                Btn("Free all", () => ForAll(Unpin, true), false);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                Btn("Knock all over", () => ForAll(c => KnockOver(c, KnockSeconds)), false);
                Btn("Launch all", () => ForAll(c => Launch(c, Vector3.up)), false);
                Btn("Fling all", () => ForAll(c => Launch(c, RandomFling())), false);
                Btn("Rocket all", () => ForAll(Rocket), false);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                Btn("Pass out all", () => ForAll(PassOut), false);
                Btn("Zombify all", () => ForAll(Zombify), false);
                Btn("Pin all", () => ForAll(Pin), false);
                Btn($"Give all {statusType}", () => ForAll(c => GiveStatus(c, statusType, StatusAmount)), false);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }).When(SelfMods.NeedWorld);
        }

        private static string NeedPick()
        {
            if (!Game.InWorld)
            {
                return "Not in a run";
            }

            return selected ? null : "Pick a scout in the list above first";
        }

        private static string Label(Character c)
        {
            var n = Game.Name(c) + (c.IsLocal ? " (you)" : "");

            if (Game.IsDead(c))
            {
                return "<color=#9AA0A8>" + n + "  dead</color>";
            }

            if (Game.IsDown(c))
            {
                return "<color=#E8A860>" + n + "  passed out</color>";
            }

            return n;
        }

        private static void Btn(string text, System.Action act, bool needsPick = true)
        {
            if (Ui.Small(text))
            {
                if (needsPick && !selected)
                {
                    Toasts.Show("Pick a scout first");
                    return;
                }

                Safe.Run("players." + text, act);

                if (needsPick)
                {
                    Toasts.Show($"{text}: {Game.Name(selected)}");
                }
                else
                {
                    Toasts.Show(text);
                }
            }
        }

        private static void ForAll(System.Action<Character> act, bool alwaysIncludeMe = false)
        {
            foreach (var c in Game.Players.ToList())
            {
                if (c.IsLocal && !includeMe && !alwaysIncludeMe)
                {
                    continue;
                }

                var target = c;
                Safe.Run("players.all", () => act(target));
            }
        }

        private static Vector3 RandomFling()
        {
            var d = Random.onUnitSphere;
            d.y = Mathf.Abs(d.y) + 0.6f;
            return d.normalized;
        }

        private static Vector3 Hip(Character c) => Safe.Get("players.hip", () => c.GetBodypart(BodypartType.Hip).transform.position, Game.Where(c));

        // ---- the actions; all public so other tabs and binds can use them ----

        public static void Revive(Character c)
        {
            if (!c)
            {
                return;
            }

            var at = (c.Ghost ? c.Ghost.transform.position : Game.Where(c)) + Vector3.up * 1.5f;
            Net.Rpc(c.view, "RPCA_ReviveAtPosition", RpcTarget.All, at, false, -1);
        }

        // the game's revive-at-position also moves a living scout; it is the one move it accepts from a guest
        public static void Bring(Character c, bool toast)
        {
            if (!c || c.IsLocal)
            {
                return;
            }

            var me = Game.Local;
            var fwd = me ? me.data.lookDirection_Flat.normalized : Vector3.forward;
            var at = Game.LocalPosition + fwd * 2f + Vector3.up * 1.5f;
            Net.Rpc(c.view, "RPCA_ReviveAtPosition", RpcTarget.All, at, false, -1);
        }

        public static void WakeUp(Character c)
        {
            if (c)
            {
                Net.Rpc(c.view, "RPCA_UnPassOut", RpcTarget.All);
            }
        }

        public static void PassOut(Character c)
        {
            if (c && !Game.IsDead(c))
            {
                Net.Rpc(c.view, "RPCA_PassOut", RpcTarget.All);
            }
        }

        public static void Zombify(Character c)
        {
            if (c && !Game.IsDead(c))
            {
                Net.Rpc(c.view, "RPCA_Zombify", RpcTarget.All);
            }
        }

        public static void KnockOver(Character c, float seconds)
        {
            if (c && !Game.IsDead(c))
            {
                Net.Rpc(c.view, "RPCA_Fall", RpcTarget.All, seconds, 0f);
            }
        }

        public static void Launch(Character c, Vector3 direction)
        {
            if (!c || Game.IsDead(c))
            {
                return;
            }

            // ragdoll them first so the push isn't fought by their legs
            Net.Rpc(c.view, "RPCA_Fall", RpcTarget.All, 2f, 0f);
            Net.Rpc(c.view, "RPCA_AddForceAtPosition", RpcTarget.All, direction.normalized * LaunchForce, Hip(c), 5f);
        }

        public static void Rocket(Character c)
        {
            if (c && !Game.IsDead(c))
            {
                Net.Rpc(c.view, "RocketRPC", RpcTarget.All, 0);
            }
        }

        // the game's sticky-hazard message: glues a body part where it is and can add a status with it
        public static void GiveStatus(Character c, CharacterAfflictions.STATUSTYPE type, float amount)
        {
            if (!c || Game.IsDead(c))
            {
                return;
            }

            var hip = Hip(c);
            Net.Rpc(c.view, "RPCA_Stick", RpcTarget.All, BodypartType.Hip, hip, hip, type, amount);

            if (!pinned.Contains(c.view.ViewID))
            {
                Net.Rpc(c.view, "RPCA_Unstick", RpcTarget.All);
            }
        }

        public static void Pin(Character c)
        {
            if (!c || Game.IsDead(c))
            {
                return;
            }

            var hip = Hip(c);
            Net.Rpc(c.view, "RPCA_Stick", RpcTarget.All, BodypartType.Hip, hip, hip, CharacterAfflictions.STATUSTYPE.Web, 0f);
            pinned.Add(c.view.ViewID);
        }

        public static void Unpin(Character c)
        {
            if (!c)
            {
                return;
            }

            Net.Rpc(c.view, "RPCA_Unstick", RpcTarget.All);
            pinned.Remove(c.view.ViewID);
        }

        public static void ThrowHeldItem(Character c)
        {
            if (!c || !c.data.currentItem || !c.refs.items.currentSelectedSlot.IsSome)
            {
                Toasts.Show("They aren't holding anything");
                return;
            }

            byte slot = c.refs.items.currentSelectedSlot.Value;
            var item = c.data.currentItem;
            var data = c.player.GetItemSlot(slot).data;
            var dir = (Random.onUnitSphere + Vector3.up).normalized;
            Net.Rpc(c.refs.items.photonView, "DropItemRpc", RpcTarget.All, 1f, slot, item.transform.position + Vector3.up * 0.5f, dir * 25f, item.transform.rotation, data, false);
        }

        public static void Carry(Character c)
        {
            var me = Game.Local;

            if (!c || !me || c.IsLocal)
            {
                return;
            }

            Net.Rpc(me.view, "RPCA_StartCarry", RpcTarget.All, c.view);
        }

        public static void Drop(Character c)
        {
            var me = Game.Local;

            if (!c || !me)
            {
                return;
            }

            Net.Rpc(me.view, "RPCA_Drop", RpcTarget.All, c.view);
        }

        // a stick of dynamite made at their feet, set off at once: the blast runs on every game,
        // and each game hurts and knocks over its own scout if they are close
        public static void Explode(Character c)
        {
            if (!c)
            {
                return;
            }

            var dyn = Items.Find<Dynamite>();

            if (!dyn)
            {
                Toasts.Show("No dynamite in this game's item list");
                return;
            }

            var go = Items.Spawn(dyn, Game.Where(c) + Vector3.down * 0.5f);

            if (go)
            {
                Net.Rpc(go.GetComponent<PhotonView>(), "RPC_Explode", RpcTarget.All);
            }
        }

        // a banana peel under them, stepped on by them
        public static void Slip(Character c)
        {
            if (!c)
            {
                return;
            }

            var peel = Items.Find<BananaPeel>();

            if (!peel)
            {
                Toasts.Show("No banana peel in this game's item list");
                return;
            }

            var go = Items.Spawn(peel, Hip(c) + Vector3.down * 0.8f);

            if (go)
            {
                Net.Rpc(go.GetComponent<PhotonView>(), "RPCA_TriggerBanana", RpcTarget.All, c.view.ViewID);
            }
        }

        public static void ScoutmasterThrow(Character c)
        {
            if (!Scoutmaster.GetPrimaryScoutmaster(out var sm) || !sm)
            {
                Toasts.Show("There's no Scoutmaster in this level right now");
                return;
            }

            Net.Rpc(sm.view, "RPCA_SetCurrentTarget", RpcTarget.All, c.view.ViewID, 30f);
            Net.Rpc(sm.view, "RPCA_Throw", RpcTarget.All);
        }

        public static void ScoutmasterTarget(Character c)
        {
            if (!Scoutmaster.GetPrimaryScoutmaster(out var sm) || !sm)
            {
                Toasts.Show("There's no Scoutmaster in this level right now (World tab can spawn one as host)");
                return;
            }

            Net.Rpc(sm.view, "RPCA_SetCurrentTarget", RpcTarget.All, c.view.ViewID, 30f);
        }

        public static void Tick()
        {
            if (!Game.InWorld || !selected)
            {
                return;
            }

            float t = Time.unscaledTime;

            if (LoopKnock && t > nextKnock)
            {
                nextKnock = t + Mathf.Max(1.5f, KnockSeconds * 0.8f);
                Safe.Run("troll.loopknock", () => KnockOver(selected, KnockSeconds));
            }

            if (LoopLaunch && t > nextLaunch)
            {
                nextLaunch = t + 3f;
                Safe.Run("troll.looplaunch", () => Launch(selected, Vector3.up));
            }

            if (LoopRocket && t > nextRocket)
            {
                nextRocket = t + 4f;
                Safe.Run("troll.looprocket", () => Rocket(selected));
            }

            if (LoopHover && t > nextHover)
            {
                nextHover = t + 0.1f;
                // only their own game runs the push; sent to everyone it makes bystanders' games read a
                // jetpack the target may not have
                Safe.Run("troll.hover", () => Net.RpcTo(selected.view, "JetpackRPC", selected.view.Owner, 50f));
            }
        }
    }
}
