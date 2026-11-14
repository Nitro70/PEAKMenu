using System.Collections.Generic;
using UnityEngine;

namespace PEAKMenu.Core
{
    // Lists of things in the level, refreshed a few times a second instead of searched every frame.
    // Entries can be destroyed between refreshes, so every reader checks them with Unity's null test.
    internal static class WorldCache
    {
        public static readonly List<Item> Items = new();
        public static readonly List<global::Luggage> Bags = new();
        public static readonly List<Campfire> Campfires = new();
        public static readonly List<FakeItem> FakeItems = new();
        public static readonly List<Component> Statues = new();
        public static readonly List<Component> Hazards = new();
        public static readonly List<Beehive> Beehives = new();

        private static float next;
        private static float nextSlow;

        public static void Tick()
        {
            if (Time.unscaledTime < next)
            {
                return;
            }

            next = Time.unscaledTime + 0.5f;

            if (!Game.InWorld)
            {
                Clear();
                return;
            }

            Safe.Run("cache.items", () =>
            {
                Items.Clear();

                foreach (var it in global::Item.ALL_ITEMS)
                {
                    if (it && it.itemState == ItemState.Ground && it.gameObject.activeInHierarchy)
                    {
                        Items.Add(it);
                    }
                }
            });

            Safe.Run("cache.luggage", () =>
            {
                Bags.Clear();

                foreach (var l in global::Luggage.ALL_LUGGAGE)
                {
                    if (l && l.isActiveAndEnabled)
                    {
                        Bags.Add(l);
                    }
                }
            });

            Safe.Run("cache.hazards", () =>
            {
                Hazards.Clear();

                var zm = ZombieManager.Instance;

                if (zm && zm.zombies != null)
                {
                    foreach (var z in zm.zombies)
                    {
                        if (z && z.isActiveAndEnabled)
                        {
                            Hazards.Add(z);
                        }
                    }
                }

                if (SpiderManager.instance && SpiderManager.instance.spiders != null)
                {
                    foreach (var s in SpiderManager.instance.spiders)
                    {
                        if (s && s.isActiveAndEnabled)
                        {
                            Hazards.Add(s);
                        }
                    }
                }

                if (MobManager.instance && MobManager.instance.mobs != null)
                {
                    foreach (var m in MobManager.instance.mobs)
                    {
                        if (m && m.isActiveAndEnabled)
                        {
                            Hazards.Add(m);
                        }
                    }
                }

                Beehives.Clear();

                foreach (var b in Beehive.ALL_BEEHIVES)
                {
                    if (b && b.isActiveAndEnabled)
                    {
                        Beehives.Add(b);
                    }
                }
            });

            // the slow ones: whole-scene searches, every 3 seconds
            if (Time.unscaledTime < nextSlow)
            {
                return;
            }

            nextSlow = Time.unscaledTime + 3f;

            Safe.Run("cache.campfires", () =>
            {
                Campfires.Clear();
                Campfires.AddRange(Object.FindObjectsByType<Campfire>(FindObjectsSortMode.None));
            });

            Safe.Run("cache.fakeitems", () =>
            {
                FakeItems.Clear();
                FakeItems.AddRange(Object.FindObjectsByType<FakeItem>(FindObjectsSortMode.None));
            });

            Safe.Run("cache.statues", () =>
            {
                Statues.Clear();
                Statues.AddRange(Object.FindObjectsByType<RespawnChest>(FindObjectsSortMode.None));
                Statues.AddRange(Object.FindObjectsByType<ScoutEffigy>(FindObjectsSortMode.None));
                Statues.AddRange(Object.FindObjectsByType<Peak.PetrifiedScout>(FindObjectsSortMode.None));
            });
        }

        public static void ForceRefresh()
        {
            next = 0f;
            nextSlow = 0f;
        }

        private static void Clear()
        {
            Items.Clear();
            Bags.Clear();
            Campfires.Clear();
            FakeItems.Clear();
            Statues.Clear();
            Hazards.Clear();
            Beehives.Clear();
        }

        public static string ItemName(Item item)
        {
            if (!item)
            {
                return "Item";
            }

            var n = Safe.Get("cache.itemname", () => item.GetName(), null);

            if (string.IsNullOrWhiteSpace(n) && item.UIData != null)
            {
                n = item.UIData.itemName;
            }

            return string.IsNullOrWhiteSpace(n) ? item.name.Replace("(Clone)", "").Trim() : n;
        }

        public static bool IsFood(Item item)
        {
            if ((item.itemTags & (global::Item.ItemTags.PackagedFood | global::Item.ItemTags.Berry | global::Item.ItemTags.Mushroom)) != global::Item.ItemTags.None)
            {
                return true;
            }

            return item.GetComponent<Action_Consume>() || item.GetComponent<Action_ConsumeAndSpawn>();
        }

        public static bool IsSpecial(Item item)
        {
            return (item.itemTags & (global::Item.ItemTags.Mystical | global::Item.ItemTags.BingBong | global::Item.ItemTags.GoldenIdol |
                                     global::Item.ItemTags.BookOfBones | global::Item.ItemTags.ScoutAmulet)) != 0;
        }

        // closed luggage has no items yet: the host rolls them when it opens. This shows what can come out.
        private static readonly Dictionary<int, string> lootLabels = new();

        public static string LuggageLoot(Luggage l)
        {
            int id = l.GetInstanceID();

            if (lootLabels.TryGetValue(id, out var cached))
            {
                return cached;
            }

            string label = Safe.Get("cache.loot", () =>
            {
                if (l.spawnMode == Spawner.SpawnMode.SingleItem && l.spawnedObjectPrefab)
                {
                    var single = l.spawnedObjectPrefab.GetComponent<Item>();
                    return single ? ItemName(single) : null;
                }

                var pool = l.spawnMode == Spawner.SpawnMode.HeightBasedSpawnPools ? l.GetSpawnPool() : l.spawnPool;
                var items = LootData.GetAllItemsInPool(pool);

                if (items == null || items.Count == 0)
                {
                    return null;
                }

                var names = new List<string>();
                var seen = new HashSet<ushort>();

                foreach (var it in items)
                {
                    if (it && seen.Add(it.itemID))
                    {
                        names.Add(ItemName(it));
                    }
                }

                int shown = Mathf.Min(5, names.Count);
                var text = string.Join(", ", names.GetRange(0, shown));
                return names.Count > shown ? $"maybe {text} +{names.Count - shown}" : "maybe " + text;
            }, null);

            lootLabels[id] = label;
            return label;
        }
    }
}
