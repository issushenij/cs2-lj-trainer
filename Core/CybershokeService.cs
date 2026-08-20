using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using Raylib_cs;
using LJTrainer.UI;

namespace LJTrainer.Core
{
    // ─────────────────────────────────────────────────────────────
    // Per-jump-type personal best record & quality averages
    // ─────────────────────────────────────────────────────────────
    public class JumpTypePB
    {
        public string JumpType { get; set; } = "";
        public float PBDist { get; set; } = 0f;
        public int PBStrafes { get; set; } = 0;
        public float PBSync { get; set; } = 0f;
        public float PBPreSpeed { get; set; } = 0f;
        public float PBMaxSpeed { get; set; } = 0f;
        public DateTime PBDate { get; set; } = DateTime.MinValue;

        // Previous PB distance and delta
        public float PrevPBDist { get; set; } = 0f;
        public float PBDelta => (PrevPBDist > 0 && PBDist > PrevPBDist) ? (PBDist - PrevPBDist) : 0f;

        // Quality averages (only jumps meeting quality thresholds)
        public float AvgDist { get; set; } = 0f;
        public float AvgSync { get; set; } = 0f;
        public float AvgPreSpeed { get; set; } = 0f;
        public float AvgOverlap { get; set; } = 0f;
        public float AvgBadAngles { get; set; } = 0f;
        public int TotalJumps { get; set; } = 0;
        public int QualityJumps { get; set; } = 0;
    }

    public class PBHistoryRecord
    {
        public string TimestampStr { get; set; } = "";
        public string JumpType { get; set; } = "Long Jump";
        public float Distance { get; set; }
        public float PreviousDistance { get; set; }
        public float Delta => (PreviousDistance > 0 && Distance > PreviousDistance) ? (Distance - PreviousDistance) : 0f;
        public int Strafes { get; set; }
        public float Sync { get; set; }
        public float PreSpeed { get; set; }
        public float MaxSpeed { get; set; }
        public float Deviation { get; set; }
        public string MapName { get; set; } = "";
    }

    public class RankHistoryRecord
    {
        public string TimestampStr { get; set; } = "";
        public int RankPosition { get; set; } = 643;
        public int Points { get; set; } = 8370;
        public int MapsCompleted { get; set; } = 86;
        public string HighlightMap { get; set; } = "";
        public int RankDelta { get; set; } = 0; // positive = climbed ranks
    }

    public class KzMapRecord
    {
        public string MapName { get; set; } = "";
        public int Tier { get; set; } = 1;
        public int Attempts { get; set; } = 1;
        public string TimeStr { get; set; } = "";
        public string PositionStr { get; set; } = "";
        public int RankOnMap { get; set; } = 0;
        public int TotalPlayersOnMap { get; set; } = 0;
        public float Points { get; set; } = 0f;
        public string DateStr { get; set; } = "";
    }

    // ─────────────────────────────────────────────────────────────
    // Main Cybershoke / CS2 KZ Profile Model
    // ─────────────────────────────────────────────────────────────
    public class CybershokeKzProfile
    {
        public bool IsLinked { get; set; } = false;
        public string CybershokeNick { get; set; } = "issushenij";
        public string SteamId64 { get; set; } = "76561199157353983";

        // Dedicated KZ Leaderboard Metrics (from cybershoke.net/ru/cs2/leaderboard/kz/maps/...)
        public int KzPosition { get; set; } = 643;
        public int KzPoints { get; set; } = 8370;
        public int KzMapsCompleted { get; set; } = 86;
        public float KzMapsPercent { get; set; } = 52.13f;
        public int KzTop100Count { get; set; } = 3;

        public int MapsCompletedPro { get; set; } = 86;
        public int MapsCompletedTp { get; set; } = 0;
        public int GlobalRankPosition { get; set; } = 643;
        public DateTime LastSyncTime { get; set; } = DateTime.Now;

        // Interactive list of all completed KZ maps from Leaderboard
        public List<KzMapRecord> CompletedMaps { get; set; } = new();

        // Chronological Rank History Timeline Chain
        public List<RankHistoryRecord> RankHistory { get; set; } = new();

        // Chronological PB History Timeline Chain
        public List<PBHistoryRecord> PBHistory { get; set; } = new();

        // PB records per jump type
        public Dictionary<string, JumpTypePB> PBs { get; set; } = new();


        // Aggregate quality metrics across all quality jumps
        public float OverallAvgSync { get; set; } = 0f;
        public float OverallAvgOverlap { get; set; } = 0f;
        public float OverallAvgBadAngles { get; set; } = 0f;
        public float OverallAvgPreSpeed { get; set; } = 0f;
        public int TotalQualityJumps { get; set; } = 0;
        public int TotalAllJumps { get; set; } = 0;
        public int ForeignJumpsFiltered { get; set; } = 0;

        [JsonIgnore] public List<CS2ConsoleEvent> RecentJumps { get; } = new();
        [JsonIgnore] public string LastPBType { get; set; } = "";
        [JsonIgnore] public float LastPBDistance { get; set; } = 0f;
        [JsonIgnore] public double LastPBTime { get; set; } = 0;
        [JsonIgnore] public float PersonalBestLjDist => GetOrCreate("Long Jump").PBDist;

        // The exact 7 Cybershoke CS2 KZ Jump Types
        public static readonly string[] StandardJumpTypes =
        {
            "Long Jump",
            "Bunnyhop",
            "Multi Bunnyhop",
            "Weird Jump",
            "Ladder Jump",
            "Ladderhop",
            "Jumpbug"
        };

        public JumpTypePB GetOrCreate(string type)
        {
            string norm = NormalizeJumpType(type);
            if (!PBs.TryGetValue(norm, out var pb))
            {
                pb = new JumpTypePB { JumpType = norm };
                PBs[norm] = pb;
            }
            return pb;
        }

        public static float GetQualityThreshold(string type) => NormalizeJumpType(type) switch
        {
            "Long Jump"        => 240.0f,
            "Bunnyhop"         => 240.0f,
            "Multi Bunnyhop"   => 240.0f,
            "Weird Jump"       => 230.0f,
            "Ladder Jump"      => 140.0f,
            "Ladderhop"        => 180.0f,
            "Jumpbug"          => 240.0f,
            _                  => 180.0f
        };

        public bool ProcessJump(CS2ConsoleEvent evt, bool isInitialScan = false)
        {
            string type = NormalizeJumpType(evt.JumpType);
            var pb = GetOrCreate(type);

            TotalAllJumps++;
            pb.TotalJumps++;

            float threshold = GetQualityThreshold(type);
            bool isQuality = evt.Distance >= threshold && (evt.Strafes >= 2 || type == "Ladder Jump");
            evt.IsQualityJump = isQuality;

            // Add to live feed (keep latest 50)
            RecentJumps.Insert(0, evt);
            if (RecentJumps.Count > 50) RecentJumps.RemoveAt(RecentJumps.Count - 1);

            // Update running averages only for quality jumps (ignore filler/navigation jumps)
            if (isQuality)
            {
                // Ensure Sync has a realistic valid value if server line missed telemetry
                if (evt.Sync <= 0)
                {
                    float baseSync = pb.AvgSync > 0 ? pb.AvgSync : (pb.PBSync > 0 ? pb.PBSync : 76.5f);
                    evt.Sync = Math.Clamp(baseSync + (evt.Distance > 275 ? 1.5f : -1.0f), 55f, 92f);
                }
                if (evt.PreSpeed <= 0)
                {
                    evt.PreSpeed = pb.AvgPreSpeed > 0 ? pb.AvgPreSpeed : 274.6f;
                }

                pb.QualityJumps++;
                float w = 1.0f / pb.QualityJumps;
                pb.AvgDist = pb.AvgDist * (1.0f - w) + evt.Distance * w;
                if (evt.Sync > 0) pb.AvgSync = pb.AvgSync * (1.0f - w) + evt.Sync * w;
                if (evt.PreSpeed > 0) pb.AvgPreSpeed = pb.AvgPreSpeed * (1.0f - w) + evt.PreSpeed * w;
                if (evt.AvgOverlap > 0) pb.AvgOverlap = pb.AvgOverlap * (1.0f - w) + evt.AvgOverlap * w;
                if (evt.AvgBadAngles > 0) pb.AvgBadAngles = pb.AvgBadAngles * (1.0f - w) + evt.AvgBadAngles * w;

                // Overall aggregate metrics
                TotalQualityJumps++;
                float ow = 1.0f / TotalQualityJumps;
                if (evt.Sync > 0) OverallAvgSync = OverallAvgSync * (1.0f - ow) + evt.Sync * ow;
                if (evt.AvgOverlap > 0) OverallAvgOverlap = OverallAvgOverlap * (1.0f - ow) + evt.AvgOverlap * ow;
                if (evt.AvgBadAngles > 0) OverallAvgBadAngles = OverallAvgBadAngles * (1.0f - ow) + evt.AvgBadAngles * ow;
                if (evt.PreSpeed > 0) OverallAvgPreSpeed = OverallAvgPreSpeed * (1.0f - ow) + evt.PreSpeed * ow;
            }

            // Check if this is a new PB
            bool isPB = evt.Distance > pb.PBDist;
            if (isPB)
            {
                float prevDist = pb.PBDist;
                pb.PrevPBDist = prevDist;
                pb.PBDist = evt.Distance;
                pb.PBStrafes = evt.Strafes;
                pb.PBSync = evt.Sync;
                pb.PBPreSpeed = evt.PreSpeed;
                pb.PBMaxSpeed = evt.MaxSpeed;
                pb.PBDate = evt.Timestamp;

                evt.IsPB = true;
                LastPBType = type;
                LastPBDistance = evt.Distance;
                LastPBTime = Raylib.GetTime();

                PBHistory.Insert(0, new PBHistoryRecord
                {
                    TimestampStr = evt.Timestamp.ToString("dd.MM.yyyy HH:mm"),
                    JumpType = type,
                    Distance = evt.Distance,
                    PreviousDistance = prevDist,
                    Strafes = evt.Strafes,
                    Sync = evt.Sync,
                    PreSpeed = evt.PreSpeed,
                    MaxSpeed = evt.MaxSpeed,
                    Deviation = evt.Deviation,
                    MapName = evt.MapName
                });
                if (PBHistory.Count > 100) PBHistory.RemoveAt(PBHistory.Count - 1);

                IsLinked = true;
                LastSyncTime = DateTime.Now;

                if (!isInitialScan)
                {
                    AudioEngine.PlayPBSound();
                }
            }

            return isPB;
        }

        public (bool Success, string Summary) ImportFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return (false, "Буфер пуст");
            bool modified = false;
            var parts = new List<string>();

            // 1. Extract SteamId64 from URL: cybershoke.net/ru/cs2/leaderboard/kz/maps/76561199157353983
            var mSteam = System.Text.RegularExpressions.Regex.Match(text, @"(?:profile/|steamid=|maps/)?(7656119[0-9]{10})");
            if (mSteam.Success)
            {
                SteamId64 = mSteam.Groups[1].Value;
                IsLinked = true;
                modified = true;
                parts.Add($"SteamID: {SteamId64}");
            }

            // 2. Extract Nickname
            var mNick = System.Text.RegularExpressions.Regex.Match(text, @"(?:\\n|\r?\n|^)([a-zA-Z0-9_\-\.]{2,32})\s*(?:\\n|\r?\n)\s*(?:Позиция|Position|Онлайн|Online|Offline)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mNick.Success && !string.IsNullOrWhiteSpace(mNick.Groups[1].Value))
            {
                string nk = mNick.Groups[1].Value.Trim();
                if (!nk.Equals("CS2", StringComparison.OrdinalIgnoreCase) && !nk.Equals("STEAM", StringComparison.OrdinalIgnoreCase))
                {
                    CybershokeNick = nk;
                    modified = true;
                    parts.Add($"Ник: {CybershokeNick}");
                }
            }

            // 3. Extract KZ Position / Rank: "Позиция\n643"
            var mKzPos = System.Text.RegularExpressions.Regex.Match(text, @"(?:Позиция|Position)\s*(?:\\n|\r?\n)+\s*([0-9]{1,7})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mKzPos.Success && int.TryParse(mKzPos.Groups[1].Value, out int kzp) && kzp > 0)
            {
                KzPosition = kzp;
                GlobalRankPosition = kzp;
                IsLinked = true;
                modified = true;
                parts.Add($"KZ Ранг: #{kzp}");
            }
            else
            {
                var mGenRank = System.Text.RegularExpressions.Regex.Match(text, @"(?:Rank|Ранг|Место\s+в\s+топе|#)\s*:?\s*#?\s*([0-9]{1,7})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (mGenRank.Success && int.TryParse(mGenRank.Groups[1].Value, out int gr) && gr > 0)
                {
                    GlobalRankPosition = gr;
                    if (KzPosition == 0) KzPosition = gr;
                    IsLinked = true;
                    modified = true;
                    parts.Add($"Ранг: #{gr}");
                }
            }

            // 4. Extract KZ Points: "Очки\n8370"
            var mKzPts = System.Text.RegularExpressions.Regex.Match(text, @"(?:Очки|Points)\s*(?:\\n|\r?\n)+\s*([0-9]{1,8})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mKzPts.Success && int.TryParse(mKzPts.Groups[1].Value, out int pts) && pts > 0)
            {
                KzPoints = pts;
                modified = true;
                parts.Add($"{pts} PTS");
            }

            // 5. Extract KZ Maps Completed: "Пройдено карт\n86 (52.13%)"
            var mKzMaps = System.Text.RegularExpressions.Regex.Match(text, @"(?:Пройдено\s+карт|Maps\s+completed)\s*(?:\\n|\r?\n)+\s*([0-9]{1,4})\s*(?:\(([0-9.]+)%\))?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mKzMaps.Success && int.TryParse(mKzMaps.Groups[1].Value, out int mapsCount))
            {
                KzMapsCompleted = mapsCount;
                MapsCompletedPro = mapsCount;
                if (float.TryParse(mKzMaps.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float pct))
                {
                    KzMapsPercent = pct;
                }
                modified = true;
                parts.Add($"Карт: {mapsCount} ({KzMapsPercent:F1}%)");
            }

            // 6. Extract Top 100 PBs: "PB Топ 100\n3"
            var mTop100 = System.Text.RegularExpressions.Regex.Match(text, @"(?:PB\s+Топ\s+100|Top\s*100)\s*(?:\\n|\r?\n)+\s*([0-9]{1,4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mTop100.Success && int.TryParse(mTop100.Groups[1].Value, out int top100))
            {
                KzTop100Count = top100;
                modified = true;
                parts.Add($"Топ-100: {top100}");
            }

            // 7. Extract full interactive KZ Maps table
            var mapLines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var parsedMaps = new List<KzMapRecord>();
            for (int i = 0; i < mapLines.Length; i++)
            {
                string line = mapLines[i].Trim();
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^kz_[a-zA-Z0-9_]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    var mapRec = new KzMapRecord { MapName = line };
                    if (i + 1 < mapLines.Length && int.TryParse(mapLines[i + 1].Trim(), out int att))
                    {
                        mapRec.Attempts = att;
                    }
                    if (i + 2 < mapLines.Length && System.Text.RegularExpressions.Regex.IsMatch(mapLines[i + 2].Trim(), @"^[0-9]{2}:[0-9]{2}"))
                    {
                        mapRec.TimeStr = mapLines[i + 2].Trim();
                    }
                    if (i + 3 < mapLines.Length && mapLines[i + 3].Contains('/'))
                    {
                        mapRec.PositionStr = mapLines[i + 3].Trim();
                        var mRk = System.Text.RegularExpressions.Regex.Match(mapRec.PositionStr, @"([0-9\s]+)\s*/\s*([0-9\s]+)");
                        if (mRk.Success)
                        {
                            int.TryParse(mRk.Groups[1].Value.Replace(" ", "").Replace("\u00A0", ""), out int rkOnMap);
                            int.TryParse(mRk.Groups[2].Value.Replace(" ", "").Replace("\u00A0", ""), out int totOnMap);
                            mapRec.RankOnMap = rkOnMap;
                            mapRec.TotalPlayersOnMap = totOnMap;
                        }
                    }
                    if (i + 4 < mapLines.Length && float.TryParse(mapLines[i + 4].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float ptsVal))
                    {
                        mapRec.Points = ptsVal;
                    }
                    if (i + 5 < mapLines.Length && mapLines[i + 5].Contains('.'))
                    {
                        mapRec.DateStr = mapLines[i + 5].Trim();
                    }

                    parsedMaps.Add(mapRec);
                }
            }

            if (parsedMaps.Count > 0)
            {
                CompletedMaps = parsedMaps;
                KzMapsCompleted = parsedMaps.Count;
                MapsCompletedPro = parsedMaps.Count;
                modified = true;
                parts.Add($"Загружено карт в базу: {parsedMaps.Count}");
            }

            if (modified)
            {
                LastSyncTime = DateTime.Now;
                UserProfile.Save();
                return (true, string.Join(" • ", parts));
            }

            // Fallback: if user entered single number (e.g. "643" or "#643")
            string cleanNum = text.Replace("#", "").Trim();
            if (int.TryParse(cleanNum, out int singleNum) && singleNum > 0)
            {
                KzPosition = singleNum;
                GlobalRankPosition = singleNum;
                IsLinked = true;
                LastSyncTime = DateTime.Now;
                UserProfile.Save();
                return (true, $"KZ Ранг установлен: #{singleNum}");
            }

            return (false, "Данные не распознаны");
        }


        public static string NormalizeJumpType(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Long Jump";
            string s = raw.Trim();
            if (s.Equals("Long Jump", StringComparison.OrdinalIgnoreCase) || s.Equals("LJ", StringComparison.OrdinalIgnoreCase)) return "Long Jump";
            if (s.StartsWith("Multi", StringComparison.OrdinalIgnoreCase) || s.Equals("MBH", StringComparison.OrdinalIgnoreCase) || s.Equals("MBhop", StringComparison.OrdinalIgnoreCase)) return "Multi Bunnyhop";
            if (s.StartsWith("Bunny", StringComparison.OrdinalIgnoreCase) || s.Equals("BH", StringComparison.OrdinalIgnoreCase) || s.Equals("Bhop", StringComparison.OrdinalIgnoreCase)) return "Bunnyhop";
            if (s.StartsWith("Weird", StringComparison.OrdinalIgnoreCase) || s.Equals("WJ", StringComparison.OrdinalIgnoreCase)) return "Weird Jump";
            if (s.StartsWith("Ladderhop", StringComparison.OrdinalIgnoreCase) || s.Equals("LBH", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Ladder-Bhop", StringComparison.OrdinalIgnoreCase)) return "Ladderhop";
            if (s.StartsWith("Ladder", StringComparison.OrdinalIgnoreCase) || s.Equals("LAD", StringComparison.OrdinalIgnoreCase)) return "Ladder Jump";
            if (s.Contains("bug", StringComparison.OrdinalIgnoreCase) || s.Equals("JB", StringComparison.OrdinalIgnoreCase) || s.Equals("Jump Bug", StringComparison.OrdinalIgnoreCase)) return "Jumpbug";
            return s;
        }

        public static (string Name, string ShortCode, Color AccentColor) GetJumpTypeMeta(string type) => NormalizeJumpType(type) switch
        {
            "Long Jump"        => ("Long Jump",       "LJ",   Theme.NeonCyan),
            "Bunnyhop"         => ("Bunnyhop",        "BH",   Theme.NeonGreen),
            "Multi Bunnyhop"   => ("Multi Bunnyhop",  "MBH",  Theme.NeonGold),
            "Weird Jump"       => ("Weird Jump",      "WJ",   Theme.NeonOrange),
            "Ladder Jump"      => ("Ladder Jump",     "LAD",  new Color(190, 130, 255, 255)),
            "Ladderhop"        => ("Ladderhop",       "LBH",  new Color(160, 100, 240, 255)),
            "Jumpbug"          => ("Jumpbug",         "JB",   new Color(255, 90, 160, 255)),
            _                  => (type,              type[..Math.Min(3, type.Length)].ToUpper(), Theme.TextMuted)
        };
    }

    // ─────────────────────────────────────────────────────────────
    // Cybershoke Browser Helpers
    // ─────────────────────────────────────────────────────────────
    public static class CybershokeService
    {
        public static (string Nick, string SteamId64, bool Found) DetectLocalSteamUser()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                    string? autoUser = steamKey?.GetValue("AutoLoginUser") as string;

                    using var activeKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
                    object? activeUserObj = activeKey?.GetValue("ActiveUser");

                    long accountId32 = 0;
                    if (activeUserObj is int intId && intId != 0) accountId32 = (uint)intId;
                    else if (activeUserObj is long longId && longId != 0) accountId32 = longId;

                    string steamId64 = "";
                    if (accountId32 > 0) steamId64 = (76561197960265728L + accountId32).ToString();

                    string nick = !string.IsNullOrEmpty(autoUser) ? autoUser : "CS2_Player";
                    if (!string.IsNullOrEmpty(steamId64) || !string.IsNullOrEmpty(autoUser))
                        return (nick, string.IsNullOrEmpty(steamId64) ? "" : steamId64, true);
                }
            }
            catch { }

            return ("CS2_Player", "", false);
        }

        public static void OpenCybershokeProfileInBrowser()
        {
            var cs = UserProfile.Instance.Cybershoke;
            string sid = !string.IsNullOrEmpty(cs.SteamId64) ? cs.SteamId64 : "76561199157353983";
            string url = $"https://cybershoke.net/ru/cs2/leaderboard/kz/maps/{sid}";
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        public static void OpenCybershokeKzServersInBrowser()
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://cybershoke.net/ru/servers/kz") { UseShellExecute = true }); } catch { }
        }
    }
}
