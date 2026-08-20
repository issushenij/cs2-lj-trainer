using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace LJTrainer.Core
{
    public class StrafeStat
    {
        public int Number { get; set; }
        public float Sync { get; set; }
        public float Gain { get; set; }
        public float Loss { get; set; }
        public float MaxSpeed { get; set; }
        public float AirtimePct { get; set; }
        public float BadAnglesPct { get; set; }
        public float OverlapPct { get; set; }
        public float DeadAirPct { get; set; }
        public float WidthDegrees { get; set; }
        public float AvgGainPerTick { get; set; }
        public float GainEff { get; set; }
        public float AngRatioAvg { get; set; }
        public float AngRatioMed { get; set; }
        public float AngRatioMin { get; set; }

        public int TickCount { get; set; }
        public float DurationMs { get; set; }
    }

    public class JumpResult
    {
        public float Distance { get; set; }
        public string Tier { get; set; } = "NORMAL";
        public int StrafeCount { get; set; }
        public float AvgSync { get; set; }
        public float PreSpeed { get; set; }
        public float MaxSpeed { get; set; }
        public float AvgBadAngles { get; set; }
        public float AvgOverlap { get; set; }
        public float AvgDeadAir { get; set; }
        public float Deviation { get; set; }
        public float Airpath { get; set; }
        public float AvgGainEff { get; set; }
        public float AvgLoss { get; set; }
        public float AvgWidth { get; set; }
        public float EdgeOffset { get; set; }
        public bool CrouchedAtLanding { get; set; }
        public float CrouchAirFraction { get; set; }
        public float ApexHeight { get; set; }
        public bool W_Released { get; set; }

        public List<StrafeStat> Strafes { get; set; } = new();
        public string LeftKeyTimeline { get; set; } = "";
        public string RightKeyTimeline { get; set; } = "";
        public List<Vector2> Trajectory2D { get; set; } = new();
    }

    public static class JumpStatsEngine
    {
        public static JumpResult CalculateJumpStats(List<TickSample> samples, float takeoffPreSpeed, float edgeOffset, PhysicsMode mode)
        {
            var result = new JumpResult
            {
                PreSpeed = takeoffPreSpeed,
                EdgeOffset = edgeOffset
            };

            if (samples == null || samples.Count < 5)
            {
                result.Distance = 0;
                result.Tier = "INVALID";
                return result;
            }

            int totalTicks = samples.Count;
            Vector3 startPos = samples[0].Position;
            Vector3 endPos = samples[^1].Position;

            // Forward flight vector & distance
            float directDistance = Vector2.Distance(new Vector2(startPos.X, startPos.Y), new Vector2(endPos.X, endPos.Y));
            // Add edge offset adjustment + duck landing adjustment (18 units hitbox bonus if crouched)
            bool crouchedAtLanding = samples[^1].KeyCrouch;
            float duckBonus = crouchedAtLanding ? 2.5f : 0.0f; // Landing duck gives ~2-3 extra units of platform reach
            
            result.Distance = directDistance + duckBonus - edgeOffset;
            if (result.Distance < 0) result.Distance = 0;

            result.CrouchedAtLanding = crouchedAtLanding;
            result.CrouchAirFraction = (float)samples.Count(s => s.KeyCrouch) / totalTicks;
            result.ApexHeight = samples.Max(s => s.Position.Z);
            result.W_Released = !samples.Take(Math.Min(10, totalTicks)).Any(s => s.KeyW);

            // Trajectory & Airpath
            float totalPathLength = 0;
            result.Trajectory2D = new List<Vector2>(totalTicks);
            for (int i = 0; i < totalTicks; i++)
            {
                result.Trajectory2D.Add(new Vector2(samples[i].Position.X, samples[i].Position.Y));
                if (i > 0)
                {
                    totalPathLength += Vector2.Distance(result.Trajectory2D[i - 1], result.Trajectory2D[i]);
                }
            }

            result.Airpath = directDistance > 0.1f ? (totalPathLength / directDistance) : 1.0f;

            // Lateral Deviation
            Vector2 flightDir = directDistance > 0.1f ? Vector2.Normalize(new Vector2(endPos.X - startPos.X, endPos.Y - startPos.Y)) : Vector2.UnitX;
            Vector2 normalDir = new(-flightDir.Y, flightDir.X);
            float maxDeviation = 0;
            foreach (var pos in result.Trajectory2D)
            {
                Vector2 rel = pos - new Vector2(startPos.X, startPos.Y);
                float lat = MathF.Abs(Vector2.Dot(rel, normalDir));
                if (lat > maxDeviation) maxDeviation = lat;
            }
            result.Deviation = maxDeviation;

            // Segment Strafes: identify changes in key or turning direction
            var strafeGroups = new List<List<TickSample>>();
            List<TickSample> currentGroup = new();
            bool? currentIsRight = null;

            for (int i = 0; i < totalTicks; i++)
            {
                var s = samples[i];
                bool isRight = s.KeyD || (s.MouseDeltaYaw < 0 && !s.KeyA);
                bool isLeft = s.KeyA || (s.MouseDeltaYaw > 0 && !s.KeyD);

                bool? strafeDir = isRight ? true : (isLeft ? false : currentIsRight);

                if (currentGroup.Count > 0 && strafeDir != currentIsRight && strafeDir != null)
                {
                    // Strafe switch!
                    strafeGroups.Add(currentGroup);
                    currentGroup = new List<TickSample>();
                }

                currentIsRight = strafeDir;
                currentGroup.Add(s);
            }
            if (currentGroup.Count > 0)
            {
                strafeGroups.Add(currentGroup);
            }

            // Filter out tiny micro-strafes (< 2 ticks) if too many
            if (strafeGroups.Count > 12)
            {
                strafeGroups = strafeGroups.Where(g => g.Count >= 2).ToList();
            }

            result.StrafeCount = Math.Max(1, strafeGroups.Count);

            // Compute per-strafe stats
            float totalGain = 0;
            float totalLoss = 0;
            float maxSpeed = takeoffPreSpeed;

            for (int i = 0; i < strafeGroups.Count; i++)
            {
                var group = strafeGroups[i];
                int count = group.Count;
                var stat = new StrafeStat
                {
                    Number = i + 1,
                    TickCount = count,
                    DurationMs = (count / (float)AppConfig.Instance.Tickrate) * 1000.0f,
                    AirtimePct = ((float)count / totalTicks) * 100.0f
                };

                int syncTicks = 0;
                int badAngleTicks = 0;
                int overlapTicks = 0;
                int deadAirTicks = 0;
                float strafeGain = 0;
                float strafeLoss = 0;
                float strafeMaxSpd = 0;
                float gainEffSum = 0;
                float totalTurn = 0;
                var angRatios = new List<float>();

                for (int j = 0; j < count; j++)
                {
                    var s = group[j];
                    if (s.IsSync) syncTicks++;
                    if (s.IsBadAngle) badAngleTicks++;
                    if (s.IsOverlap) overlapTicks++;
                    if (s.IsDeadAir) deadAirTicks++;

                    strafeGain += s.Gain;
                    strafeLoss += s.Loss;
                    gainEffSum += s.GainEfficiency;
                    totalTurn += MathF.Abs(s.MouseDeltaYaw);
                    angRatios.Add(s.AngRatio);

                    if (s.Speed2D > strafeMaxSpd) strafeMaxSpd = s.Speed2D;
                    if (s.Speed2D > maxSpeed) maxSpeed = s.Speed2D;
                }

                stat.Sync = (float)syncTicks / count * 100.0f;
                stat.BadAnglesPct = (float)badAngleTicks / count * 100.0f;
                stat.OverlapPct = (float)overlapTicks / count * 100.0f;
                stat.DeadAirPct = (float)deadAirTicks / count * 100.0f;
                stat.Gain = strafeGain;
                stat.Loss = strafeLoss;
                stat.MaxSpeed = strafeMaxSpd;
                stat.WidthDegrees = totalTurn;
                stat.AvgGainPerTick = count > 0 ? (strafeGain / count) : 0;
                stat.GainEff = count > 0 ? (gainEffSum / count) : 0;

                if (angRatios.Count > 0)
                {
                    angRatios.Sort();
                    stat.AngRatioAvg = angRatios.Average();
                    stat.AngRatioMed = angRatios[angRatios.Count / 2];
                    stat.AngRatioMin = angRatios[0];
                }

                totalGain += strafeGain;
                totalLoss += strafeLoss;
                result.Strafes.Add(stat);
            }

            result.MaxSpeed = maxSpeed;
            result.AvgSync = result.Strafes.Count > 0 ? result.Strafes.Average(s => s.Sync) : 0;
            result.AvgGainEff = result.Strafes.Count > 0 ? result.Strafes.Average(s => s.GainEff) : 0;
            result.AvgLoss = totalLoss;
            result.AvgBadAngles = result.Strafes.Count > 0 ? result.Strafes.Average(s => s.BadAnglesPct) : 0;
            result.AvgOverlap = result.Strafes.Count > 0 ? result.Strafes.Average(s => s.OverlapPct) : 0;
            result.AvgDeadAir = result.Strafes.Count > 0 ? result.Strafes.Average(s => s.DeadAirPct) : 0;
            result.AvgWidth = result.Strafes.Count > 0 ? result.Strafes.Average(s => s.WidthDegrees) : 0;

            // Determine Tier
            result.Tier = ClassifyTier(result.Distance, mode);

            // Generate Key Timelines
            int timelineWidth = Math.Min(60, totalTicks);
            char[] leftChars = new char[timelineWidth];
            char[] rightChars = new char[timelineWidth];

            for (int i = 0; i < timelineWidth; i++)
            {
                int sampleIdx = (int)((i / (float)timelineWidth) * totalTicks);
                sampleIdx = Math.Clamp(sampleIdx, 0, totalTicks - 1);
                var s = samples[sampleIdx];

                leftChars[i] = s.KeyA ? 'L' : '.';
                rightChars[i] = s.KeyD ? 'R' : '.';
            }

            result.LeftKeyTimeline = new string(leftChars);
            result.RightKeyTimeline = new string(rightChars);

            return result;
        }

        private static string ClassifyTier(float dist, PhysicsMode mode)
        {
            if (mode == PhysicsMode.CKZ)
            {
                if (dist >= 290.0f) return "MONSTER";
                if (dist >= 285.0f) return "HOLY SHIT";
                if (dist >= 280.0f) return "OWNAGE";
                if (dist >= 275.0f) return "GODLIKE";
                if (dist >= 270.0f) return "PERFECT";
                if (dist >= 265.0f) return "IMPRESSIVE";
                if (dist >= 255.0f) return "DECENT";
                return "NORMAL";
            }
            else
            {
                // Vanilla thresholds
                if (dist >= 256.0f) return "HOLY SHIT";
                if (dist >= 253.0f) return "OWNAGE";
                if (dist >= 250.0f) return "GODLIKE";
                if (dist >= 245.0f) return "PERFECT";
                if (dist >= 240.0f) return "IMPRESSIVE";
                if (dist >= 230.0f) return "DECENT";
                return "NORMAL";
            }
        }
    }
}
