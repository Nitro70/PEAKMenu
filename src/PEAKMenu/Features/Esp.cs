using System.Collections.Generic;
using PEAKMenu.Core;
using PEAKMenu.UI;
using UnityEngine;

namespace PEAKMenu.Features
{
    // See things through walls. Reads only what your own game already knows, so it works in any
    // lobby and nobody else sees it. Same look as REPOMenu: labels, optional boxes and lines.
    internal static class Esp
    {
        public static bool On;

        private static bool Opt(string id, bool def = true) => Settings.Bool("esp." + id, def);
        private static float MaxDistance => Settings.Float("esp.distance", 250f);

        private static readonly Dictionary<int, Renderer[]> renderers = new();
        private static GUIStyle label;
        private static float rendererCacheAt;

        private static readonly Color PlayerColor = new(0.35f, 0.75f, 1f);
        private static readonly Color DownColor = new(1f, 0.7f, 0.3f);
        private static readonly Color DeadColor = new(0.65f, 0.65f, 0.7f);
        private static readonly Color ItemColor = new(0.45f, 0.95f, 0.95f);
        private static readonly Color FoodColor = new(0.55f, 1f, 0.45f);
        private static readonly Color SpecialColor = new(1f, 0.55f, 0.95f);
        private static readonly Color LuggageColor = new(1f, 0.82f, 0.3f);
        private static readonly Color CampfireColor = new(1f, 0.5f, 0.2f);
        private static readonly Color StatueColor = new(0.8f, 0.85f, 1f);
        private static readonly Color DangerColor = new(1f, 0.32f, 0.3f);

        public static void Register()
        {
            var tab = Registry.Tab("ESP", "◎", "See scouts, loot, luggage and danger through walls. Only reads what your game already knows, so it works in any lobby and nobody else sees it.");

            tab.Switch("esp.on", "ESP", "Master switch for everything below.", Scope.Client, () => On, v => On = v);

            tab.Panel("esp.what", "What to show", "Pick the kinds of things to mark.", Scope.Client, () =>
            {
                Row("players", "Scouts (name, health, passed out / dead)");
                Row("items", "Items lying around");
                Row("food", "Food");
                Row("special", "Special items (artifacts, idols, amulets, Bing Bong)");
                Row("luggage", "Closed luggage and what can come out");
                Row("campfires", "Campfires");
                Row("statues", "Revive statues, effigies, petrified scouts");
                Row("danger", "Danger: Scoutmaster, zombies, spiders, beetles, bees");
            });

            tab.Panel("esp.style", "How it looks", "Boxes, lines and limits.", Scope.Client, () =>
            {
                Row("boxes", "Boxes around scouts and danger");
                Row("tracers", "Lines from the bottom of the screen", false);
                Row("distance", "Show distance");
                Settings.SetFloat("esp.distance", Ui.Slider("Max distance", MaxDistance, 20f, 1500f, "0", " m"));
            });
        }

        private static void Row(string id, string text, bool def = true)
        {
            Settings.SetBool("esp." + id, Ui.SwitchRow(text, Opt(id, def)));
        }

        public static void Draw()
        {
            if (!On || Event.current.type != EventType.Repaint || !Game.InWorld)
            {
                return;
            }

            var cam = Game.Camera;

            if (!cam)
            {
                return;
            }

            label ??= new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperCenter, richText = true, wordWrap = false };

            if (Time.unscaledTime > rendererCacheAt)
            {
                rendererCacheAt = Time.unscaledTime + 10f;
                renderers.Clear();
            }

            var me = Game.LocalPosition;
            float max = MaxDistance;
            bool boxes = Opt("boxes"), tracers = Opt("tracers", false), dist = Opt("distance");

            if (Opt("campfires"))
            {
                foreach (var cf in WorldCache.Campfires)
                {
                    if (cf)
                    {
                        string state = cf.state == Campfire.FireState.Lit ? "lit" : cf.state == Campfire.FireState.Spent ? "burnt out" : "not lit";
                        Mark(cam, null, cf.transform.position + Vector3.up, $"Campfire ({state})", CampfireColor, me, 100000f, false, tracers, dist);
                    }
                }
            }

            if (Opt("statues"))
            {
                foreach (var s in WorldCache.Statues)
                {
                    if (s && s.gameObject.activeInHierarchy)
                    {
                        string name = s is RespawnChest ? "Revive statue" : s is ScoutEffigy ? "Scout effigy" : "Petrified scout";
                        Mark(cam, null, s.transform.position + Vector3.up, name, StatueColor, me, max, false, tracers, dist);
                    }
                }
            }

            if (Opt("luggage"))
            {
                foreach (var l in WorldCache.Bags)
                {
                    if (!l || l is RespawnChest)
                    {
                        continue;
                    }

                    var name = string.IsNullOrWhiteSpace(l.displayName) ? "Luggage" : l.displayName;
                    var loot = WorldCache.LuggageLoot(l);
                    var text = loot == null ? name : $"{name}\n<size=10>{loot}</size>";
                    Mark(cam, null, l.Center(), text, LuggageColor, me, max, false, tracers, dist);
                }
            }

            bool items = Opt("items"), food = Opt("food"), special = Opt("special");

            if (items || food || special)
            {
                foreach (var it in WorldCache.Items)
                {
                    if (!it)
                    {
                        continue;
                    }

                    bool isSpecial = WorldCache.IsSpecial(it);
                    bool isFood = !isSpecial && WorldCache.IsFood(it);

                    if ((isSpecial && !special) || (isFood && !food) || (!isSpecial && !isFood && !items))
                    {
                        continue;
                    }

                    Mark(cam, null, it.Center(), WorldCache.ItemName(it), isSpecial ? SpecialColor : isFood ? FoodColor : ItemColor, me, max, false, tracers, dist);
                }

                foreach (var f in WorldCache.FakeItems)
                {
                    if (!f || f.pickedUp || !f.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    var real = f.realItemPrefab;
                    bool isSpecial = real && WorldCache.IsSpecial(real);
                    bool isFood = real && !isSpecial && WorldCache.IsFood(real);

                    if ((isSpecial && !special) || (isFood && !food) || (!isSpecial && !isFood && !items))
                    {
                        continue;
                    }

                    var name = real ? WorldCache.ItemName(real) : f.GetName();
                    Mark(cam, null, f.Center(), name, isSpecial ? SpecialColor : isFood ? FoodColor : ItemColor, me, max, false, tracers, dist);
                }
            }

            if (Opt("danger"))
            {
                if (Scoutmaster.GetPrimaryScoutmaster(out var sm) && sm && sm.character)
                {
                    var pos = sm.character.Center;

                    // parked far away while it waits to appear
                    if (pos.z < 4000f)
                    {
                        var target = sm.currentTarget ? "  hunting " + Game.Name(sm.currentTarget) : "";
                        Mark(cam, sm.character.gameObject, pos + Vector3.up, "SCOUTMASTER" + target, DangerColor, me, 100000f, boxes, true, dist);
                    }
                }

                foreach (var h in WorldCache.Hazards)
                {
                    if (h)
                    {
                        Mark(cam, h.gameObject, h.transform.position + Vector3.up, HazardName(h), DangerColor, me, max, boxes, tracers, dist);
                    }
                }

                foreach (var b in WorldCache.Beehives)
                {
                    if (b)
                    {
                        Mark(cam, null, b.transform.position, "Beehive", DangerColor, me, max, false, tracers, dist);
                    }
                }
            }

            if (Opt("players"))
            {
                foreach (var c in Game.Players)
                {
                    if (!c || c.IsLocal)
                    {
                        continue;
                    }

                    var name = Game.Name(c);

                    if (Game.IsDead(c))
                    {
                        Mark(cam, null, Game.Where(c) + Vector3.up * 0.5f, name + " (dead)", DeadColor, me, 100000f, false, tracers, dist);
                        continue;
                    }

                    float hp = Safe.Get("esp.hp", () => Mathf.Clamp01(1f - c.refs.afflictions.statusSum), 1f);
                    bool down = Game.IsDown(c);
                    var text = down ? $"{name}  <color=#FFB060>passed out</color>" : $"{name}  <color=#9FE8A0>{hp * 100f:0}%</color>";
                    Mark(cam, c.gameObject, Game.Where(c) + Vector3.up * 0.45f, text, down ? DownColor : PlayerColor, me, 100000f, boxes, tracers, dist);
                }
            }
        }

        private static string HazardName(Component h)
        {
            if (h is MushroomZombie)
            {
                return "Zombie";
            }

            if (h is Spider)
            {
                return "Spider";
            }

            return h.GetType().Name;
        }

        private static bool Project(Camera cam, Vector3 world, out Vector2 screen)
        {
            var vp = cam.WorldToViewportPoint(world);
            screen = new Vector2(vp.x * Screen.width, (1f - vp.y) * Screen.height);
            return vp.z > 0.05f && vp.x > -0.2f && vp.x < 1.2f && vp.y > -0.2f && vp.y < 1.2f;
        }

        private static void Mark(Camera cam, GameObject go, Vector3 anchor, string text, Color color, Vector3 me, float max, bool box, bool tracer, bool showDist)
        {
            float d = Vector3.Distance(me, anchor);

            if (d > max || !Project(cam, anchor, out var at))
            {
                return;
            }

            if (box && go && TryScreenBox(cam, go, out var rect))
            {
                DrawBox(rect, color);
                at = new Vector2(rect.center.x, rect.yMin - 16);
            }

            if (tracer)
            {
                DrawLine(new Vector2(Screen.width / 2f, Screen.height), at, new Color(color.r, color.g, color.b, 0.55f), 1.2f);
            }

            var content = showDist ? $"{text}  <color=#C8D2DC>{d:0}m</color>" : text;
            int lines = 1 + content.Split('\n').Length - 1;
            var r = new Rect(at.x - 160, at.y - 2, 320, 18 * lines + 2);
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.85f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), StripColors(content), label);
            GUI.color = Color.white;
            GUI.Label(r, "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + content + "</color>", label);
            GUI.color = old;
        }

        private static readonly Vector3[] corners = new Vector3[8];

        private static bool TryScreenBox(Camera cam, GameObject go, out Rect rect)
        {
            rect = default;
            int id = go.GetInstanceID();

            if (!renderers.TryGetValue(id, out var list))
            {
                list = go.GetComponentsInChildren<Renderer>(false);
                renderers[id] = list;
            }

            bool any = false;
            var b = new Bounds();

            foreach (var r in list)
            {
                if (!r || !r.enabled || r is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!any)
                {
                    b = r.bounds;
                    any = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }

            if (!any || b.size.sqrMagnitude > 400f)
            {
                return false;
            }

            var c = b.center;
            var e = b.extents;
            corners[0] = c + new Vector3(e.x, e.y, e.z);
            corners[1] = c + new Vector3(e.x, e.y, -e.z);
            corners[2] = c + new Vector3(e.x, -e.y, e.z);
            corners[3] = c + new Vector3(e.x, -e.y, -e.z);
            corners[4] = c + new Vector3(-e.x, e.y, e.z);
            corners[5] = c + new Vector3(-e.x, e.y, -e.z);
            corners[6] = c + new Vector3(-e.x, -e.y, e.z);
            corners[7] = c + new Vector3(-e.x, -e.y, -e.z);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            foreach (var corner in corners)
            {
                var vp = cam.WorldToViewportPoint(corner);

                if (vp.z <= 0.05f)
                {
                    return false;
                }

                float x = vp.x * Screen.width, y = (1f - vp.y) * Screen.height;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return rect.width > 2 && rect.height > 2 && rect.width < Screen.width * 1.5f;
        }

        private static void DrawBox(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.6f);
            Outline(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), 3);
            GUI.color = c;
            Outline(r, 1);
            GUI.color = old;
        }

        private static void Outline(Rect r, float t)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Theme.White);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Theme.White);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Theme.White);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Theme.White);
        }

        private static void DrawLine(Vector2 a, Vector2 b, Color c, float width)
        {
            var old = GUI.color;
            var matrix = GUI.matrix;
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            float len = (b - a).magnitude;
            GUI.color = c;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, len, width), Theme.White);
            GUI.matrix = matrix;
            GUI.color = old;
        }

        private static string StripColors(string s) => System.Text.RegularExpressions.Regex.Replace(s, "</?color[^>]*>", "");
    }
}
