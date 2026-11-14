using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PEAKMenu.Core;
using PEAKMenu.UI;
using UnityEngine;

namespace PEAKMenu.Features
{
    // Moving yourself. Since 3.0 the game only accepts the warp message from the host, so this moves
    // your own body directly with the game's own warp routine instead; everyone sees you arrive.
    internal static class Teleport
    {
        public static bool ToPing;

        private static string xs = "0", ys = "0", zs = "0";
        private static string spotName = "";

        public static void Register()
        {
            var tab = Registry.Tab("Teleport", "✈", "Move yourself anywhere. Works in any lobby (it moves your own body).");

            tab.Switch("tp.ping", "Teleport to your ping", "Ping somewhere (middle mouse by default) and you land there.", Scope.Client, () => ToPing, v => ToPing = v);

            tab.Action("tp.aim", "Teleport where you look", "Lands you on the first surface under your crosshair (up to 400 m). Good one to bind to a key.", Scope.Client, "Go",
                () => To(Game.AimPoint(400f, 1.2f), "where you were looking")).When(SelfMods.NeedWorld);

            tab.Action("tp.up", "Up 25 m", "Pops you straight up. Handy when stuck.", Scope.Client, "Up",
                () => To(Game.LocalPosition + Vector3.up * 25f, "25 m up")).When(SelfMods.NeedWorld);

            tab.Action("tp.spawn", "Back to the start", "Warps you to the first spawn point of this level.", Scope.Client, "Go", () =>
            {
                var sp = SpawnPoint.allSpawnPoints;

                if (sp != null && sp.Count > 0 && sp[0])
                {
                    To(sp[0].transform.position + Vector3.up, "the start");
                }
                else
                {
                    Toasts.Show("No spawn point in this level");
                }
            }).When(SelfMods.NeedWorld);

            tab.Panel("tp.coords", "Coordinates", "Type a position and go, or copy where you are.", Scope.Client, () =>
            {
                var p = Game.LocalPosition;
                GUILayout.Label($"You are at  <b>{p.x:0.0}, {p.y:0.0}, {p.z:0.0}</b>", Theme.Label);
                GUILayout.BeginHorizontal();
                GUILayout.Label("X", Theme.LabelSubtle, GUILayout.Width(14));
                xs = Ui.Field(xs, 80);
                GUILayout.Label("Y", Theme.LabelSubtle, GUILayout.Width(14));
                ys = Ui.Field(ys, 80);
                GUILayout.Label("Z", Theme.LabelSubtle, GUILayout.Width(14));
                zs = Ui.Field(zs, 80);

                if (Ui.Small("Use mine", 76))
                {
                    xs = p.x.ToString("0.0", CultureInfo.InvariantCulture);
                    ys = p.y.ToString("0.0", CultureInfo.InvariantCulture);
                    zs = p.z.ToString("0.0", CultureInfo.InvariantCulture);
                }

                if (Ui.Small("Go", 50))
                {
                    if (Parse(xs, out var x) && Parse(ys, out var y) && Parse(zs, out var z))
                    {
                        To(new Vector3(x, y, z), "those coordinates");
                    }
                    else
                    {
                        Toasts.Show("Those aren't numbers");
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }).When(SelfMods.NeedWorld);

            tab.Panel("tp.spots", "Saved spots", "Save where you stand and come back to it later. Kept between launches.", Scope.Client, SpotsPanel).When(SelfMods.NeedWorld);
        }

        private static bool Parse(string s, out float v) => float.TryParse((s ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        public static void To(Vector3 position, string what)
        {
            var c = Game.Local;

            if (!c)
            {
                return;
            }

            if (c.data.dead)
            {
                Toasts.Show("You're dead: revive first");
                return;
            }

            c.WarpPlayer(position, true);
            Toasts.Show("Teleported to " + what);
        }

        private sealed class Spot
        {
            public string Name;
            public Vector3 Pos;
        }

        private static List<Spot> Spots()
        {
            var list = new List<Spot>();

            foreach (var entry in Settings.String("tp.spots", "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = entry.Split(';');

                if (bits.Length == 4 && Parse(bits[1], out var x) && Parse(bits[2], out var y) && Parse(bits[3], out var z))
                {
                    list.Add(new Spot { Name = bits[0], Pos = new Vector3(x, y, z) });
                }
            }

            return list;
        }

        private static void Save(List<Spot> spots)
        {
            Settings.SetString("tp.spots", string.Join("|", spots.Select(s =>
                $"{s.Name.Replace("|", "").Replace(";", "")};{s.Pos.x.ToString("0.00", CultureInfo.InvariantCulture)};{s.Pos.y.ToString("0.00", CultureInfo.InvariantCulture)};{s.Pos.z.ToString("0.00", CultureInfo.InvariantCulture)}")));
        }

        private static void SpotsPanel()
        {
            var spots = Spots();
            GUILayout.BeginHorizontal();
            spotName = Ui.Field(spotName, 200);

            if (Ui.Small("Save here", 90))
            {
                var name = string.IsNullOrWhiteSpace(spotName) ? $"Spot {spots.Count + 1}" : spotName.Trim();
                spots.Add(new Spot { Name = name, Pos = Game.LocalPosition });
                Save(spots);
                spotName = "";
                Toasts.Show("Saved " + name);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (spots.Count == 0)
            {
                Ui.Hint("Nothing saved yet.");
                return;
            }

            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{s.Name}</b>  <color=#9FB2C6>{Vector3.Distance(Game.LocalPosition, s.Pos):0} m away</color>", Theme.Label);
                GUILayout.FlexibleSpace();

                if (Ui.Small("Go", 44))
                {
                    To(s.Pos, s.Name);
                }

                if (Ui.Small("Delete", 60))
                {
                    spots.RemoveAt(i);
                    Save(spots);
                    GUIUtility.ExitGUI();
                }

                GUILayout.EndHorizontal();
            }
        }
    }
}
