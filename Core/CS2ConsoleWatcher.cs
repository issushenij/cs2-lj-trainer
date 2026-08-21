using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace LJTrainer.Core
{
    public class StrafeDetail
    {
        public int StrafeIndex { get; set; }
        public float Sync { get; set; }
        public float Gain { get; set; }
        public float Loss { get; set; }
        public float MaxSpeed { get; set; }
        public float AirtimePct { get; set; }
        public float BadAngles { get; set; }
        public float Overlap { get; set; }
        public float DeadAir { get; set; }
        public float WidthDeg { get; set; }
        public float AvgGain { get; set; }
        public float GainEff { get; set; }
    }

    public class CS2ConsoleEvent
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string RawLine { get; set; } = "";
        public string PlayerNick { get; set; } = "";

        // Jump telemetry
        public bool IsJumpStat { get; set; }
        public float Distance { get; set; }
        public float BlockDistance { get; set; } = 0f;
        public float Sync { get; set; }
        public float PreSpeed { get; set; }
        public float MaxSpeed { get; set; }
        public int Strafes { get; set; }
        public float AvgOverlap { get; set; }
        public float AvgBadAngles { get; set; }
        public float AvgDeadAir { get; set; }
        public float Deviation { get; set; }
        public float Airpath { get; set; }
        public float AvgWidth { get; set; }
        public float AvgGainEff { get; set; }
        public float AvgLoss { get; set; }
        public float Height { get; set; }
        public float Crouched { get; set; }
        public string JumpType { get; set; } = "Long Jump";
        public string JumpDirection { get; set; } = "Forwards";
        public bool IsQualityJump { get; set; }
        public bool IsPB { get; set; }

        // Per-strafe breakdown table directly from console
        public List<StrafeDetail> StrafeBreakdown { get; set; } = new();
        public string LeftKeySequence { get; set; } = "";
        public string RightKeySequence { get; set; } = "";
        public string LeftMouseSequence { get; set; } = "";
        public string RightMouseSequence { get; set; } = "";

        // Map telemetry
        public bool IsMapChange { get; set; }
        public bool IsMapFinished { get; set; }
        public string MapName { get; set; } = "";
        public string FinishTime { get; set; } = "";
        public bool IsPro { get; set; }
    }

    public static class CS2ConsoleWatcher
    {
        private static CancellationTokenSource? _cts;
        private static string _currentLogPath = "";
        private static long _lastFilePosition = 0;
        private static bool _initialScanComplete = false;

        private static CS2ConsoleEvent? _pendingJumpEvt = null;

        public static bool IsWatching { get; private set; } = false;
        public static string ActiveLogPath => _currentLogPath;
        public static int EventsCaptured { get; private set; } = 0;
        public static string CurrentMap { get; private set; } = "";
        public static string DetectedNick { get; private set; } = "";
        public static DateTime LastActivityTime { get; private set; } = DateTime.MinValue;

        public static event Action<CS2ConsoleEvent>? OnConsoleEvent;

        private static readonly HashSet<string> _knownJumpSignatures = new();

        public static string ComputeJumpSignature(CS2ConsoleEvent j)
        {
            string nick = (j.PlayerNick ?? "").Trim().ToLowerInvariant();
            return $"{nick}_{j.JumpType}_{j.Distance:F2}_{j.Strafes}_{j.Sync:F0}_{j.PreSpeed:F0}_{j.BlockDistance:F0}";
        }

        public static void InitializeFromProfile(CybershokeKzProfile cs)
        {
            _knownJumpSignatures.Clear();
            foreach (var kvp in cs.JumpHistoryPerType)
            {
                if (kvp.Value != null)
                {
                    foreach (var j in kvp.Value)
                    {
                        _knownJumpSignatures.Add(ComputeJumpSignature(j));
                    }
                }
            }
            foreach (var j in cs.RecentJumps)
            {
                _knownJumpSignatures.Add(ComputeJumpSignature(j));
            }
        }

        // "Player jumped 269.2057 units (Block: 260) with a Long Jump" (Supports Cyrillic, Chinese, Kanji, Special Chars)
        private static readonly Regex JumpedPattern = new(
            @"^(?:\[.*?\]\s*|\{.*?\}\s*|\(.*?\)\s*)?(.+?)\s+jumped\s+([0-9]+\.[0-9]+)\s+units(?:\s*\(?(?:Block|Блок):\s*([0-9]+(?:\.[0-9]+)?)\)?)?\s+with\s+a\s+([\p{L}\w\s\-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "CKZ | 238 Block | 8 Strafes | 55.56% AvgSync | 275.38 Pre | 344.17 Max | 30.30% AvgBadAngles | 0.00% AvgOverlap | 14.14% AvgDeadAir | JumpDirection: Forwards"
        // "CKZ | 9 Strafes | 57.39% AvgSync | 270.61 Pre | 337.69 Max | 14.33% AvgBadAngles | 23.23% AvgOverlap | 3.03% AvgDeadAir | JumpDirection: Forwards"
        private static readonly Regex CkzPattern = new(
            @"CKZ\s*\|(?:\s*([0-9.]+)\s*(?:Block|Блок)\s*\|)?\s*(\d+)\s*Strafes\s*\|\s*([0-9.]+)%\s*AvgSync\s*\|\s*([0-9.]+)\s*Pre\s*\|\s*([0-9.]+)\s*Max(?:\s*\|\s*([0-9.]+)%\s*AvgBadAngles)?(?:\s*\|\s*([0-9.]+)%\s*AvgOverlap)?(?:\s*\|\s*([0-9.]+)%\s*AvgDeadAir)?(?:\s*\|\s*JumpDirection:\s*(\w+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Cybershoke Line 3: "51.40 Deviation | 1.024 Airpath | 53.37% AvgGainEff | 0.00 AvgLoss | 33.83° AvgWidth | 0.00 Offset | 0.00/0.00 Crouched | 55.83 Height | ✓ W"
        private static readonly Regex CkzDetailsPattern = new(
            @"(-?[0-9.]+)\s*Deviation\s*\|\s*([0-9.]+)\s*Airpath(?:\s*\|\s*([0-9.]+)%\s*AvgGainEff)?(?:\s*\|\s*(-?[0-9.]+)\s*AvgLoss)?(?:\s*\|\s*([0-9.]+)[°\u00B0\s]*AvgWidth)?(?:\s*\|\s*([0-9.]+)\s*Offset)?(?:\s*\|\s*([0-9./]+)\s*Crouched)?(?:\s*\|\s*([0-9.]+)\s*Height)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Per-strafe breakdown row: "1.     79.98%     +18.75     -0.00     294.72     16.10%      20.02%        0.00%       0.00%       73.08°      1.17        70.45%      -0.24 | -0.01 | -0.50"
        private static readonly Regex StrafeRowPattern = new(
            @"^\s*(\d+)\.\s+([0-9.]+)%\s+([+\-0-9.]+)\s+([+\-0-9.]+)\s+([0-9.]+)\s+([0-9.]+)%\s+([0-9.]+)%\s+([0-9.]+)%\s+([0-9.]+)%[°\u00B0\s0-9.]*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Key / Mouse Sequences
        private static readonly Regex LeftKeyPattern = new(@"^LEFT\s+KEY\s*\|\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RightKeyPattern = new(@"^RIGHT\s+KEY\s*\|\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LeftMousePattern = new(@"^LEFT\s+MOUSE\s*\|\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RightMousePattern = new(@"^RIGHT\s+MOUSE\s*\|\s*(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Generic JumpStats fallback: "[JumpStats] 272.5 u | 8 str | 78% sync | Pre: 274.5"
        private static readonly Regex GenericJumpStatsPattern = new(
            @"(?:\[JumpStats\]|\[KZ\])\s*(?:Distance:\s*)?([0-9]+\.[0-9]+)\s*(?:units|u)?\s*\|\s*(\d+)\s*(?:strafes|str)?\s*\|\s*([0-9.]+)%\s*(?:sync|AvgSync)?(?:\s*\|\s*(?:Pre:\s*)?([0-9.]+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "[Client] Map: "kz_leto""
        private static readonly Regex MapChangePattern = new(
            @"\[Client\]\s+Map:\s+""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Map finish
        private static readonly Regex MapFinishPattern = new(
            @"(?:finished|completed|Time:)\s*(?:map\s+)?[""']?([a-zA-Z0-9_]+)[""']?\s+(?:in\s+)?([0-9:]+\.?[0-9]*)\s*(?:\(?(pro|tp)\)?)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Server stats / rank output from chat commands (!stats, !top, !profile, !rank)
        private static readonly Regex ServerStatsPattern = new(
            @"(?:\[KZ\]|\[CYBERSHOKE\]|\[CKZ\]|Global Rank).*?(?:Rank|Top|Место|#)\s*:?\s*#?([0-9]{1,6})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ServerPointsPattern = new(
            @"(?:\[KZ\]|\[CYBERSHOKE\]|\[CKZ\]).*?(?:Points|Очки|PTS)\s*:?\s*([0-9]{1,7})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void StartWatching()
        {
            if (IsWatching) return;

            _currentLogPath = FindCS2ConsoleLogFile();
            _cts = new CancellationTokenSource();
            IsWatching = true;

            Task.Run(() => WatchLoop(_cts.Token));
        }

        public static void ReScanFullLogFromBeginning()
        {
            _lastFilePosition = 0;
            _initialScanComplete = false;
            UserProfile.Instance.LastLogPosition = 0;
            _currentLogPath = FindCS2ConsoleLogFile();
        }

        public static void StopWatching()
        {
            _cts?.Cancel();
            IsWatching = false;
        }

        private static async Task WatchLoop(CancellationToken token)
        {
            int retrySearchTicks = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // If no file yet, retry search every ~2 seconds
                    if (string.IsNullOrEmpty(_currentLogPath) || !File.Exists(_currentLogPath))
                    {
                        if (retrySearchTicks % 20 == 0)
                        {
                            string found = FindCS2ConsoleLogFile();
                            if (!string.IsNullOrEmpty(found))
                            {
                                _currentLogPath = found;
                                _lastFilePosition = 0;
                                _initialScanComplete = false;
                            }
                        }
                        retrySearchTicks++;
                    }

                    if (!string.IsNullOrEmpty(_currentLogPath) && File.Exists(_currentLogPath))
                    {
                        using var fs = new FileStream(_currentLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                        if (fs.Length < _lastFilePosition)
                        {
                            // File was truncated / recreated by CS2 restart
                            _lastFilePosition = 0;
                        }

                        if (!_initialScanComplete)
                        {
                            var prof = UserProfile.Instance;
                            if (prof.LastLogPosition > 0 && fs.Length >= prof.LastLogPosition && prof.LastLogLength == fs.Length)
                            {
                                _lastFilePosition = prof.LastLogPosition;
                                _initialScanComplete = true;
                            }
                            else if (prof.LastLogPosition > 0 && fs.Length >= prof.LastLogPosition)
                            {
                                _lastFilePosition = prof.LastLogPosition;
                            }
                            else
                            {
                                // First launch or file rotated: only scan the newest ~1MB of log from the tail
                                _lastFilePosition = Math.Max(0, fs.Length - 1024 * 1024);
                            }
                        }

                        if (fs.Length > _lastFilePosition)
                        {
                            fs.Seek(_lastFilePosition, SeekOrigin.Begin);
                            using var reader = new StreamReader(fs, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                            string? line;

                            bool isInit = !_initialScanComplete;
                            bool hasNewJumps = false;

                            while ((line = reader.ReadLine()) != null)
                            {
                                if (ParseLine(line, isInit))
                                {
                                    hasNewJumps = true;
                                }
                            }

                            if (_pendingJumpEvt != null)
                            {
                                var evt = _pendingJumpEvt;
                                _pendingJumpEvt = null;
                                LastActivityTime = DateTime.Now;
                                EventsCaptured++;
                                if (ApplyEventToProfile(evt, isInit))
                                {
                                    hasNewJumps = true;
                                }
                                OnConsoleEvent?.Invoke(evt);
                            }

                            _lastFilePosition = fs.Position;
                            _initialScanComplete = true;

                            UserProfile.Instance.LastLogPosition = _lastFilePosition;
                            UserProfile.Instance.LastLogLength = fs.Length;

                            if (hasNewJumps)
                            {
                                UserProfile.Save();
                            }
                        }
                    }
                }
                catch { }

                // High responsiveness real-time polling (100ms)
                await Task.Delay(100, token);
            }
        }

        private static string StripTimestamp(string line)
        {
            // CS2 format: "08/20 04:07:59 message..."
            if (line.Length > 16 && char.IsDigit(line[0]) && line[2] == '/' && line[5] == ' ' && line[8] == ':')
                return line[15..].TrimStart();
            return line.TrimStart();
        }

        private static bool ParseLine(string rawLine, bool isInitialScan)
        {
            if (string.IsNullOrWhiteSpace(rawLine)) return false;

            string line = StripTimestamp(rawLine);
            bool jumpAdded = false;

            // 1. Map Change
            var mMap = MapChangePattern.Match(line);
            if (mMap.Success)
            {
                CurrentMap = mMap.Groups[1].Value;
                LastActivityTime = DateTime.Now;
                var evt = new CS2ConsoleEvent { RawLine = rawLine, IsMapChange = true, MapName = CurrentMap };
                EventsCaptured++;
                OnConsoleEvent?.Invoke(evt);
                return false;
            }

            // 2. Jump line 1 ("issushenij jumped 269.2057 units (Block: 260) with a Long Jump")
            var mJumped = JumpedPattern.Match(line);
            if (mJumped.Success)
            {
                // If previous jump was waiting and didn't get details, flush it
                if (_pendingJumpEvt != null)
                {
                    if (ApplyEventToProfile(_pendingJumpEvt, isInitialScan))
                    {
                        jumpAdded = true;
                    }
                    OnConsoleEvent?.Invoke(_pendingJumpEvt);
                    _pendingJumpEvt = null;
                }

                string nick = mJumped.Groups[1].Value.Trim();
                float.TryParse(mJumped.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float dist);
                float blockDist = 0f;
                if (mJumped.Groups[3].Success)
                {
                    float.TryParse(mJumped.Groups[3].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out blockDist);
                }
                string jType = mJumped.Groups[4].Value.Trim();

                if (!string.IsNullOrEmpty(nick))
                {
                    DetectedNick = nick;
                    if (string.IsNullOrEmpty(UserProfile.Instance.Cybershoke.CybershokeNick) || UserProfile.Instance.Cybershoke.CybershokeNick == "CS2_Player")
                    {
                        UserProfile.Instance.Cybershoke.CybershokeNick = nick;
                    }
                }

                _pendingJumpEvt = new CS2ConsoleEvent
                {
                    RawLine = rawLine,
                    PlayerNick = nick,
                    IsJumpStat = true,
                    Distance = dist,
                    BlockDistance = blockDist,
                    JumpType = jType,
                    MapName = CurrentMap,
                    Timestamp = DateTime.Now
                };
                return jumpAdded; // Wait for immediate next CKZ line with telemetry
            }

            // 3. CKZ Telemetry line 2 ("CKZ | 238 Block | 8 Strafes | 55.56% AvgSync...") or ("CKZ | 9 Strafes...")
            var mCkz = CkzPattern.Match(line);
            if (mCkz.Success)
            {
                float blockFromCkz = 0f;
                if (mCkz.Groups[1].Success)
                {
                    float.TryParse(mCkz.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out blockFromCkz);
                }

                int.TryParse(mCkz.Groups[2].Value, out int strafes);
                float.TryParse(mCkz.Groups[3].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sync);
                float.TryParse(mCkz.Groups[4].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float pre);
                float.TryParse(mCkz.Groups[5].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float max);
                float.TryParse(mCkz.Groups[6].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float badAngles);
                float.TryParse(mCkz.Groups[7].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float overlap);
                float.TryParse(mCkz.Groups[8].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float deadAir);
                string jumpDir = mCkz.Groups[9].Value;

                var evt = _pendingJumpEvt ?? new CS2ConsoleEvent
                {
                    RawLine = rawLine,
                    IsJumpStat = true,
                    MapName = CurrentMap,
                    Timestamp = DateTime.Now
                };

                if (_pendingJumpEvt != null)
                {
                    evt.RawLine += "\n" + rawLine;
                }

                evt.Strafes = strafes;
                evt.Sync = sync;
                evt.PreSpeed = pre;
                evt.MaxSpeed = max;
                evt.AvgBadAngles = badAngles;
                evt.AvgOverlap = overlap;
                evt.AvgDeadAir = deadAir;
                if (!string.IsNullOrEmpty(jumpDir)) evt.JumpDirection = jumpDir;
                if (blockFromCkz > 0 && evt.BlockDistance <= 0) evt.BlockDistance = blockFromCkz;

                _pendingJumpEvt = evt; // Keep pending for Line 3 (Deviation / Airpath details)
                LastActivityTime = DateTime.Now;
                return jumpAdded;
            }

            // 4. CKZ Details line 3 ("-11.51 Deviation | 1.016 Airpath | 43.26% AvgGainEff | 36.61° AvgWidth ...")
            var mDetails = CkzDetailsPattern.Match(line);
            if (mDetails.Success)
            {
                float.TryParse(mDetails.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float deviation);
                float.TryParse(mDetails.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float airpath);
                float.TryParse(mDetails.Groups[3].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float avgGainEff);
                float.TryParse(mDetails.Groups[4].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float avgLoss);
                float.TryParse(mDetails.Groups[5].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float avgWidth);
                float.TryParse(mDetails.Groups[7].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float crouched);
                float.TryParse(mDetails.Groups[8].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float height);

                var evt = _pendingJumpEvt ?? new CS2ConsoleEvent
                {
                    RawLine = rawLine,
                    IsJumpStat = true,
                    MapName = CurrentMap,
                    Timestamp = DateTime.Now
                };

                if (_pendingJumpEvt != null)
                {
                    evt.RawLine += "\n" + rawLine;
                }

                evt.Deviation = deviation;
                evt.Airpath = airpath;
                evt.AvgGainEff = avgGainEff;
                evt.AvgLoss = avgLoss;
                evt.AvgWidth = avgWidth;
                evt.Crouched = crouched;
                evt.Height = height;

                _pendingJumpEvt = evt; // Keep pending for per-strafe breakdown table!
                LastActivityTime = DateTime.Now;
                return jumpAdded;
            }

            // 5. Per-strafe breakdown table row (1. 79.98% +18.75 ...)
            var mStrafe = StrafeRowPattern.Match(line);
            if (mStrafe.Success && _pendingJumpEvt != null)
            {
                _pendingJumpEvt.RawLine += "\n" + rawLine;
                int.TryParse(mStrafe.Groups[1].Value, out int sIdx);
                float.TryParse(mStrafe.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sSync);
                float.TryParse(mStrafe.Groups[3].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sGain);
                float.TryParse(mStrafe.Groups[4].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sLoss);
                float.TryParse(mStrafe.Groups[5].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sMax);
                float.TryParse(mStrafe.Groups[6].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sAirtime);
                float.TryParse(mStrafe.Groups[7].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sBad);
                float.TryParse(mStrafe.Groups[8].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sOver);
                float.TryParse(mStrafe.Groups[9].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sDead);
                float.TryParse(mStrafe.Groups[10].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sWidth);

                _pendingJumpEvt.StrafeBreakdown.Add(new StrafeDetail
                {
                    StrafeIndex = sIdx - 1,
                    Sync = sSync,
                    Gain = sGain,
                    Loss = sLoss,
                    MaxSpeed = sMax,
                    AirtimePct = sAirtime,
                    BadAngles = sBad,
                    Overlap = sOver,
                    DeadAir = sDead,
                    WidthDeg = sWidth
                });
                LastActivityTime = DateTime.Now;
                return jumpAdded;
            }

            // 6. Key / Mouse Sequences & Table Headers
            if (_pendingJumpEvt != null)
            {
                var mLK = LeftKeyPattern.Match(line);
                if (mLK.Success) { _pendingJumpEvt.RawLine += "\n" + rawLine; _pendingJumpEvt.LeftKeySequence = mLK.Groups[1].Value.Trim(); LastActivityTime = DateTime.Now; return jumpAdded; }

                var mRK = RightKeyPattern.Match(line);
                if (mRK.Success) { _pendingJumpEvt.RawLine += "\n" + rawLine; _pendingJumpEvt.RightKeySequence = mRK.Groups[1].Value.Trim(); LastActivityTime = DateTime.Now; return jumpAdded; }

                var mLM = LeftMousePattern.Match(line);
                if (mLM.Success) { _pendingJumpEvt.RawLine += "\n" + rawLine; _pendingJumpEvt.LeftMouseSequence = mLM.Groups[1].Value.Trim(); LastActivityTime = DateTime.Now; return jumpAdded; }

                var mRM = RightMousePattern.Match(line);
                if (mRM.Success)
                {
                    _pendingJumpEvt.RawLine += "\n" + rawLine;
                    _pendingJumpEvt.RightMouseSequence = mRM.Groups[1].Value.Trim();
                    // Final line of table sequence! Flush jump event.
                    var evt = _pendingJumpEvt;
                    _pendingJumpEvt = null;
                    LastActivityTime = DateTime.Now;
                    EventsCaptured++;
                    if (ApplyEventToProfile(evt, isInitialScan))
                    {
                        jumpAdded = true;
                    }
                    OnConsoleEvent?.Invoke(evt);
                    return jumpAdded;
                }

                // Any telemetry headers, empty lines or table separators belong to this jump log
                if (line.StartsWith("#.") || line.StartsWith("Sync") || line.StartsWith("LEFT KEY") || line.StartsWith("RIGHT KEY") || line.StartsWith("LEFT MOUSE") || line.StartsWith("RIGHT MOUSE") || string.IsNullOrWhiteSpace(line))
                {
                    _pendingJumpEvt.RawLine += "\n" + rawLine;
                    LastActivityTime = DateTime.Now;
                    return jumpAdded;
                }
            }

            // Flush pending jump if an unrelated line arrived
            if (_pendingJumpEvt != null)
            {
                var evt = _pendingJumpEvt;
                _pendingJumpEvt = null;
                LastActivityTime = DateTime.Now;
                EventsCaptured++;
                if (ApplyEventToProfile(evt, isInitialScan))
                {
                    jumpAdded = true;
                }
                OnConsoleEvent?.Invoke(evt);
            }

            // 5. Generic JumpStats pattern
            var mGen = GenericJumpStatsPattern.Match(line);
            if (mGen.Success)
            {
                float.TryParse(mGen.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float dist);
                int.TryParse(mGen.Groups[2].Value, out int strafes);
                float.TryParse(mGen.Groups[3].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float sync);
                float.TryParse(mGen.Groups[4].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float pre);

                var evt = new CS2ConsoleEvent
                {
                    RawLine = rawLine,
                    IsJumpStat = true,
                    Distance = dist,
                    Strafes = strafes,
                    Sync = sync,
                    PreSpeed = pre,
                    MapName = CurrentMap,
                    Timestamp = DateTime.Now
                };

                LastActivityTime = DateTime.Now;
                EventsCaptured++;
                if (ApplyEventToProfile(evt, isInitialScan))
                {
                    jumpAdded = true;
                }
                OnConsoleEvent?.Invoke(evt);
                return jumpAdded;
            }

            // 5. Map Finish
            var mFinish = MapFinishPattern.Match(line);
            if (mFinish.Success)
            {
                var evt = new CS2ConsoleEvent
                {
                    RawLine = rawLine,
                    IsMapFinished = true,
                    MapName = mFinish.Groups[1].Value,
                    FinishTime = mFinish.Groups[2].Value,
                    IsPro = !mFinish.Groups[3].Value.Equals("tp", StringComparison.OrdinalIgnoreCase),
                    Timestamp = DateTime.Now
                };
                LastActivityTime = DateTime.Now;
                EventsCaptured++;
                ApplyEventToProfile(evt, isInitialScan);
                OnConsoleEvent?.Invoke(evt);
                return false;
            }

            // 6. Server Rank / Stats Output (from !stats, !top, !profile in chat)
            var mRank = ServerStatsPattern.Match(line);
            if (mRank.Success && int.TryParse(mRank.Groups[1].Value, out int rk) && rk > 0)
            {
                UserProfile.Instance.Cybershoke.GlobalRankPosition = rk;
                UserProfile.Instance.Cybershoke.IsLinked = true;
                UserProfile.Instance.Cybershoke.LastSyncTime = DateTime.Now;
                UserProfile.Save();
            }

            return jumpAdded;
        }

        public static bool IsPlayerMatch(string jumpNick, string myNick)
        {
            if (string.IsNullOrWhiteSpace(jumpNick)) return true;
            if (string.IsNullOrWhiteSpace(myNick) || myNick == "CS2_Player" || myNick == "Player") return true;

            string cleanJump = CS2ConfigImporter.SanitizeNick(Regex.Replace(jumpNick, @"^\[.*?\]|\{.*?\}|\(.*?\)", "").Trim());
            string cleanMy = CS2ConfigImporter.SanitizeNick(Regex.Replace(myNick, @"^\[.*?\]|\{.*?\}|\(.*?\)", "").Trim());

            if (string.Equals(cleanJump, cleanMy, StringComparison.OrdinalIgnoreCase)) return true;
            if (cleanJump.IndexOf(cleanMy, StringComparison.OrdinalIgnoreCase) >= 0 || cleanMy.IndexOf(cleanJump, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            string simJump = cleanJump.Replace(" ", "").Replace("_", "").Replace(".", "");
            string simMy = cleanMy.Replace(" ", "").Replace("_", "").Replace(".", "");
            if (simJump.Equals(simMy, StringComparison.OrdinalIgnoreCase) || simJump.Contains(simMy, StringComparison.OrdinalIgnoreCase) || simMy.Contains(simJump, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static bool ApplyEventToProfile(CS2ConsoleEvent evt, bool isInitialScan)
        {
            var prof = UserProfile.Instance;
            var cs = prof.Cybershoke;

            if (evt.IsJumpStat && evt.Distance > 140.0f)
            {
                string sig = ComputeJumpSignature(evt);
                if (_knownJumpSignatures.Contains(sig))
                {
                    return false;
                }

                string targetNick = !string.IsNullOrEmpty(cs.CybershokeNick) ? cs.CybershokeNick : DetectedNick;
                bool isMe = AppConfig.Instance.CaptureAllConsoleJumps ||
                            string.IsNullOrEmpty(evt.PlayerNick) || 
                            cs.RecentJumps.Count == 0 ||
                            IsPlayerMatch(evt.PlayerNick, targetNick);

                if (isMe)
                {
                    if (!string.IsNullOrEmpty(evt.PlayerNick) && (string.IsNullOrEmpty(cs.CybershokeNick) || cs.CybershokeNick == "CS2_Player" || cs.CybershokeNick == "Player" || cs.RecentJumps.Count == 0))
                    {
                        cs.CybershokeNick = evt.PlayerNick;
                    }

                    _knownJumpSignatures.Add(sig);
                    bool isPB = cs.ProcessJump(evt, isInitialScan);

                    if (isPB && !isInitialScan)
                    {
                        AudioEngine.PlayPBSound();
                    }

                    return true;
                }
                else
                {
                    cs.ForeignJumpsFiltered++;
                }
            }

            if (evt.IsMapFinished)
            {
                if (evt.IsPro) cs.MapsCompletedPro++;
                else cs.MapsCompletedTp++;
                cs.IsLinked = true;
                cs.LastSyncTime = DateTime.Now;
                UserProfile.Save();
                return true;
            }

            return false;
        }

        public static string FindCS2GameDirectory()
        {
            if (!OperatingSystem.IsWindows()) return "";

            // 1. Check currently running cs2.exe process path
            try
            {
                var procs = System.Diagnostics.Process.GetProcessesByName("cs2");
                if (procs.Length > 0 && procs[0].MainModule?.FileName is string procPath)
                {
                    string? cur = Path.GetDirectoryName(procPath);
                    while (!string.IsNullOrEmpty(cur))
                    {
                        if (File.Exists(Path.Combine(cur, "game", "csgo", "console.log")) ||
                            Directory.Exists(Path.Combine(cur, "game", "csgo")))
                        {
                            return cur;
                        }
                        cur = Path.GetDirectoryName(cur);
                    }
                }
            }
            catch { }

            // 2. Check Steam Registry Keys
            string[] registryPaths = { @"Software\Valve\Steam", @"SOFTWARE\WOW6432Node\Valve\Steam", @"SOFTWARE\Valve\Steam" };
            foreach (var rPath in registryPaths)
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(rPath) ?? Registry.LocalMachine.OpenSubKey(rPath);
                    if (key?.GetValue("SteamPath") is string steamPath)
                    {
                        steamPath = steamPath.Replace('/', '\\');
                        string p1 = Path.Combine(steamPath, "steamapps", "common", "Counter-Strike Global Offensive");
                        if (Directory.Exists(p1)) return p1;

                        string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                        if (File.Exists(vdfPath))
                        {
                            foreach (var line in File.ReadAllLines(vdfPath))
                            {
                                var m = Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"");
                                if (m.Success)
                                {
                                    string libPath = m.Groups[1].Value.Replace("\\\\", "\\").Replace('/', '\\');
                                    string cs2Path = Path.Combine(libPath, "steamapps", "common", "Counter-Strike Global Offensive");
                                    if (Directory.Exists(cs2Path)) return cs2Path;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // 3. Scan all available drives for standard Steam library folders
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    string d = drive.RootDirectory.FullName.TrimEnd('\\');

                    string[] candidates = {
                        $@"{d}\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
                        $@"{d}\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive",
                        $@"{d}\Program Files\Steam\steamapps\common\Counter-Strike Global Offensive",
                        $@"{d}\Steam\steamapps\common\Counter-Strike Global Offensive",
                        $@"{d}\Games\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
                        $@"{d}\Games\Steam\steamapps\common\Counter-Strike Global Offensive",
                        $@"{d}\SteamGames\steamapps\common\Counter-Strike Global Offensive",
                    };

                    foreach (var p in candidates)
                    {
                        if (Directory.Exists(p)) return p;
                    }
                }
            }
            catch { }

            return "";
        }

        public static string FindCS2ConsoleLogFile()
        {
            if (!string.IsNullOrEmpty(AppConfig.Instance.CustomConsoleLogPath) && File.Exists(AppConfig.Instance.CustomConsoleLogPath))
            {
                return AppConfig.Instance.CustomConsoleLogPath;
            }

            string gameDir = FindCS2GameDirectory();
            if (string.IsNullOrEmpty(gameDir)) return "";

            string[] candidates =
            {
                Path.Combine(gameDir, "game", "csgo", "console.log"),
                Path.Combine(gameDir, "game", "csgo", "conlog.txt"),
                Path.Combine(gameDir, "game", "csgo", "console.txt"),
                Path.Combine(gameDir, "csgo", "console.log"),
                Path.Combine(gameDir, "csgo", "conlog.txt"),
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            string defaultExpected = Path.Combine(gameDir, "game", "csgo", "console.log");
            if (Directory.Exists(Path.Combine(gameDir, "game", "csgo")))
            {
                return defaultExpected;
            }

            return "";
        }
    }
}
