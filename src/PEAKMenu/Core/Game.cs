using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEngine;

namespace PEAKMenu.Core
{
    // Small, null-safe questions about the game's state. Everything here may be asked from OnGUI
    // while the game is loading, so nothing assumes a singleton exists.
    internal static class Game
    {
        private static readonly List<Character> players = new();
        private static string version;

        public static Character Local => Character.localCharacter;

        public static bool InWorld => Local && Local.data != null && Local.refs != null;

        public static bool Alive => InWorld && !Local.data.dead;

        public static bool Online => PhotonNetwork.InRoom && !PhotonNetwork.OfflineMode;

        // host of a lobby, or anyone playing alone
        public static bool IsHost => !Online || PhotonNetwork.IsMasterClient;

        public static bool IsGuest => Online && !PhotonNetwork.IsMasterClient;

        // real scouts only: no Scoutmaster, no zombies, no other bots
        public static List<Character> Players
        {
            get
            {
                players.Clear();
                var all = Character.AllCharacters;

                if (all != null)
                {
                    foreach (var c in all)
                    {
                        if (c && !c.isBot && !c.isZombie && !c.isScoutmaster)
                        {
                            players.Add(c);
                        }
                    }
                }

                return players;
            }
        }

        public static IEnumerable<Character> Others
        {
            get
            {
                foreach (var c in Players)
                {
                    if (!c.IsLocal)
                    {
                        yield return c;
                    }
                }
            }
        }

        public static string Name(Character c)
        {
            if (!c)
            {
                return "?";
            }

            var n = Safe.Get("game.name", () => c.characterName, null);
            return string.IsNullOrWhiteSpace(n) ? "Scout" : n;
        }

        public static bool IsDead(Character c) => c && c.data != null && c.data.dead;

        public static bool IsDown(Character c) => c && c.data != null && (c.data.passedOut || c.data.fullyPassedOut);

        // where a player really is: their ghost while dead, otherwise their head
        public static Vector3 Where(Character c)
        {
            if (!c)
            {
                return Vector3.zero;
            }

            if (IsDead(c) && c.Ghost)
            {
                return c.Ghost.transform.position;
            }

            return Safe.Get("game.head", () => c.Head, c.transform.position);
        }

        public static Camera Camera
        {
            get
            {
                if (MainCamera.instance && MainCamera.instance.cam)
                {
                    return MainCamera.instance.cam;
                }

                return Camera.main;
            }
        }

        public static Vector3 LocalPosition => Local ? Where(Local) : Vector3.zero;

        // where the player is looking, up to a distance, stopping at the first surface
        public static Vector3 AimPoint(float maxDistance = 200f, float backOff = 0.8f)
        {
            var cam = Camera;

            if (!cam)
            {
                return LocalPosition;
            }

            var ray = new Ray(cam.transform.position, cam.transform.forward);

            if (Physics.Raycast(ray, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point + hit.normal * backOff;
            }

            return ray.GetPoint(Mathf.Min(maxDistance, 10f));
        }

        public static string Version
        {
            get
            {
                if (version != null)
                {
                    return version;
                }

                version = "?";

                try
                {
                    var file = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "version.txt");

                    if (File.Exists(file))
                    {
                        var text = File.ReadAllText(file).Trim();
                        int cut = text.IndexOf("--");
                        version = cut > 0 ? text.Substring(0, cut).Trim() : text;
                    }
                    else
                    {
                        version = Application.version;
                    }
                }
                catch
                {
                    version = Application.version;
                }

                return version;
            }
        }

        public static string Status
        {
            get
            {
                if (!Online)
                {
                    return "Offline / alone: everything works";
                }

                return PhotonNetwork.IsMasterClient
                    ? $"Host of {PhotonNetwork.CurrentRoom?.PlayerCount ?? 1} scouts: everything works"
                    : $"Guest in {PhotonNetwork.CurrentRoom?.PlayerCount ?? 1} scouts: host-only cards are locked";
            }
        }
    }
}
