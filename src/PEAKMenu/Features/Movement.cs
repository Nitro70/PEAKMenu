using PEAKMenu.Core;
using PEAKMenu.UI;
using UnityEngine;

namespace PEAKMenu.Features
{
    // How your scout moves: fly, speed, jump, climbing. Your body's physics run on your own machine
    // and everyone else just sees where it ends up, so these work in any lobby.
    internal static class Movement
    {
        public static bool Fly, Speed, Jump, Climb, SuperSprint;

        private static Vector3 flyVelocity;
        private static float defSpeed = -1f, defJump = -1f, defClimb = -1f, defVine = -1f, defRope = -1f, defSprint = -1f;
        private static bool appliedSpeed, appliedJump, appliedClimb, appliedSprint;

        private static float FlySpeed => Settings.Float("move.flyspeed", 18f);
        private static float FlyAccel => Settings.Float("move.flyaccel", 6f);
        private static float SpeedMult => Settings.Float("move.speed", 2f);
        private static float SprintMult => Settings.Float("move.sprint", 2f);
        private static float JumpGravity => Settings.Float("move.jumpgravity", 4f);
        private static float ClimbMult => Settings.Float("move.climb", 3f);

        public static void Register()
        {
            var tab = Registry.Tab("Movement", "➤", "Fly, run, jump and climb faster. Your body moves on your own machine, so these work in any lobby.");

            tab.Switch("move.fly", "Fly", "WASD to move, Space up, Ctrl down. No falling, no fall damage while it is on.", Scope.Client, () => Fly, v =>
            {
                Fly = v;
                flyVelocity = Vector3.zero;
            }).WithBody(() =>
            {
                Settings.SetFloat("move.flyspeed", Ui.Slider("Fly speed", FlySpeed, 2f, 80f, "0", " m/s"));
                Settings.SetFloat("move.flyaccel", Ui.Slider("Responsiveness", FlyAccel, 1f, 20f, "0.0"));
            });

            tab.Switch("move.speed", "Walk speed", "Multiplies how fast you walk and run.", Scope.Client, () => Speed, v => Speed = v)
                .WithBody(() => Settings.SetFloat("move.speed", Ui.Slider("Multiplier", SpeedMult, 0.5f, 8f, "0.0", "x")));

            tab.Switch("move.sprint", "Sprint boost", "Extra multiplier only while sprinting.", Scope.Client, () => SuperSprint, v => SuperSprint = v)
                .WithBody(() => Settings.SetFloat("move.sprint", Ui.Slider("Multiplier", SprintMult, 1f, 6f, "0.0", "x")));

            tab.Switch("move.jump", "Super jump", "Lower jump gravity means higher, floatier jumps (the game's normal value is 10).", Scope.Client, () => Jump, v => Jump = v)
                .WithBody(() => Settings.SetFloat("move.jumpgravity", Ui.Slider("Jump gravity", JumpGravity, 0.5f, 10f, "0.0")));

            tab.Switch("move.climb", "Fast climbing", "Climb rock, vines and ropes faster.", Scope.Client, () => Climb, v => Climb = v)
                .WithBody(() => Settings.SetFloat("move.climb", Ui.Slider("Multiplier", ClimbMult, 1f, 10f, "0.0", "x")));
        }

        public static void Tick()
        {
            if (!Game.InWorld)
            {
                return;
            }

            var c = Game.Local;
            var mv = c.refs.movement;

            if (mv)
            {
                Apply(ref appliedSpeed, ref defSpeed, Speed, SpeedMult, () => mv.movementModifier, v => mv.movementModifier = v);
                Apply(ref appliedSprint, ref defSprint, SuperSprint, SprintMult, () => mv.sprintMultiplier, v => mv.sprintMultiplier = v, multiply: true);
                Apply(ref appliedJump, ref defJump, Jump, JumpGravity, () => mv.jumpGravity, v => mv.jumpGravity = v);
            }

            if (Climb || appliedClimb)
            {
                if (defClimb < 0f && c.refs.climbing)
                {
                    defClimb = c.refs.climbing.climbSpeedMod;
                    defVine = c.refs.vineClimbing ? c.refs.vineClimbing.climbSpeedMod : 1f;
                    defRope = c.refs.ropeHandling ? c.refs.ropeHandling.climbSpeedMod : 1f;
                }

                float m = Climb ? ClimbMult : 1f;

                if (c.refs.climbing)
                {
                    c.refs.climbing.climbSpeedMod = Climb ? defClimb * m : defClimb;
                }

                if (c.refs.vineClimbing)
                {
                    c.refs.vineClimbing.climbSpeedMod = Climb ? defVine * m : defVine;
                }

                if (c.refs.ropeHandling)
                {
                    c.refs.ropeHandling.climbSpeedMod = Climb ? defRope * m : defRope;
                }

                appliedClimb = Climb;
            }
        }

        // remembers the game's value the first time, sets ours while on, puts the game's back once when off
        private static void Apply(ref bool applied, ref float def, bool on, float value, System.Func<float> get, System.Action<float> set, bool multiply = false)
        {
            if (on)
            {
                if (!applied)
                {
                    def = get();
                    applied = true;
                }

                set(multiply ? def * value : value);
            }
            else if (applied)
            {
                set(def);
                applied = false;
            }
        }

        // called from the game's own Character.Update for your scout
        public static void FlyTick(Character c)
        {
            if (!Fly || c.data == null || c.data.dead)
            {
                return;
            }

            c.data.isGrounded = true;
            c.data.sinceGrounded = 0f;
            c.data.sinceJump = 0f;

            var input = c.input.movementInput;
            var fwd = c.data.lookDirection_Flat.normalized;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var dir = fwd * input.y + right * input.x;

            if (c.input.jumpIsPressed)
            {
                dir += Vector3.up;
            }

            if (c.input.crouchIsPressed)
            {
                dir += Vector3.down;
            }

            flyVelocity = Vector3.Lerp(flyVelocity, dir.normalized * FlySpeed, Time.deltaTime * FlyAccel);

            foreach (var part in c.refs.ragdoll.partList)
            {
                var rig = part ? part.Rig : null;

                if (rig)
                {
                    rig.linearVelocity = flyVelocity;
                }
            }
        }
    }
}
