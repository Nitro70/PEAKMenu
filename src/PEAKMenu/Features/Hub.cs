using PEAKMenu.Core;

namespace PEAKMenu.Features
{
    // Wires every feature into the menu (in sidebar order) and runs their per-frame work.
    internal static class Hub
    {
        public static void RegisterAll()
        {
            SelfMods.Register();
            Movement.Register();
            Teleport.Register();
            Esp.Register();
            Visuals.Register();
            Players.Register();
            Items.Register();
            World.Register();
            Unlocks.Register();
            MenuPages.Register();
        }

        public static void Tick()
        {
            Safe.Run("tick.self", SelfMods.Tick);
            Safe.Run("tick.move", Movement.Tick);
            Safe.Run("tick.players", Players.Tick);
            Safe.Run("tick.items", Items.Tick);
        }

        public static void LateTick()
        {
            Safe.Run("tick.visuals", Visuals.LateTick);
        }

        public static void FixedTick()
        {
        }
    }
}
