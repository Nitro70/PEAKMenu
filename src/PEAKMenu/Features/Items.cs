using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using PEAKMenu.Core;
using PEAKMenu.UI;
using UnityEngine;
using Zorro.Core;

namespace PEAKMenu.Features
{
    // Spawn any item, give it to yourself, keep what you hold full. In PEAK any player may create
    // items that they own (the game's own "give item" command does exactly this), so this works as
    // a guest too. The list is read from the game's item database, so new items show up by themselves.
    internal static class Items
    {
        private static readonly Ui.Picker<Item> picker = new();
        private static List<Item> all;
        private static float listAt;

        public static bool InfiniteUses;
        private static float nextRefill;

        private static int Count => Mathf.RoundToInt(Settings.Float("items.count", 1f));

        public static List<Item> All
        {
            get
            {
                if (all == null || all.Count == 0 || Time.unscaledTime > listAt)
                {
                    listAt = Time.unscaledTime + 30f;
                    all = Safe.Get("items.list", () =>
                    {
                        var db = SingletonAsset<ItemDatabase>.Instance;

                        return db?.itemLookup == null
                            ? new List<Item>()
                            : db.itemLookup.Values.Where(i => i).OrderBy(WorldCache.ItemName, System.StringComparer.OrdinalIgnoreCase).ToList();
                    }, new List<Item>());
                }

                return all;
            }
        }

        public static void Register()
        {
            var tab = Registry.Tab("Items", "✚", "Spawn any item in the game and keep what you hold full. Any player may make items in PEAK, so this works as a guest too.");

            tab.Panel("items.spawn", "Spawn an item", "Pick an item, then choose where it goes.", Scope.Client, () =>
            {
                picker.Draw(All, WorldCache.ItemName, 200f, i => i.name);
                var item = picker.Selected;
                Settings.SetFloat("items.count", Ui.Slider("How many", Count, 1f, 20f, "0"));

                GUILayout.BeginHorizontal();
                GUI.enabled = item;

                if (Ui.Small("In my hands", 100))
                {
                    InHands(item);
                }

                if (Ui.Small("At my feet", 90))
                {
                    SpawnMany(item, Game.LocalPosition + Vector3.up * 0.5f);
                }

                if (Ui.Small("Where I look", 100))
                {
                    SpawnMany(item, Game.AimPoint(200f, 1f));
                }

                if (Ui.Small("On picked scout", 120))
                {
                    var target = Players.Selected;

                    if (target)
                    {
                        SpawnMany(item, Game.Where(target) + Vector3.up * 1.5f);
                    }
                    else
                    {
                        Toasts.Show("Pick a scout in the Players tab first");
                    }
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                Ui.Hint(item ? $"Picked: <b>{WorldCache.ItemName(item)}</b>" : "Nothing picked yet.");
            }).When(SelfMods.NeedWorld);

            tab.Action("items.refill", "Refill what you hold", "Fills uses, fuel and charge of the item in your hands.", Scope.Client, "Refill", () =>
            {
                Toasts.Show(Refill(Game.Local.data.currentItem) ? "Refilled" : "Nothing in your hands to refill");
            }).When(SelfMods.NeedWorld);

            tab.Switch("items.infinite", "Infinite item uses", "Keeps the item in your hands full: endless rope, food, flares, lantern fuel...", Scope.Client, () => InfiniteUses, v => InfiniteUses = v);

            tab.Panel("items.kits", "Quick kits", "One click for a handful of useful things at your feet.", Scope.Client, () =>
            {
                GUILayout.BeginHorizontal();
                Kit("Climbing kit", "rope", "piton", "chain", "anchor");
                Kit("Healing kit", "bandage", "medkit", "cure", "first");
                Kit("Food", "trail", "granola", "banana", "coconut", "apple");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }).When(SelfMods.NeedWorld);
        }

        private static void Kit(string label, params string[] words)
        {
            if (!Ui.Small(label))
            {
                return;
            }

            int n = 0;

            foreach (var w in words)
            {
                var hit = All.FirstOrDefault(i => (i.name + " " + WorldCache.ItemName(i)).ToLowerInvariant().Contains(w));

                if (hit)
                {
                    Spawn(hit, Game.LocalPosition + Vector3.up * 0.6f + Random.insideUnitSphere * 0.4f);
                    n++;
                }
            }

            Toasts.Show($"{label}: spawned {n} item(s)");
        }

        // the game's own developer give: made by you, then picked straight up
        public static void InHands(Item item)
        {
            if (!item || !Game.InWorld)
            {
                return;
            }

            ItemDatabase.Add(item, Game.LocalPosition + Vector3.up);
            Toasts.Show("Gave you " + WorldCache.ItemName(item));
        }

        public static void SpawnMany(Item item, Vector3 at)
        {
            if (!item)
            {
                return;
            }

            for (int i = 0; i < Count; i++)
            {
                Spawn(item, at + (Count > 1 ? Random.insideUnitSphere * 0.6f : Vector3.zero));
            }

            Toasts.Show($"Spawned {Count} x {WorldCache.ItemName(item)}");
        }

        public static GameObject Spawn(Item item, Vector3 at)
        {
            if (!item || !Net.Ready)
            {
                return null;
            }

            return PhotonNetwork.Instantiate("0_Items/" + item.name, at, Quaternion.identity, 0);
        }

        public static Item Find<T>() where T : Component => All.FirstOrDefault(i => i.GetComponent<T>());

        public static bool Refill(Item item)
        {
            if (!item || item.data?.data == null)
            {
                return false;
            }

            foreach (var entry in item.data.data.ToList())
            {
                switch (entry.Key)
                {
                    case DataEntryKey.ItemUses when entry.Value is OptionableIntItemData uses && item.totalUses > 0:
                        uses.HasData = true;
                        uses.Value = item.totalUses;
                        break;
                    case DataEntryKey.UseRemainingPercentage when entry.Value is FloatItemData pct:
                        pct.Value = 1f;
                        break;
                    case DataEntryKey.Fuel when entry.Value is FloatItemData fuel:
                        fuel.Value = Mathf.Max(fuel.Value, StartingFuel(item));
                        break;
                }
            }

            Safe.Run("items.refill.pct", () => item.SetUseRemainingPercentage(1f));
            return true;
        }

        // lanterns, candles and the like keep their full tank in a field called startingFuel
        private static float StartingFuel(Item item)
        {
            foreach (var comp in item.GetComponents<MonoBehaviour>())
            {
                var f = comp ? comp.GetType().GetField("startingFuel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) : null;

                if (f != null && f.FieldType == typeof(float))
                {
                    return (float)f.GetValue(comp);
                }
            }

            return 100f;
        }

        public static void Tick()
        {
            if (!InfiniteUses || !Game.InWorld || Time.unscaledTime < nextRefill)
            {
                return;
            }

            nextRefill = Time.unscaledTime + 0.5f;
            var held = Game.Local.data.currentItem;

            if (held)
            {
                Refill(held);
            }
        }
    }
}
