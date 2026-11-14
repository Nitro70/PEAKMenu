using System;
using System.Linq;
using PEAKMenu.Core;
using PEAKMenu.UI;
using UnityEngine;

namespace PEAKMenu.Features
{
    // Things that change only your own scout. PEAK lets every player own their stamina, statuses
    // and body, so all of these work in anyone's lobby.
    internal static class SelfMods
    {
        public static bool InfiniteStamina, God, LockStatuses, NoWeight, NoHunger, NoTemperature, NoFallDamage, NoFallOver, AutoRevive, NeverPassOut;

        private static float nextStatusTick;
        private static float fallDamageDefault = -1f;
        private static float nextRevive;
        private static bool hadStamina, hadLock;

        private static readonly CharacterAfflictions.STATUSTYPE[] Bad =
        {
            CharacterAfflictions.STATUSTYPE.Injury, CharacterAfflictions.STATUSTYPE.Hunger, CharacterAfflictions.STATUSTYPE.Cold,
            CharacterAfflictions.STATUSTYPE.Poison, CharacterAfflictions.STATUSTYPE.Curse, CharacterAfflictions.STATUSTYPE.Drowsy,
            CharacterAfflictions.STATUSTYPE.Hot, CharacterAfflictions.STATUSTYPE.Spores, CharacterAfflictions.STATUSTYPE.Web,
            CharacterAfflictions.STATUSTYPE.Crab, CharacterAfflictions.STATUSTYPE.Petrify, CharacterAfflictions.STATUSTYPE.FlyTrap
        };

        public static void Register()
        {
            var tab = Registry.Tab("Self", "♥", "Your own scout: stamina, statuses, damage. These change only you, so they work in any lobby.");

            tab.Switch("self.stamina", "Infinite stamina", "Climb, sprint and jump forever. Uses the game's own developer switch.", Scope.Client,
                () => InfiniteStamina, v =>
                {
                    InfiniteStamina = v;

                    if (v && Game.InWorld)
                    {
                        Game.Local.data.currentStamina = 1f;
                    }
                });

            tab.Switch("self.god", "God mode", "Nothing bad can be added to you (injury, hunger, cold, heat, poison, spores, curse...) and anything already there is cleared. Food and healing still work.", Scope.Client,
                () => God, v => God = v);

            tab.Switch("self.lock", "Freeze statuses", "Your status bar stays exactly as it is now, good or bad. The game's developer lock.", Scope.Client,
                () => LockStatuses, v => LockStatuses = v);

            tab.Switch("self.noweight", "No weight", "Carrying things never slows you or fills your bar.", Scope.Client, () => NoWeight, v => NoWeight = v);
            tab.Switch("self.nohunger", "Never hungry", "Hunger stays at zero.", Scope.Client, () => NoHunger, v => NoHunger = v);
            tab.Switch("self.notemp", "No cold or heat", "Snow and desert sun do nothing to you.", Scope.Client, () => NoTemperature, v => NoTemperature = v);
            tab.Switch("self.nofall", "No fall damage", "Land from any height without getting hurt.", Scope.Client, () => NoFallDamage, v => NoFallDamage = v);
            tab.Switch("self.nofallover", "Never fall over", "You don't ragdoll from big falls, hits or slips.", Scope.Client, () => NoFallOver, v => NoFallOver = v);
            tab.Switch("self.nopassout", "Never pass out", "If you would pass out, you get straight back up.", Scope.Client, () => NeverPassOut, v => NeverPassOut = v);
            tab.Switch("self.autorevive", "Auto revive", "Come straight back to life where you died.", Scope.Client, () => AutoRevive, v => AutoRevive = v);

            tab.Action("self.heal", "Heal fully", "Clears every bad status and affliction (thorns and arrows stay, pull them out normally) and fills stamina.", Scope.Client, "Heal", Heal)
                .When(NeedWorld);
            tab.Action("self.revive", "Revive yourself", "Come back to life where your ghost is.", Scope.Client, "Revive", () => Players.Revive(Game.Local))
                .When(() => !Game.InWorld ? "Not in a run" : !Game.IsDead(Game.Local) ? "You are alive" : null);
            tab.Action("self.passout", "Pass out", "Drop to the ground like you fainted.", Scope.Client, "Pass out", () => Players.PassOut(Game.Local)).When(NeedWorld);
            tab.Action("self.zombie", "Turn into a zombie", "The game's own zombify: you become a mushroom zombie.", Scope.Client, "Zombify", () => Players.Zombify(Game.Local)).When(NeedWorld);

            tab.Panel("self.status", "Set a status exactly", "Drag a bar to set that status on yourself (0 = none, 1 = the whole bar).", Scope.Client, StatusPanel).When(NeedWorld);
        }

        public static string NeedWorld() => Game.InWorld ? null : "Not in a run";

        private static void StatusPanel()
        {
            var aff = Game.InWorld ? Game.Local.refs.afflictions : null;

            if (!aff)
            {
                return;
            }

            foreach (CharacterAfflictions.STATUSTYPE t in Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE)))
            {
                float cur = aff.GetCurrentStatus(t);
                float now = Ui.Slider(t.ToString(), cur, 0f, 1f, "0.00");

                if (!Mathf.Approximately(now, cur))
                {
                    aff.SetStatus(t, now, true);
                }
            }
        }

        public static void Heal()
        {
            var c = Game.Local;
            var aff = c.refs.afflictions;
            int removed = 0;

            var list = aff.afflictionList?.ToArray();

            if (list != null)
            {
                for (int i = list.Length - 1; i >= 0; i--)
                {
                    var a = list[i];

                    if (a != null && aff.afflictionList.Contains(a) && IsNegative(a))
                    {
                        aff.RemoveAffliction(a, false, false);
                        removed++;
                    }
                }

                if (removed > 0)
                {
                    aff.PushAfflictions();
                }
            }

            aff.ClearAllStatus(false, false);
            c.data.SetPetrify(0);
            c.data.currentStamina = 1f;
            Toasts.Show("Healed" + (removed > 0 ? $", removed {removed} affliction(s)" : ""));
        }

        private static bool IsNegative(Peak.Afflictions.Affliction a)
        {
            switch (a.GetAfflictionType())
            {
                case Peak.Afflictions.Affliction.AfflictionType.PoisonOverTime:
                case Peak.Afflictions.Affliction.AfflictionType.Exhausted:
                case Peak.Afflictions.Affliction.AfflictionType.Chaos:
                case Peak.Afflictions.Affliction.AfflictionType.PreventPoisonHealing:
                case Peak.Afflictions.Affliction.AfflictionType.ZombieBite:
                case Peak.Afflictions.Affliction.AfflictionType.Blind:
                case Peak.Afflictions.Affliction.AfflictionType.Numb:
                    return true;
                case Peak.Afflictions.Affliction.AfflictionType.ColdOverTime:
                    return a is Peak.Afflictions.Affliction_AdjustColdOverTime c && c.statusPerSecond > 0f;
                case Peak.Afflictions.Affliction.AfflictionType.DrowsyOverTime:
                    return a is Peak.Afflictions.Affliction_AdjustDrowsyOverTime d && d.statusPerSecond > 0f;
                case Peak.Afflictions.Affliction.AfflictionType.AdjustStatusOverTime:
                    return a is Peak.Afflictions.Affliction_AdjustStatusOverTime o && o.statusPerSecond > 0f;
                default:
                    return false;
            }
        }

        public static void Tick()
        {
            if (!Game.InWorld)
            {
                return;
            }

            var c = Game.Local;

            // the game's own switches: set while on, handed back once when turned off
            if (InfiniteStamina)
            {
                c.infiniteStam = true;
                hadStamina = true;
            }
            else if (hadStamina)
            {
                c.infiniteStam = false;
                hadStamina = false;
            }

            if (LockStatuses)
            {
                c.statusesLocked = true;
                hadLock = true;
            }
            else if (hadLock)
            {
                c.statusesLocked = false;
                hadLock = false;
            }

            var mv = c.refs.movement;

            if (mv)
            {
                if (fallDamageDefault < 0f && mv.fallDamageTime < 9000f)
                {
                    fallDamageDefault = mv.fallDamageTime;
                }

                if (NoFallDamage)
                {
                    mv.fallDamageTime = 9999f;
                }
                else if (mv.fallDamageTime > 9000f)
                {
                    mv.fallDamageTime = fallDamageDefault > 0f ? fallDamageDefault : 1.5f;
                }
            }

            if (NeverPassOut && c.data.passedOut && !c.data.dead)
            {
                Safe.Run("self.nopassout", () =>
                {
                    c.refs.afflictions.ClearAllStatus(false, false);
                    Players.WakeUp(c);
                });
            }

            if (AutoRevive && c.data.dead && Time.unscaledTime > nextRevive)
            {
                nextRevive = Time.unscaledTime + 3f;
                Safe.Run("self.autorevive", () => Players.Revive(c));
            }

            if (Time.unscaledTime < nextStatusTick || LockStatuses)
            {
                return;
            }

            nextStatusTick = Time.unscaledTime + 0.15f;
            var aff = c.refs.afflictions;

            if (God)
            {
                foreach (var t in Bad)
                {
                    if (aff.GetCurrentStatus(t) > 0f)
                    {
                        aff.SetStatus(t, 0f, true);
                    }
                }

                return;
            }

            if (NoHunger)
            {
                Zero(aff, CharacterAfflictions.STATUSTYPE.Hunger);
            }

            if (NoTemperature)
            {
                Zero(aff, CharacterAfflictions.STATUSTYPE.Cold);
                Zero(aff, CharacterAfflictions.STATUSTYPE.Hot);
            }
        }

        private static void Zero(CharacterAfflictions aff, CharacterAfflictions.STATUSTYPE t)
        {
            if (aff.GetCurrentStatus(t) > 0f)
            {
                aff.SetStatus(t, 0f, true);
            }
        }
    }
}
