using System;
using System.Collections.Generic;
using System.Numerics;
using LJTrainer.Core;

namespace LJTrainer.Tests
{
    public static class PhysicsTests
    {
        public static bool RunTests()
        {
            Console.WriteLine("[TESTS] Running Core Physics & JumpStats verification...");

            // Test 1: Ground Pre-speed cap
            var physics = SourcePhysics.Instance;
            Vector3 vel = new Vector3(250, 0, 0);
            Vector3 wishDir = new Vector3(1, 0, 0);
            float dt = 1.0f / 128.0f;

            for (int i = 0; i < 200; i++)
            {
                vel = physics.AccelerateGround(vel, wishDir, 250.0f, 5.2f, dt, 276.0f);
            }

            float spd = MathF.Sqrt(vel.X * vel.X + vel.Y * vel.Y);
            if (spd > 276.001f)
            {
                Console.WriteLine($"[FAIL] Ground pre-speed exceeded 276.0 cap: {spd}");
                return false;
            }
            Console.WriteLine($"[PASS] Ground pre-speed cap verified: {spd:F2} <= 276.0");

            // Test 2: Air Accelerate Gain & GainEff
            Vector3 airVel = new Vector3(275.0f, 0, 0);
            // Perpendicular wishdir (turning left holding A)
            Vector3 airWishDir = new Vector3(0, -1.0f, 0);
            var (newVel, gain, loss, maxGain, gainEff, isBad, angRatio) = 
                physics.AccelerateAir(airVel, airWishDir, 30.0f, 100.0f, dt, -2.5f);

            if (gain <= 0 || newVel.Length() <= airVel.Length())
            {
                Console.WriteLine($"[FAIL] Air accelerate failed to produce gain: gain={gain}");
                return false;
            }
            Console.WriteLine($"[PASS] Air accelerate produced gain: +{gain:F3} u/s, GainEff: {gainEff:F1}%, AngRatio: {angRatio:F2}");

            // Test 3: JumpStats Engine Simulation & Tier classification
            var samples = new List<TickSample>();
            Vector3 pos = Vector3.Zero;
            Vector3 v = new Vector3(275.0f, 0, 250.0f);

            for (int tick = 0; tick < 100; tick++)
            {
                pos += v * dt;
                v.Z -= 800.0f * dt;
                bool isRight = (tick / 15) % 2 == 0;
                float turnYaw = isRight ? 2.5f : -2.5f;

                samples.Add(new TickSample
                {
                    TickIndex = tick,
                    Time = tick * dt,
                    Position = pos,
                    Velocity = v,
                    Speed2D = MathF.Sqrt(v.X * v.X + v.Y * v.Y),
                    MouseDeltaYaw = turnYaw,
                    KeyA = !isRight,
                    KeyD = isRight,
                    KeyW = false,
                    KeyCrouch = tick > 80,
                    IsSync = true,
                    IsOverlap = false,
                    IsDeadAir = false,
                    Gain = 0.35f,
                    Loss = 0.0f,
                    GainEfficiency = 82.0f,
                    TheoreticalMaxGain = 0.42f,
                    AngRatio = -0.05f
                });
            }

            var result = JumpStatsEngine.CalculateJumpStats(samples, 275.0f, 0.0f, PhysicsMode.CKZ);
            if (result.Strafes.Count == 0 || string.IsNullOrEmpty(result.Tier))
            {
                Console.WriteLine("[FAIL] JumpStatsEngine failed to generate strafes or tier.");
                return false;
            }

            Console.WriteLine($"[PASS] JumpStats calculated: Dist={result.Distance:F2}u, Tier={result.Tier}, Strafes={result.StrafeCount}, AvgSync={result.AvgSync:F1}%");
            Console.WriteLine($"[PASS] Left Key:  {result.LeftKeyTimeline.Substring(0, Math.Min(25, result.LeftKeyTimeline.Length))}...");
            Console.WriteLine($"[PASS] Right Key: {result.RightKeyTimeline.Substring(0, Math.Min(25, result.RightKeyTimeline.Length))}...");

            Console.WriteLine("[SUCCESS] All core math & physics tests passed!");
            return true;
        }
    }
}
