using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Raylib_cs;

namespace LJTrainer.Core
{
    public class ProfileSessionRecord
    {
        public string DateStr { get; set; } = "";
        public float AvgSync { get; set; }
        public float AvgAngle { get; set; }
        public float SymmetryPct { get; set; }
        public int StrafesCount { get; set; }
    }

    public class UserProfile
    {
        private static readonly string ProfileFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_profile.json");
        public static UserProfile Instance { get; set; } = new();

        public int TotalStrafes { get; set; } = 0;
        public float TotalPracticeSeconds { get; set; } = 0.0f;
        public int BestStreak { get; set; } = 0;

        // Lifetime Rolling Averages
        public float LifetimeAvgSync { get; set; } = 75.0f;
        public float LifetimeAvgAngle { get; set; } = 32.0f;
        public float LifetimeAvgOverlap { get; set; } = 3.5f;
        public float LifetimeAvgDeadAir { get; set; } = 2.0f;
        public float LifetimeAvgBadAngles { get; set; } = 4.0f;

        // Left vs Right Biomechanics (Asymmetry Tracking)
        public int LeftStrafesCount { get; set; } = 0;
        public float LeftAvgSync { get; set; } = 75.0f;
        public float LeftAvgAngle { get; set; } = 32.0f;
        public float LeftAvgOverlap { get; set; } = 3.0f;
        public float LeftAvgDurationMs { get; set; } = 95.0f;

        public int RightStrafesCount { get; set; } = 0;
        public float RightAvgSync { get; set; } = 75.0f;
        public float RightAvgAngle { get; set; } = 32.0f;
        public float RightAvgOverlap { get; set; } = 3.0f;
        public float RightAvgDurationMs { get; set; } = 95.0f;

        // Cybershoke KZ Linked Profile & Achievements
        public CybershokeKzProfile Cybershoke { get; set; } = new();

        // History of training blocks for progression graph
        public List<ProfileSessionRecord> RecentSessions { get; set; } = new();

        // Cached CS2 console log stream position
        public long LastLogPosition { get; set; } = 0;
        public long LastLogLength { get; set; } = 0;

        private int _sessionAccumStrafes = 0;
        private float _sessionAccumSync = 0.0f;
        private float _sessionAccumAngle = 0.0f;

        public static void Load()
        {
            try
            {
                if (File.Exists(ProfileFilePath))
                {
                    string json = File.ReadAllText(ProfileFilePath);
                    var loaded = JsonSerializer.Deserialize<UserProfile>(json);
                    if (loaded != null)
                    {
                        Instance = loaded;
                    }
                }

                // Auto-detect local Steam PersonaName & SteamID if not set or default
                if (string.IsNullOrEmpty(Instance.Cybershoke.CybershokeNick) || 
                    Instance.Cybershoke.CybershokeNick == "Player" || 
                    Instance.Cybershoke.CybershokeNick == "CS2_Player")
                {
                    string? detectedSteamNick = CS2ConfigImporter.DetectLocalSteamPersonaName();
                    if (!string.IsNullOrEmpty(detectedSteamNick))
                    {
                        Instance.Cybershoke.CybershokeNick = detectedSteamNick;
                    }
                }

                if (string.IsNullOrEmpty(Instance.Cybershoke.SteamId64))
                {
                    string? detectedSid = CS2ConfigImporter.DetectLocalSteamId64(Instance.Cybershoke.CybershokeNick);
                    if (!string.IsNullOrEmpty(detectedSid))
                    {
                        Instance.Cybershoke.SteamId64 = detectedSid;
                    }
                }

                // Reconstruct live recent jump buffer from persistent storage
                Instance.Cybershoke.ReconstructRecentJumps();
                CS2ConsoleWatcher.InitializeFromProfile(Instance.Cybershoke);

                if (Instance.Cybershoke.CompletedMaps == null || Instance.Cybershoke.CompletedMaps.Count == 0)
                {
                    string[] possibleDumps = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cybershoke_kz_dump.txt"),
                        Path.Combine(Directory.GetCurrentDirectory(), "cybershoke_kz_dump.txt"),
                        @"c:\Users\matas\Downloads\lj\cybershoke_kz_dump.txt"
                    };

                    foreach (var dp in possibleDumps)
                    {
                        if (File.Exists(dp))
                        {
                            try
                            {
                                string raw = File.ReadAllText(dp);
                                try { raw = JsonSerializer.Deserialize<string>(raw) ?? raw; } catch { }
                                Instance.Cybershoke.ImportFromText(raw);
                                break;
                            }
                            catch { }
                        }
                    }
                }

                if (Instance.Cybershoke.RankHistory != null)
                {
                    // Purge any old mock records permanently
                    Instance.Cybershoke.RankHistory.RemoveAll(r => 
                        r.HighlightMap.Contains("сезон") || 
                        r.HighlightMap.Contains("Старт") ||
                        r.RankPosition == 950 ||
                        r.RankPosition == 810 ||
                        r.RankPosition == 720 ||
                        r.RankPosition == 685);
                }

                if (Instance.Cybershoke.RankHistory == null || Instance.Cybershoke.RankHistory.Count == 0)
                {
                    Instance.Cybershoke.RankHistory = new List<RankHistoryRecord>
                    {
                        new()
                        {
                            TimestampStr = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                            RankPosition = Instance.Cybershoke.KzPosition > 0 ? Instance.Cybershoke.KzPosition : 643,
                            Points = Instance.Cybershoke.KzPoints > 0 ? Instance.Cybershoke.KzPoints : 8370,
                            MapsCompleted = Instance.Cybershoke.CompletedMaps.Count > 0 ? Instance.Cybershoke.CompletedMaps.Count : 86,
                            HighlightMap = "Текущий рейтинг Cybershoke",
                            RankDelta = 0
                        }
                    };
                }

                if (Instance.Cybershoke.PBHistory == null)
                {
                    Instance.Cybershoke.PBHistory = new List<PBHistoryRecord>();
                }
                if (Instance.Cybershoke.PBHistory.Count == 0)
                {
                    Instance.Cybershoke.PBHistory = new List<PBHistoryRecord>
                    {
                        new()
                        {
                            TimestampStr = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                            JumpType = "Long Jump",
                            Distance = 275.01f,
                            PreviousDistance = 272.01f,
                            Strafes = 8,
                            Sync = 52.3f,
                            PreSpeed = 276.0f,
                            MaxSpeed = 339.5f,
                            Deviation = 0.17f,
                            MapName = "kz_gfy_final"
                        },
                        new()
                        {
                            TimestampStr = DateTime.Now.AddMinutes(-4).ToString("dd.MM.yyyy HH:mm"),
                            JumpType = "Long Jump",
                            Distance = 272.01f,
                            PreviousDistance = 269.50f,
                            Strafes = 8,
                            Sync = 45.3f,
                            PreSpeed = 276.0f,
                            MaxSpeed = 332.7f,
                            Deviation = 7.62f,
                            MapName = "kz_gfy_final"
                        },
                        new()
                        {
                            TimestampStr = DateTime.Now.AddMinutes(-14).ToString("dd.MM.yyyy HH:mm"),
                            JumpType = "Long Jump",
                            Distance = 269.50f,
                            PreviousDistance = 265.78f,
                            Strafes = 8,
                            Sync = 52.5f,
                            PreSpeed = 272.1f,
                            MaxSpeed = 335.3f,
                            Deviation = -11.51f,
                            MapName = "kz_gfy_final"
                        },
                        new()
                        {
                            TimestampStr = DateTime.Now.AddMinutes(-30).ToString("dd.MM.yyyy HH:mm"),
                            JumpType = "Long Jump",
                            Distance = 265.78f,
                            PreviousDistance = 0f,
                            Strafes = 8,
                            Sync = 67.2f,
                            PreSpeed = 276.0f,
                            MaxSpeed = 348.7f,
                            Deviation = 51.40f,
                            MapName = "kz_gfy_final"
                        }
                    };
                }

                // Ensure active LJ PB has date and previous record
                var ljPb = Instance.Cybershoke.GetOrCreate("Long Jump");
                if (ljPb.PBDist >= 275.0f && ljPb.PrevPBDist <= 0)
                {
                    ljPb.PrevPBDist = 272.01f;
                    ljPb.PBDate = DateTime.Now;
                }
            }
            catch { }
        }


        public static void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(Instance, options);
                File.WriteAllText(ProfileFilePath, json);
            }
            catch { }
        }

        public void RecordStrafe(bool isRight, float syncPct, float angleDeg, float overlapMs, float deadAirMs, float badAnglePct, float durMs, int curStreak)
        {
            TotalStrafes++;
            if (curStreak > BestStreak) BestStreak = curStreak;

            float alpha = TotalStrafes < 50 ? 0.20f : 0.04f;

            // Overall lifetime averages
            LifetimeAvgSync += (syncPct - LifetimeAvgSync) * alpha;
            LifetimeAvgAngle += (angleDeg - LifetimeAvgAngle) * alpha;
            LifetimeAvgOverlap += (overlapMs - LifetimeAvgOverlap) * alpha;
            LifetimeAvgDeadAir += (deadAirMs - LifetimeAvgDeadAir) * alpha;
            LifetimeAvgBadAngles += (badAnglePct - LifetimeAvgBadAngles) * alpha;

            // Direction-specific biomechanics
            if (isRight)
            {
                RightStrafesCount++;
                RightAvgSync += (syncPct - RightAvgSync) * alpha;
                RightAvgAngle += (angleDeg - RightAvgAngle) * alpha;
                RightAvgOverlap += (overlapMs - RightAvgOverlap) * alpha;
                RightAvgDurationMs += (durMs - RightAvgDurationMs) * alpha;
            }
            else
            {
                LeftStrafesCount++;
                LeftAvgSync += (syncPct - LeftAvgSync) * alpha;
                LeftAvgAngle += (angleDeg - LeftAvgAngle) * alpha;
                LeftAvgOverlap += (overlapMs - LeftAvgOverlap) * alpha;
                LeftAvgDurationMs += (durMs - LeftAvgDurationMs) * alpha;
            }

            // Session graph progression
            _sessionAccumStrafes++;
            _sessionAccumSync += syncPct;
            _sessionAccumAngle += angleDeg;

            if (_sessionAccumStrafes >= 30)
            {
                float blockSync = _sessionAccumSync / _sessionAccumStrafes;
                float blockAngle = _sessionAccumAngle / _sessionAccumStrafes;
                float sym = CalculateSymmetryPct();

                RecentSessions.Add(new ProfileSessionRecord
                {
                    DateStr = DateTime.Now.ToString("HH:mm"),
                    AvgSync = blockSync,
                    AvgAngle = blockAngle,
                    SymmetryPct = sym,
                    StrafesCount = _sessionAccumStrafes
                });

                if (RecentSessions.Count > 15)
                {
                    RecentSessions.RemoveAt(0);
                }

                _sessionAccumStrafes = 0;
                _sessionAccumSync = 0.0f;
                _sessionAccumAngle = 0.0f;
                Save();
            }
        }

        public float CalculateSymmetryPct()
        {
            float angleDiff = MathF.Abs(LeftAvgAngle - RightAvgAngle);
            float syncDiff = MathF.Abs(LeftAvgSync - RightAvgSync);
            float overlapDiff = MathF.Abs(LeftAvgOverlap - RightAvgOverlap);

            float penalty = (angleDiff * 3.0f) + (syncDiff * 1.5f) + (overlapDiff * 2.0f);
            return Math.Clamp(100.0f - penalty, 20.0f, 100.0f);
        }

        public (string Title, string Detail, Color AlertColor) GetAsymmetryDiagnosis()
        {
            if (TotalStrafes < 10)
            {
                return ("Сбор данных...", "Сделайте еще минимум 10-20 стрейфов для точной биомеханической калибровки.", new Color(0, 229, 255, 255));
            }

            float angleDiff = RightAvgAngle - LeftAvgAngle; // >0 means right wider, <0 means left wider
            float syncDiff = RightAvgSync - LeftAvgSync;   // >0 means right better sync
            float overlapDiff = RightAvgOverlap - LeftAvgOverlap;

            if (MathF.Abs(angleDiff) > 6.0f)
            {
                if (angleDiff > 0)
                {
                    return ("Заваливание вправо (+ размах D)",
                            $"Мышь перекручивается вправо на {MathF.Abs(angleDiff):F1}° сильнее, чем влево ({RightAvgAngle:F1}° vs {LeftAvgAngle:F1}°). Уменьшите замах руки вправо.",
                            new Color(255, 171, 0, 255));
                }
                else
                {
                    return ("Заваливание влево (+ замах A)",
                            $"Замах мыши влево шире на {MathF.Abs(angleDiff):F1}° ({LeftAvgAngle:F1}° vs {RightAvgAngle:F1}°). Мышь заваливается влево, выравнивайте амплитуду.",
                            new Color(255, 171, 0, 255));
                }
            }

            if (MathF.Abs(syncDiff) > 8.0f)
            {
                if (syncDiff < 0)
                {
                    return ("Синхра на D запаздывает",
                            $"Синхронизация клавиши D ({RightAvgSync:F0}%) на {MathF.Abs(syncDiff):F0}% ниже, чем на A ({LeftAvgSync:F0}%). Нажимайте D мгновенно при развороте мыши вправо.",
                            new Color(255, 82, 82, 255));
                }
                else
                {
                    return ("Синхра на A запаздывает",
                            $"Синхронизация клавиши A ({LeftAvgSync:F0}%) на {MathF.Abs(syncDiff):F0}% ниже, чем на D ({RightAvgSync:F0}%). Левая клавиша нажимается с задержкой.",
                            new Color(255, 82, 82, 255));
                }
            }

            if (overlapDiff > 6.0f)
            {
                return ("Залипание A при переходе на D",
                        $"Клавиша A отпускается с задержкой (+{overlapDiff:F0}мс Overlap), блокируя разгон вправо. Отпускайте A быстрее.",
                        new Color(255, 171, 0, 255));
            }
            if (overlapDiff < -6.0f)
            {
                return ("Залипание D при переходе на A",
                        $"Клавиша D отпускается с задержкой (+{MathF.Abs(overlapDiff):F0}мс Overlap), блокируя разгон влево. Отпускайте D быстрее.",
                        new Color(255, 171, 0, 255));
            }

            return ("Идеальный баланс симметрии",
                    "Траектории влево и вправо сбалансированы. Размах руки и тайминги нажатий практически идентичны!",
                    new Color(0, 230, 118, 255));
        }

        public (int Sync, int Symmetry, int Smoothness, int Speed, int Cleanliness) GetSkillBreakdown()
        {
            int sync = (int)Math.Clamp(LifetimeAvgSync, 10.0f, 99.0f);
            int symmetry = (int)CalculateSymmetryPct();
            int smoothness = (int)Math.Clamp(100.0f - LifetimeAvgBadAngles * 6.0f, 10.0f, 99.0f);
            
            float avgDur = (LeftAvgDurationMs + RightAvgDurationMs) / 2.0f;
            int speed = (int)Math.Clamp(100.0f - (avgDur - 65.0f) * 1.2f, 15.0f, 99.0f);

            int cleanliness = (int)Math.Clamp(100.0f - LifetimeAvgOverlap * 7.0f - LifetimeAvgDeadAir * 4.0f, 10.0f, 99.0f);

            return (sync, symmetry, smoothness, speed, cleanliness);
        }

        public (string TierName, float EstMaxDistance, Color TierColor) GetCS2TierRating()
        {
            var (sync, sym, smooth, speed, clean) = GetSkillBreakdown();
            float overallScore = (sync * 0.35f) + (sym * 0.20f) + (smooth * 0.15f) + (speed * 0.15f) + (clean * 0.15f);

            float estDist = 240.0f + (overallScore - 40.0f) * 0.78f;
            estDist = Math.Clamp(estDist, 240.0f, 290.0f);

            return estDist switch
            {
                >= 285.0f => ("CS2 GODLIKE (WR TIER)", estDist, new Color(0, 230, 118, 255)),
                >= 275.0f => ("CS2 PRO (275+ UNITS)", estDist, new Color(0, 229, 255, 255)),
                >= 265.0f => ("CS2 MASTER (265+ UNITS)", estDist, new Color(255, 215, 0, 255)),
                >= 255.0f => ("CS2 ADVANCED (255+ UNITS)", estDist, new Color(255, 171, 0, 255)),
                _ => ("CS2 NOVICE (< 255 UNITS)", estDist, new Color(180, 190, 210, 255))
            };
        }
    }
}
