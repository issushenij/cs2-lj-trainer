using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Raylib_cs;
using LJTrainer.Core;
using LJTrainer.UI;

namespace LJTrainer.Modes
{
    public struct TrailPoint
    {
        public Vector2 Pos;
        public float Age;
        public float Smoothness;
        public Color Col;
    }

    public struct ReversalParticle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public Color Col;
    }

    public class CompletedStrafeRecord
    {
        public int Index;
        public string Key = "";
        public float DurationMs;
        public float TargetDurationMs;
        public float AngleWidthDeg;
        public float SyncPct;
        public float BadAnglesPct;
        public float OverlapMs;
        public float DeadAirMs;
        public float EstGain;
        public float EstLoss;
        public bool IsOptimalAngle;
        public bool IsOptimalPace;
    }

    public class CadenceLabMode
    {
        public bool ShowHistoryModal { get; set; } = false;
        public bool IsTrainingRunning { get; set; } = false;

        // Follower Ball state
        private Vector2 _ballPos;
        private readonly List<TrailPoint> _trail = new();
        private const int MaxTrailPoints = 75;
        private readonly List<ReversalParticle> _particles = new();

        // Active Strafe Tracking State
        private int _totalStrafesCompleted = 0;
        private int _perfectStreak = 0;
        private int _bestStreak = 0;
        private float _currentStrafeTimer = 0.0f;
        private float _currentStrafeAccumAngle = 0.0f;
        private int _currentStrafeSyncTicks = 0;
        private int _currentStrafeBadTicks = 0;
        private int _currentStrafeTotalTicks = 0;
        private float _currentStrafeGain = 0.0f;
        private float _currentStrafeLoss = 0.0f;
        private bool? _currentStrafeIsRight = null;
        private float _lastReversalBallX = 0.0f;

        // Screen Edge Glow Gradient Vignette State (Left & Right Reversal Feedback)
        private float _leftEdgeGlowAlpha = 0.0f;
        private Color _leftEdgeGlowColor = Theme.NeonGreen;
        private float _rightEdgeGlowAlpha = 0.0f;
        private Color _rightEdgeGlowColor = Theme.NeonGreen;

        // Reversal Overlap & Dead Air micro-detectors
        private float _lastOverlapTimer = 0.0f;
        private float _lastDeadAirTimer = 0.0f;
        private string _reversalFeedbackText = "";
        private Color _reversalFeedbackCol = Theme.NeonGreen;
        private float _reversalFeedbackAnim = 0.0f;

        // Real-Time Intelligent Strafe Sync Coach / Advisor State (No flickering, rolling window analytics)
        private string _coachAdviceCurrent = "Выполните 3-5 стрейфов для калибровки и анализа вашей синхронизации...";
        private string _coachAdviceTarget = "";
        private Color _coachAdviceColor = Theme.TextMuted;
        private Color _coachAdviceTargetColor = Theme.TextMuted;
        private float _coachFadeAlpha = 1.0f;
        private float _coachUpdateTimer = 0.0f;

        // Previous delta for jerk / smoothness computation
        private float _prevDeltaYaw = 0.0f;

        // History of completed strafes
        private readonly List<CompletedStrafeRecord> _recentStrafes = new();
        public IReadOnlyList<CompletedStrafeRecord> RecentStrafes => _recentStrafes;
        private const int MaxStrafeHistory = 20;

        // Active Rolling Sync buffer
        private readonly Queue<bool> _activeSyncBuffer = new();
        private float _liveRollingSyncPct = 0.0f;
        private float _smoothedAvgSync = 0.0f;

        // Idle / Stray Mouse Movement Filter (Ignore random mouse twitches when not pressing A/D)
        private float _timeSinceLastMovementKey = 0.0f;
        private bool _currentStrafeHadMovementKey = false;
        private bool _isIdleStandby = false;
        public bool IsIdleStandby => _isIdleStandby;

        // Metronome Oscillator & Freestyle duration state
        private float _metroTimer = 0.0f;
        private bool _metroSideRight = false;
        private float _metroPulseAnim = 0.0f;
        private float _freestyleAvgStrafeDurationMs = 95.0f;

        // Spring animation state for WASD keys
        private float _keyAnimW = 0.0f;
        private float _keyAnimA = 0.0f;
        private float _keyAnimS = 0.0f;
        private float _keyAnimD = 0.0f;
        private float _keyAnimDuck = 0.0f;
        private float _keyAnimJump = 0.0f;
        private float _ambientTime = 0.0f;

        // Structured 5-Minute Daily Routine State
        public int RoutinePhase { get; private set; } = 0; // 0 = Warmup, 1 = Angles, 2 = Sprint, 3 = Exam, 4 = Results
        public float RoutineTimer { get; private set; } = 0.0f;
        public readonly List<float> RoutineExamDistances = new();
        public bool ShowRoutineResultsModal { get; set; } = false;

        // Zen Mode Auto-fade
        private float _zenFadeAlpha = 1.0f;

        // Flight Attempt Simulation State
        public bool FlightActive { get; private set; } = false;
        public float FlightAirTime { get; private set; } = 0.0f;
        public float FlightSimSpeed { get; private set; } = 276.0f;
        public float FlightSimDistance { get; private set; } = 0.0f;
        public string FlightResultRating { get; private set; } = "";
        public readonly List<TickSample> FlightSamples = new();

        public CadenceLabMode()
        {
            _ballPos = new Vector2(690, 360);
        }

        public void Reset()
        {
            _totalStrafesCompleted = 0;
            _perfectStreak = 0;
            _currentStrafeTimer = 0.0f;
            _currentStrafeAccumAngle = 0.0f;
            _currentStrafeSyncTicks = 0;
            _currentStrafeBadTicks = 0;
            _currentStrafeTotalTicks = 0;
            _currentStrafeGain = 0.0f;
            _currentStrafeLoss = 0.0f;
            _currentStrafeIsRight = null;
            _prevDeltaYaw = 0.0f;
            _recentStrafes.Clear();
            _activeSyncBuffer.Clear();
            _liveRollingSyncPct = 0.0f;
            _smoothedAvgSync = 0.0f;
            _metroTimer = 0.0f;
            _trail.Clear();
            _particles.Clear();
            _reversalFeedbackAnim = 0.0f;
            _leftEdgeGlowAlpha = 0.0f;
            _rightEdgeGlowAlpha = 0.0f;
            _coachAdviceCurrent = "Выполните 3-5 стрейфов для калибровки и анализа вашей синхронизации...";
            _coachAdviceTarget = "";
            _coachAdviceColor = Theme.TextMuted;
            _coachAdviceTargetColor = Theme.TextMuted;
            _coachFadeAlpha = 1.0f;
            _coachUpdateTimer = 0.0f;
            FlightActive = false;
            FlightAirTime = 0.0f;
            FlightSimDistance = 0.0f;
            FlightSamples.Clear();
        }

        public void Update(float frameDt)
        {
            var inp = InputManager.Instance;
            var cfg = AppConfig.Instance;

            int screenW = Raylib.GetScreenWidth();
            int screenH = Raylib.GetScreenHeight();

            if (_ballPos == Vector2.Zero)
            {
                _ballPos = new Vector2(screenW / 2.0f, screenH * 0.35f);
            }

            if (inp.KeyRestart)
            {
                Reset();
                return;
            }

            // Metronome pacing timer (only in fixed metronome flow mode)
            float beatInterval = cfg.TargetStrafeDurationMs / 1000.0f;
            _ambientTime += frameDt;

            bool runMetronome = cfg.MetronomeEnabled;

            if (IsTrainingRunning && runMetronome)
            {
                _metroTimer += frameDt;

                if (_metroTimer >= beatInterval)
                {
                    _metroTimer -= beatInterval;
                    _metroSideRight = !_metroSideRight;
                    _metroPulseAnim = 1.0f;

                    if (cfg.SoundEnabled && inp.CursorLocked)
                    {
                        AudioEngine.PlayMetronomeTick(cfg.SoundPresetIndex, _metroSideRight);
                    }
                }
            }
            else
            {
                _metroTimer = 0.0f;
            }
            _metroPulseAnim = MathF.Max(0.0f, _metroPulseAnim - frameDt * 6.0f);

            // CS2 Mouse Delta (Degrees):
            float deltaYaw = inp.DeltaYawDegrees;
            float rawDeltaX = inp.RawDeltaX;
            float rawDeltaY = inp.RawDeltaY;

            // Move free follower ball on screen using CS2 angular yaw / pitch mapping:
            // At 90 deg CS2 FOV on screenW, deltaYaw rotates across the monitor proportional to sensitivity!
            float fovDegrees = 90.0f;
            float pxPerDegreeX = (screenW * 0.95f) / fovDegrees;
            float pxPerDegreeY = (screenH * 0.85f) / 70.0f;
            float ballDeltaX = deltaYaw * pxPerDegreeX;
            float ballDeltaY = -inp.DeltaPitchDegrees * pxPerDegreeY;

            _ballPos.X = Math.Clamp(_ballPos.X + ballDeltaX, 30.0f, screenW - 30.0f);
            _ballPos.Y = Math.Clamp(_ballPos.Y + ballDeltaY, 60.0f, screenH - 50.0f);

            // Key Spring Animations
            _keyAnimW += ((inp.KeyW ? 1.0f : 0.0f) - _keyAnimW) * 0.35f;
            _keyAnimA += ((inp.KeyA ? 1.0f : 0.0f) - _keyAnimA) * 0.40f;
            _keyAnimS += ((inp.KeyS ? 1.0f : 0.0f) - _keyAnimS) * 0.35f;
            _keyAnimD += ((inp.KeyD ? 1.0f : 0.0f) - _keyAnimD) * 0.40f;
            _keyAnimDuck += ((inp.KeyDuck ? 1.0f : 0.0f) - _keyAnimDuck) * 0.35f;
            _keyAnimJump += ((inp.KeyJump ? 1.0f : 0.0f) - _keyAnimJump) * 0.35f;

            // Key Engagement & Auto-Idle Standby Detection (Filters out wandering mouse without keys)
            bool isKeyEngaged = inp.KeyA || inp.KeyD || inp.KeyJump || inp.KeyDuck;
            if (isKeyEngaged)
            {
                _timeSinceLastMovementKey = 0.0f;
                _currentStrafeHadMovementKey = true;
                _isIdleStandby = false;
            }
            else
            {
                _timeSinceLastMovementKey += frameDt;
                if (_timeSinceLastMovementKey > 1.8f)
                {
                    _isIdleStandby = true;
                }
            }

            // Overlap & Dead Air micro-detection
            bool isOverlap = inp.KeyA && inp.KeyD;
            bool isDeadAir = !inp.KeyA && !inp.KeyD && (MathF.Abs(deltaYaw) > 0.015f || MathF.Abs(rawDeltaX) > 0.5f);

            if (isOverlap && !_isIdleStandby) _lastOverlapTimer += frameDt * 1000.0f;
            if (isDeadAir && !_isIdleStandby) _lastDeadAirTimer += frameDt * 1000.0f;

            // Compute Motion Smoothness & Jerk:
            float jerk = MathF.Abs(deltaYaw - _prevDeltaYaw);
            _prevDeltaYaw = deltaYaw;

            bool isMovingLeft = deltaYaw < -0.015f || rawDeltaX < -0.5f;
            bool isMovingRight = deltaYaw > 0.015f || rawDeltaX > 0.5f;
            bool hasActiveInput = inp.KeyA || inp.KeyD || (MathF.Abs(deltaYaw) > 0.015f && !_isIdleStandby);

            // Sync Condition:
            bool isSync = (inp.KeyA && !inp.KeyD && isMovingLeft) ||
                          (inp.KeyD && !inp.KeyA && isMovingRight);

            // Bad Angle / Sudden Jerk check:
            bool isBadAngle = (jerk > 6.5f) || (inp.KeyA && isMovingRight) || (inp.KeyD && isMovingLeft) || isOverlap;

            // Smoothness Score: 1.0 (smooth harmonic motion) down to 0.0 (jerky/desync)
            float smoothness = 0.0f;
            if (isSync)
            {
                smoothness = Math.Clamp(1.0f - (jerk / 8.0f), 0.25f, 1.0f);
            }

            // Determine Trail Color:
            Color trailCol;
            if (isBadAngle || (!isSync && hasActiveInput))
            {
                trailCol = Theme.NeonRed;
            }
            else if (smoothness >= 0.75f)
            {
                trailCol = Theme.NeonGreen;
            }
            else if (smoothness >= 0.40f)
            {
                trailCol = Theme.NeonCyan;
            }
            else
            {
                trailCol = Theme.NeonGold;
            }

            // Add point to trail
            if (hasActiveInput || _trail.Count == 0 || Vector2.Distance(_ballPos, _trail[0].Pos) > 4.0f)
            {
                _trail.Insert(0, new TrailPoint
                {
                    Pos = _ballPos,
                    Age = 0.0f,
                    Smoothness = smoothness,
                    Col = trailCol
                });

                if (_trail.Count > MaxTrailPoints)
                {
                    _trail.RemoveAt(_trail.Count - 1);
                }
            }

            // Update trail ages
            for (int i = 0; i < _trail.Count; i++)
            {
                var tp = _trail[i];
                tp.Age += frameDt;
                _trail[i] = tp;
            }

            // Update reversal particles
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Pos += p.Vel * frameDt;
                p.Life -= frameDt * 2.5f;
                if (p.Life <= 0.0f)
                {
                    _particles.RemoveAt(i);
                }
                else
                {
                    _particles[i] = p;
                }
            }

            // Update active rolling sync buffer
            if (hasActiveInput)
            {
                _activeSyncBuffer.Enqueue(isSync);
                if (_activeSyncBuffer.Count > 160)
                {
                    _activeSyncBuffer.Dequeue();
                }
            }

            if (_activeSyncBuffer.Count > 0)
            {
                int syncCount = _activeSyncBuffer.Count(s => s);
                _liveRollingSyncPct = (float)syncCount / _activeSyncBuffer.Count * 100.0f;
            }
            else if (_recentStrafes.Count > 0)
            {
                _liveRollingSyncPct = _recentStrafes.Average(s => s.SyncPct);
            }

            float targetAvgSync = _recentStrafes.Count > 0 ? _recentStrafes.Average(s => s.SyncPct) : _liveRollingSyncPct;
            _smoothedAvgSync += (targetAvgSync - _smoothedAvgSync) * 0.12f;

            // Physical CS2 Air Acceleration Gain & Loss Calculation:
            float nominalAirAccel = cfg.AirAccelerate; // 100 in CKZ, 12 in Vanilla
            float maxTickGain = (nominalAirAccel * 30.0f / 128.0f) * 0.92f;

            if (isSync && MathF.Abs(deltaYaw) > 0.015f)
            {
                // Optimal angular sweep efficiency: sweet spot around ~30-35 deg per strafe
                float absTurn = MathF.Abs(deltaYaw);
                float angleEff = Math.Clamp(1.0f - MathF.Abs(absTurn - 0.26f) * 1.6f, 0.45f, 1.0f);
                float tickGain = maxTickGain * angleEff * (frameDt * 128.0f);
                _currentStrafeGain += Math.Clamp(tickGain, 0.0f, 2.5f);
            }
            else
            {
                // Direct Physical Losses:
                float tickLoss = 0.0f;

                // 1. Counter-strafe (holding opposite key against mouse direction):
                if ((inp.KeyA && isMovingRight) || (inp.KeyD && isMovingLeft))
                {
                    tickLoss += 0.22f * (nominalAirAccel / 100.0f);
                }

                // 2. Key Overlap (holding A and D simultaneously):
                if (isOverlap)
                {
                    tickLoss += 0.18f;
                }

                // 3. Over-turning (> 40 deg excessive sweep):
                if (_currentStrafeAccumAngle > 40.0f)
                {
                    tickLoss += 0.08f;
                }

                // 4. Dead Air (mouse turning with no active key):
                if (isDeadAir)
                {
                    tickLoss += 0.03f;
                }

                _currentStrafeLoss += tickLoss * (frameDt * 128.0f);
            }

            // Direction reversal state detection based on User Setting:
            bool? dirNow = null;
            if (cfg.FreestyleTrigger == ReversalTriggerMode.ByMouseMovement)
            {
                // Default: Triggers precisely when mouse movement changes sign/direction
                if (deltaYaw > 0.030f || rawDeltaX > 0.5f) dirNow = true;        // Right
                else if (deltaYaw < -0.030f || rawDeltaX < -0.5f) dirNow = false; // Left
                else dirNow = _currentStrafeIsRight;
            }
            else // ByKeyPress
            {
                // Triggers when user presses opposite key (A <-> D)
                if (inp.KeyD && !inp.KeyA) dirNow = true;        // Right
                else if (inp.KeyA && !inp.KeyD) dirNow = false; // Left
                else dirNow = _currentStrafeIsRight;
            }

            if (_currentStrafeIsRight == null && dirNow != null)
            {
                _currentStrafeIsRight = dirNow;
                _lastReversalBallX = _ballPos.X;
                _currentStrafeTimer = 0.0f;
                _currentStrafeAccumAngle = 0.0f;
                _currentStrafeSyncTicks = 0;
                _currentStrafeBadTicks = 0;
                _currentStrafeTotalTicks = 0;
                _currentStrafeGain = 0.0f;
                _currentStrafeLoss = 0.0f;
                _lastOverlapTimer = 0.0f;
                _lastDeadAirTimer = 0.0f;

                if (!runMetronome && cfg.SoundEnabled && inp.CursorLocked)
                {
                    AudioEngine.PlayMetronomeTick(cfg.SoundPresetIndex, dirNow.Value);
                }
            }
            else if (_currentStrafeIsRight != null && dirNow != null && dirNow != _currentStrafeIsRight)
            {
                // Play sound tick on reversal when metronome is not active
                if (!runMetronome && cfg.SoundEnabled && inp.CursorLocked)
                {
                    AudioEngine.PlayMetronomeTick(cfg.SoundPresetIndex, dirNow.Value);
                    _metroPulseAnim = 1.0f;
                }

                // STRAFE COMPLETED ONLY IF MOVEMENT KEYS WERE ENGAGED (filters out wandering mouse without keys)!
                bool validStrafeEngagement = _currentStrafeHadMovementKey && !_isIdleStandby && _currentStrafeAccumAngle >= 10.0f;
                if (_currentStrafeTotalTicks >= 3 && validStrafeEngagement)
                {
                    _totalStrafesCompleted++;
                    float durMs = _currentStrafeTimer * 1000.0f;
                    _freestyleAvgStrafeDurationMs += (durMs - _freestyleAvgStrafeDurationMs) * 0.30f;
                    float targetDurMs = cfg.TargetStrafeDurationMs;
                    float syncPct = _currentStrafeTotalTicks > 0 ? (float)_currentStrafeSyncTicks / _currentStrafeTotalTicks * 100.0f : 0.0f;
                    float badPct = _currentStrafeTotalTicks > 0 ? (float)_currentStrafeBadTicks / _currentStrafeTotalTicks * 100.0f : 0.0f;
                    float widthDeg = _currentStrafeAccumAngle;

                    bool isOptimalAngle = widthDeg >= 26.0f && widthDeg <= 42.0f;
                    bool isOptimalPace = MathF.Abs(durMs - targetDurMs) <= 25.0f;

                    if (isOptimalAngle && isOptimalPace && syncPct >= 75.0f && _lastOverlapTimer <= 5.0f)
                    {
                        _perfectStreak++;
                        if (_perfectStreak > _bestStreak) _bestStreak = _perfectStreak;
                    }
                    else
                    {
                        _perfectStreak = 0;
                    }

                    // Reversal Diagnostic feedback
                    if (_lastOverlapTimer > 8.0f)
                    {
                        _reversalFeedbackText = $"[OVERLAP: {_lastOverlapTimer:F0}ms SPEED DROP]";
                        _reversalFeedbackCol = Theme.NeonRed;
                    }
                    else if (_lastDeadAirTimer > 18.0f)
                    {
                        _reversalFeedbackText = $"[DEAD AIR: {_lastDeadAirTimer:F0}ms DELAY]";
                        _reversalFeedbackCol = Theme.NeonGold;
                    }
                    else
                    {
                        _reversalFeedbackText = "[CLEAN REVERSAL SWITCH]";
                        _reversalFeedbackCol = Theme.NeonGreen;
                    }
                    _reversalFeedbackAnim = 1.0f;

                    // Trigger smooth screen edge glow gradient vignette on the completed side:
                    if (_currentStrafeIsRight == false) // Left strafe completed
                    {
                        if (isOptimalAngle && syncPct >= 75.0f && _lastOverlapTimer <= 5.0f)
                            _leftEdgeGlowColor = Theme.NeonGreen;
                        else if (widthDeg < 25.0f)
                            _leftEdgeGlowColor = Theme.NeonCyan;
                        else
                            _leftEdgeGlowColor = Theme.NeonRed;
                        _leftEdgeGlowAlpha = 1.0f;
                    }
                    else if (_currentStrafeIsRight == true) // Right strafe completed
                    {
                        if (isOptimalAngle && syncPct >= 75.0f && _lastOverlapTimer <= 5.0f)
                            _rightEdgeGlowColor = Theme.NeonGreen;
                        else if (widthDeg < 25.0f)
                            _rightEdgeGlowColor = Theme.NeonCyan;
                        else
                            _rightEdgeGlowColor = Theme.NeonRed;
                        _rightEdgeGlowAlpha = 1.0f;
                    }

                    // 3-Tier Biofeedback:
                    // Clean / Perfect: Bright chime
                    // Minor Desync / Slight inaccuracy: Same chime lowered by 3 semitones
                    // Hard error / Bad collision: Low dull thud
                    AudioEngine.BiofeedbackTier bioTier;
                    if (syncPct >= 80.0f && _lastOverlapTimer <= 5.0f && isOptimalAngle)
                    {
                        bioTier = AudioEngine.BiofeedbackTier.Clean;
                    }
                    else if (syncPct >= 65.0f && _lastOverlapTimer <= 16.0f)
                    {
                        bioTier = AudioEngine.BiofeedbackTier.MinorDesync;
                    }
                    else
                    {
                        bioTier = AudioEngine.BiofeedbackTier.HardError;
                    }

                    if (cfg.AudioBiofeedback)
                    {
                        AudioEngine.PlayBiofeedback(bioTier);
                    }

                    // Spawn spark particles at reversal point
                    SpawnReversalParticles(_ballPos, _reversalFeedbackCol);

                    _recentStrafes.Insert(0, new CompletedStrafeRecord
                    {
                        Index = _totalStrafesCompleted,
                        Key = _currentStrafeIsRight == true ? "D" : "A",
                        DurationMs = durMs,
                        TargetDurationMs = targetDurMs,
                        AngleWidthDeg = widthDeg,
                        SyncPct = syncPct,
                        BadAnglesPct = badPct,
                        OverlapMs = _lastOverlapTimer,
                        DeadAirMs = _lastDeadAirTimer,
                        EstGain = _currentStrafeGain,
                        EstLoss = _currentStrafeLoss,
                        IsOptimalAngle = isOptimalAngle,
                        IsOptimalPace = isOptimalPace
                    });

                    if (_recentStrafes.Count > MaxStrafeHistory)
                    {
                        _recentStrafes.RemoveAt(_recentStrafes.Count - 1);
                    }

                    // Persistent Deep Biomechanical Profiler
                    UserProfile.Instance.RecordStrafe(
                        _currentStrafeIsRight == true,
                        syncPct,
                        widthDeg,
                        _lastOverlapTimer,
                        _lastDeadAirTimer,
                        badPct,
                        durMs,
                        _perfectStreak
                    );
                }

                // Reset for next strafe
                _currentStrafeIsRight = dirNow;
                _lastReversalBallX = _ballPos.X;
                _currentStrafeTimer = 0.0f;
                _currentStrafeAccumAngle = 0.0f;
                _currentStrafeSyncTicks = 0;
                _currentStrafeBadTicks = 0;
                _currentStrafeTotalTicks = 0;
                _currentStrafeGain = 0.0f;
                _currentStrafeLoss = 0.0f;
                _lastOverlapTimer = 0.0f;
                _lastDeadAirTimer = 0.0f;
            }

            _reversalFeedbackAnim = MathF.Max(0.0f, _reversalFeedbackAnim - frameDt * 0.8f);
            _leftEdgeGlowAlpha = MathF.Max(0.0f, _leftEdgeGlowAlpha - frameDt * 1.5f);
            _rightEdgeGlowAlpha = MathF.Max(0.0f, _rightEdgeGlowAlpha - frameDt * 1.5f);

            // Accumulate active strafe
            _currentStrafeTimer += frameDt;
            _currentStrafeAccumAngle += MathF.Abs(deltaYaw);
            _currentStrafeTotalTicks++;
            if (isSync) _currentStrafeSyncTicks++;
            if (isBadAngle) _currentStrafeBadTicks++;

            // Zen Mode Auto-fade
            if (cfg.ZenModeAutoFade && inp.CursorLocked)
            {
                float targetAlpha = hasActiveInput ? 0.18f : 1.0f;
                _zenFadeAlpha += (targetAlpha - _zenFadeAlpha) * frameDt * 3.5f;
            }
            else
            {
                _zenFadeAlpha = 1.0f;
            }

            // Intelligent Strafe Synchronization Coach Evaluation (5.0s comfortable reading hold time)
            _coachUpdateTimer += frameDt;
            if (_coachUpdateTimer >= 5.0f)
            {
                _coachUpdateTimer = 0.0f;
                string newAdvice;
                Color newColor;

                if (_recentStrafes.Count < 3)
                {
                    newAdvice = "Выполните 3-5 стрейфов для калибровки и анализа вашей синхронизации...";
                    newColor = Theme.TextMuted;
                }
                else
                {
                    var sample = _recentStrafes.Take(6).ToList();
                    float sSync = sample.Average(s => s.SyncPct);
                    float sOverlap = sample.Average(s => s.OverlapMs);
                    float sDeadAir = sample.Average(s => s.DeadAirMs);
                    float sAngle = sample.Average(s => s.AngleWidthDeg);
                    float sBadAng = sample.Average(s => s.BadAnglesPct);

                    if (sOverlap > 9.0f)
                    {
                        newAdvice = $"Залипание клавиш (Overlap {sOverlap:F0}мс): отпускайте [A] ровно в момент нажатия [D], чтобы не гасить скорость!";
                        newColor = Theme.NeonRed;
                    }
                    else if (sDeadAir > 18.0f)
                    {
                        newAdvice = $"Опоздание кнопок (Dead Air {sDeadAir:F0}мс): мышь уже летит, а кнопка запаздывает — жмите клавишу активнее на старте маха!";
                        newColor = Theme.NeonGold;
                    }
                    else if (sSync < 68.0f)
                    {
                        newAdvice = $"Рассинхрон ({sSync:F0}%): поворот мыши не совпадает с направлением A/D — начинайте мах и кнопку строго одновременно!";
                        newColor = Theme.NeonOrange;
                    }
                    else if (sAngle < 26.0f)
                    {
                        newAdvice = $"Короткий мах ({sAngle:F0}°): мышь не доходит до рамки 30°-35° — ведите руку шире для набора максимального Gain!";
                        newColor = Theme.NeonCyan;
                    }
                    else if (sAngle > 40.0f)
                    {
                        newAdvice = $"Чрезмерный занос ({sAngle:F0}°): слишком широкий поворот срывает ускорение — разворачивайте мышь строго в рамку 30°-35°!";
                        newColor = Theme.NeonOrange;
                    }
                    else if (sBadAng > 12.0f)
                    {
                        newAdvice = $"Резкие рывки ({sBadAng:F0}%): ведите мышь более плавно, без микро-дерганий руки для чистого ускорения.";
                        newColor = Theme.NeonGold;
                    }
                    else if (sSync >= 84.0f && sOverlap <= 4.0f)
                    {
                        newAdvice = $"Идеальная синхронизация ({sSync:F0}%): чистый тайминг смены и стабильный угол 30°-35°. Держите этот ритм!";
                        newColor = Theme.NeonGreen;
                    }
                    else
                    {
                        newAdvice = $"Хороший темп ({sSync:F0}% синхры): старайтесь делать смену направления еще чище, без микропауз.";
                        newColor = Theme.NeonCyan;
                    }
                }

                if (newAdvice != _coachAdviceTarget)
                {
                    _coachAdviceTarget = newAdvice;
                    _coachAdviceTargetColor = newColor;
                }
            }

            // Smooth advice text crossfade (never flickers)
            if (!string.IsNullOrEmpty(_coachAdviceTarget) && _coachAdviceTarget != _coachAdviceCurrent)
            {
                _coachFadeAlpha = MathF.Max(0.0f, _coachFadeAlpha - frameDt * 2.0f);
                if (_coachFadeAlpha <= 0.02f)
                {
                    _coachAdviceCurrent = _coachAdviceTarget;
                    _coachAdviceColor = _coachAdviceTargetColor;
                }
            }
            else
            {
                _coachFadeAlpha = MathF.Min(1.0f, _coachFadeAlpha + frameDt * 2.0f);
            }
        }

        private void SpawnReversalParticles(Vector2 pos, Color col)
        {
            var rand = new Random();
            for (int i = 0; i < 8; i++)
            {
                float angle = (float)(rand.NextDouble() * Math.PI * 2.0);
                float speed = (float)(rand.NextDouble() * 120.0 + 40.0);
                _particles.Add(new ReversalParticle
                {
                    Pos = pos,
                    Vel = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
                    Life = 1.0f,
                    Col = col
                });
            }
        }

        private void UpdateFlightAttempt(float dt)
        {
            var inp = InputManager.Instance;
            var cfg = AppConfig.Instance;

            if (!FlightActive && inp.KeyJumpPressed)
            {
                FlightActive = true;
                FlightAirTime = 0.0f;
                FlightSimSpeed = cfg.MaxPreSpeed;
                FlightSimDistance = 0.0f;
                FlightSamples.Clear();
                AudioEngine.PlayTakeoff();
            }

            if (FlightActive)
            {
                FlightAirTime += dt;
                FlightSimSpeed += _currentStrafeGain * 0.05f - _currentStrafeLoss * 0.08f;
                FlightSimDistance += FlightSimSpeed * dt;

                // End of jump (760ms or Duck pressed)
                if (FlightAirTime >= cfg.StandardAirTimeSeconds || (FlightAirTime > 0.40f && inp.KeyDuck))
                {
                    FlightActive = false;
                    AudioEngine.PlayLanding();

                    // Calculate result distance
                    float rawDist = 220.0f + (FlightSimSpeed - 250.0f) * 0.72f + (_recentStrafes.Count >= 6 ? 12.5f : 0.0f);
                    float syncBonus = (_smoothedAvgSync - 70.0f) * 0.35f;
                    FlightSimDistance = Math.Clamp(rawDist + syncBonus, 230.0f, 290.0f);

                    FlightResultRating = FlightSimDistance switch
                    {
                        >= 285.0f => "WR TIER",
                        >= 280.0f => "GODLIKE",
                        >= 275.0f => "PERFECT",
                        >= 270.0f => "IMPRESSIVE",
                        >= 260.0f => "DECENT",
                        _ => "NORMAL"
                    };
                }
            }
        }

        public void Draw(int screenWidth, int screenHeight)
        {
            var cfg = AppConfig.Instance;

            // 1. Subtle Ambient Grid
            DrawBackgroundGrid(screenWidth, screenHeight);

            // 2. Left & Right Ideal Angle Target Guide Corridors (30° - 35°)
            DrawAngleTargetGuides(screenWidth, screenHeight);

            // 3. Smooth Screen Edge Glow Gradient Vignettes (Left & Right reversal quality)
            DrawEdgeGlowVignettes(screenWidth, screenHeight);

            // 4. Smoothness Trail Line behind follower ball
            DrawSmoothnessTrail();

            // 5. Reversal Spark Particles
            DrawReversalParticles();

            // 6. Dynamic Follower Ball
            DrawFollowerBall();

            // 7. Pause Banner if training is paused
            if (!IsTrainingRunning)
            {
                DrawPauseBanner(screenWidth, screenHeight);
            }

            // 8. Apple-Style Floating Capsule (Below Screen Center)
            int capBottomY = DrawAppleGlassCapsule(screenWidth, screenHeight);

            // 9. Reversal Diagnostic Alert (Overlap / Dead Air alert)
            DrawReversalAlert(screenWidth, capBottomY);

            // 10. SKZ / KZ Server Authentic WASD Showkeys HUD (Placed Lower Down)
            DrawKZShowkeysHUD(screenWidth, capBottomY + 26);

            // 11. Intelligent Live Sync Coach / Advisor Pill (At Bottom of Screen)
            DrawLiveCoachPill(screenWidth, screenHeight);

        }

        private void DrawLiveCoachPill(int screenWidth, int screenHeight)
        {
            var cfg = AppConfig.Instance;
            float scale = cfg.UiScale;

            int pillW = Math.Min((int)(940 * scale), screenWidth - 36);
            int pillH = (int)(34 * scale);
            int pillX = (screenWidth - pillW) / 2;
            int pillY = screenHeight - (int)(44 * scale);

            // Frosted Glass Coach Pill Container
            Theme.DrawGlassPanel(pillX, pillY, pillW, pillH);

            byte alpha = (byte)Math.Clamp(255 * _coachFadeAlpha, 0, 255);
            Color textCol = new((byte)_coachAdviceColor.R, (byte)_coachAdviceColor.G, (byte)_coachAdviceColor.B, alpha);
            Color badgeCol = new((byte)Theme.NeonCyan.R, (byte)Theme.NeonCyan.G, (byte)Theme.NeonCyan.B, alpha);

            // Icon / Badge Prefix
            string badge = "COACH:";
            int badgeW = Theme.MeasureText(badge, 12);
            Theme.DrawText(badge, pillX + 14, pillY + (pillH - Theme.GetScaledFontSize(12)) / 2, 12, badgeCol);

            // Smooth advice text (smoothly crossfades, never flickers)
            Theme.DrawText(_coachAdviceCurrent, pillX + 14 + badgeW + 8, pillY + (pillH - Theme.GetScaledFontSize(12)) / 2, 12, textCol);
        }

        private void DrawBackgroundGrid(int screenWidth, int screenHeight)
        {
            int gridStep = 60;
            Color lineCol = new((byte)Theme.Border.R, (byte)Theme.Border.G, (byte)Theme.Border.B, (byte)45);
            int cx = screenWidth / 2;

            // Vertical grid lines directly centered on screen center:
            for (int x = cx; x < screenWidth; x += gridStep)
            {
                Raylib.DrawLine(x, 46, x, screenHeight, lineCol);
            }
            for (int x = cx - gridStep; x >= 0; x -= gridStep)
            {
                Raylib.DrawLine(x, 46, x, screenHeight, lineCol);
            }

            // Horizontal grid lines:
            for (int y = 46; y < screenHeight; y += gridStep)
            {
                Raylib.DrawLine(0, y, screenWidth, y, lineCol);
            }
        }

        private void DrawSmoothnessTrail()
        {
            if (_trail.Count < 2) return;

            for (int i = 0; i < _trail.Count - 1; i++)
            {
                var p0 = _trail[i];
                var p1 = _trail[i + 1];

                float progress = (float)i / _trail.Count;
                float thickness = Math.Max(1.5f, 6.5f * (1.0f - progress));
                byte alpha = (byte)Math.Clamp(235 * (1.0f - progress * 0.9f), 20, 245);

                Color col = new(p0.Col.R, p0.Col.G, p0.Col.B, alpha);
                Raylib.DrawLineEx(p0.Pos, p1.Pos, thickness, col);
            }
        }

        private void DrawReversalParticles()
        {
            foreach (var p in _particles)
            {
                byte alpha = (byte)Math.Clamp(255 * p.Life, 0, 255);
                Raylib.DrawCircle((int)p.Pos.X, (int)p.Pos.Y, 2.5f, new Color(p.Col.R, p.Col.G, p.Col.B, alpha));
            }
        }

        private void DrawFollowerBall()
        {
            var inp = InputManager.Instance;
            bool hasMotion = MathF.Abs(inp.DeltaYawDegrees) > 0.01f;
            Color ballCol = hasMotion ? (_trail.Count > 0 ? _trail[0].Col : Theme.NeonCyan) : Theme.TextDim;

            // Metronome Pulse halo
            float pulseRadius = 14.0f + _metroPulseAnim * 12.0f;
            Raylib.DrawCircleLines((int)_ballPos.X, (int)_ballPos.Y, pulseRadius, new Color(ballCol.R, ballCol.G, ballCol.B, (byte)(180 * _metroPulseAnim)));

            // Outer ring
            Raylib.DrawCircle((int)_ballPos.X, (int)_ballPos.Y, 14, new Color(ballCol.R, ballCol.G, ballCol.B, (byte)50));
            Raylib.DrawCircleLines((int)_ballPos.X, (int)_ballPos.Y, 14, ballCol);

            // Inner solid core
            Raylib.DrawCircle((int)_ballPos.X, (int)_ballPos.Y, 7, ballCol);
            Raylib.DrawCircle((int)_ballPos.X, (int)_ballPos.Y, 3, Theme.TextWhite);

            // Active Key Label next to the ball
            string keyText = inp.KeyA ? "A" : (inp.KeyD ? "D" : "");
            if (!string.IsNullOrEmpty(keyText))
            {
                Color kCol = inp.KeyA ? Theme.NeonCyan : Theme.NeonOrange;
                Raylib.DrawText(keyText, (int)_ballPos.X + 18, (int)_ballPos.Y - 8, 16, kCol);
            }
        }

        private void DrawAngleTargetGuides(int screenWidth, int screenHeight)
        {
            var cfg = AppConfig.Instance;
            float scale = cfg.UiScale;
            float fovDegrees = 90.0f;
            float pxPerDeg = (screenWidth * 0.95f) / fovDegrees;

            float curAngle = _currentStrafeAccumAngle;
            bool isOptimal = curAngle >= 27.0f && curAngle <= 38.0f;
            bool isOver = curAngle > 39.0f;

            // Dynamic Target Box (anchored relative to current strafe starting reversal point):
            if (_lastReversalBallX > 10.0f && _currentStrafeIsRight != null)
            {
                float dirSign = _currentStrafeIsRight == true ? 1.0f : -1.0f;
                float dyn30 = _lastReversalBallX + dirSign * 30.0f * pxPerDeg;
                float dyn35 = _lastReversalBallX + dirSign * 35.0f * pxPerDeg;
                float minX = MathF.Min(dyn30, dyn35);
                float maxX = MathF.Max(dyn30, dyn35);
                int boxW = (int)MathF.Max(16.0f, maxX - minX);

                // Stable fixed vertical height (does NOT jump or follow mouse up/down)
                int boxY = (int)(screenHeight * 0.26f);
                int boxH = (int)(screenHeight * 0.26f);

                Color dynCol = isOptimal ? Theme.NeonGreen : (isOver ? Theme.NeonRed : Theme.NeonGold);
                byte fillAlpha = isOptimal ? (byte)45 : (byte)22;
                byte lineAlpha = isOptimal ? (byte)230 : (byte)160;

                // Glowing target corridor box
                Raylib.DrawRectangle((int)minX, boxY, boxW, boxH, new Color((byte)dynCol.R, (byte)dynCol.G, (byte)dynCol.B, fillAlpha));
                Raylib.DrawRectangleLines((int)minX, boxY, boxW, boxH, new Color((byte)dynCol.R, (byte)dynCol.G, (byte)dynCol.B, lineAlpha));

                // Target label without any broken unicode characters
                string dynLabel = _currentStrafeIsRight == true ? "ЦЕЛЬ: 30-35 deg >>" : "<< ЦЕЛЬ: 30-35 deg";
                int dlw = Theme.MeasureText(dynLabel, 12);
                Theme.DrawText(dynLabel, (int)(minX + boxW / 2 - dlw / 2), boxY - (int)(18 * scale), 12, dynCol);
            }
        }

        private void DrawEdgeGlowVignettes(int screenWidth, int screenHeight)
        {
            int edgeW = (int)(screenWidth * 0.42f); // Extended wide subtle gradient (~42% screen)

            // 1. Left Screen Edge Vignette
            if (_leftEdgeGlowAlpha > 0.01f)
            {
                byte a = (byte)Math.Clamp(28 * _leftEdgeGlowAlpha, 0, 255); // Super soft, subtle, barely noticeable
                Color c1 = new(_leftEdgeGlowColor.R, _leftEdgeGlowColor.G, _leftEdgeGlowColor.B, a);
                Color c2 = new(_leftEdgeGlowColor.R, _leftEdgeGlowColor.G, _leftEdgeGlowColor.B, (byte)0);
                Raylib.DrawRectangleGradientH(0, 0, edgeW, screenHeight, c1, c2);
            }

            // 2. Right Screen Edge Vignette
            if (_rightEdgeGlowAlpha > 0.01f)
            {
                byte a = (byte)Math.Clamp(28 * _rightEdgeGlowAlpha, 0, 255); // Super soft, subtle, barely noticeable
                Color c1 = new(_rightEdgeGlowColor.R, _rightEdgeGlowColor.G, _rightEdgeGlowColor.B, (byte)0);
                Color c2 = new(_rightEdgeGlowColor.R, _rightEdgeGlowColor.G, _rightEdgeGlowColor.B, a);
                Raylib.DrawRectangleGradientH(screenWidth - edgeW, 0, edgeW, screenHeight, c1, c2);
            }
        }

        private void DrawPauseBanner(int screenWidth, int screenHeight)
        {
            var cfg = AppConfig.Instance;
            int cx = screenWidth / 2;
            int cy = (int)(screenHeight * 0.50f);
            string banner = "ТРЕНИРОВКА НА ПАУЗЕ — НАЖМИТЕ ПРОБЕЛ (SPACE) ДЛЯ СТАРТА";
            int bw = Theme.MeasureText(banner, 15);

            Raylib.DrawRectangle(cx - bw / 2 - 18, cy, bw + 36, 34, new Color((byte)Theme.BgDark.R, (byte)Theme.BgDark.G, (byte)Theme.BgDark.B, (byte)240));
            Raylib.DrawRectangleLines(cx - bw / 2 - 18, cy, bw + 36, 34, Theme.NeonGold);
            Theme.DrawText(banner, cx - bw / 2, cy + 8, 15, Theme.NeonGold);
        }

        private int DrawAppleGlassCapsule(int screenWidth, int screenHeight)
        {
            var cfg = AppConfig.Instance;
            float scale = cfg.UiScale;
            int capW = Math.Min((int)(1100 * scale), screenWidth - 30);
            int capH = (int)(96 * scale);
            int capX = (screenWidth - capW) / 2;
            int capY = (int)(screenHeight * 0.58f);

            // Frosted Glass Capsule Background (Glassmorphism 2.0 with Specular Highlight)
            Theme.DrawGlassPanel(capX, capY, capW, capH);

            int colCount = 6;
            int colW = capW / colCount;

            float overallAvgAngle = _recentStrafes.Count > 0 ? _recentStrafes.Average(s => s.AngleWidthDeg) : _currentStrafeAccumAngle;
            float overallBadAng = _recentStrafes.Count > 0 ? _recentStrafes.Average(s => s.BadAnglesPct) : 0.0f;
            float avgGain = _recentStrafes.Count > 0 ? _recentStrafes.Average(s => s.EstGain) : _currentStrafeGain;
            float avgLoss = _recentStrafes.Count > 0 ? _recentStrafes.Average(s => s.EstLoss) : _currentStrafeLoss;

            Vector2 mouse = Raylib.GetMousePosition();
            bool isFree = !InputManager.Instance.CursorLocked;

            // Col 1: SYNC %
            Color syncCol = _smoothedAvgSync >= 80 ? Theme.NeonGreen : (_smoothedAvgSync >= 65 ? Theme.NeonGold : Theme.NeonOrange);
            DrawCapsuleColumn(capX, capY, colW, capH, "AVG SYNC", $"{_smoothedAvgSync:F1}%", $"Live: {_liveRollingSyncPct:F0}%", syncCol, scale);
            if (isFree && cfg.ShowTooltips && mouse.X >= capX && mouse.X < capX + colW && mouse.Y >= capY && mouse.Y <= capY + capH)
            {
                Theme.DrawTooltip((int)mouse.X, (int)mouse.Y, "AVERAGE SYNCHRONIZATION", "Percentage of air ticks where mouse turning matches the active A/D key.", "> 85% is Godlike");
            }

            // Col 2: AVG ANGLE
            Color angCol = (overallAvgAngle >= 26 && overallAvgAngle <= 40) ? Theme.NeonGreen : Theme.NeonCyan;
            DrawCapsuleColumn(capX + colW, capY, colW, capH, "AVG ANGLE", $"{overallAvgAngle:F1} deg", "Target: 30-35 deg", angCol, scale);
            if (isFree && cfg.ShowTooltips && mouse.X >= capX + colW && mouse.X < capX + colW * 2 && mouse.Y >= capY && mouse.Y <= capY + capH)
            {
                Theme.DrawTooltip((int)mouse.X, (int)mouse.Y, "STRAFE SWEEP ANGLE", "Average mouse turn width in degrees. Ideal LJ angle is 30-35 deg.", "30° - 35° Sweet Spot");
            }

            // Col 3: BAD ANGLES
            Color badCol = overallBadAng <= 5.0f ? Theme.NeonGreen : (overallBadAng <= 15.0f ? Theme.NeonGold : Theme.NeonRed);
            DrawCapsuleColumn(capX + colW * 2, capY, colW, capH, "BAD ANGLES", $"{overallBadAng:F1}%", "Jerks / Jitter", badCol, scale);
            if (isFree && cfg.ShowTooltips && mouse.X >= capX + colW * 2 && mouse.X < capX + colW * 3 && mouse.Y >= capY && mouse.Y <= capY + capH)
            {
                Theme.DrawTooltip((int)mouse.X, (int)mouse.Y, "BAD ANGLES (ПЛОХИЕ УГЛЫ / ЗАНОС / РЫВКИ)", "Доля времени, когда мышь разворачивается шире 40° или делает резкие рывки. В CS2 это срывает воздушное ускорение.", "Норма: < 3.0% | Приводит к потере Gain и росту Loss");
            }

            // Col 4: GAIN / LOSS
            DrawCapsuleColumn(capX + colW * 3, capY, colW, capH, "GAIN / LOSS", $"+{avgGain:F1} / -{avgLoss:F2}", "Estimated u/s", Theme.NeonCyan, scale);
            if (isFree && cfg.ShowTooltips && mouse.X >= capX + colW * 3 && mouse.X < capX + colW * 4 && mouse.Y >= capY && mouse.Y <= capY + capH)
            {
                Theme.DrawTooltip((int)mouse.X, (int)mouse.Y, "SPEED GAIN & LOSS", "Air acceleration speed gained vs speed lost per strafe in units/sec.", "+12 to +16 u/s per strafe");
            }

            // Col 5: CADENCE OR CS2 STRAFE ESTIMATOR
            if (cfg.MetronomeEnabled)
            {
                DrawCapsuleColumn(capX + colW * 4, capY, colW, capH, "CADENCE (МЕТРОНОМ)", $"{cfg.TargetStrafeDurationMs:F0} ms", $"{cfg.CalculatedMetronomeBpm} BPM", Theme.NeonGold, scale);
                if (isFree && cfg.ShowTooltips && mouse.X >= capX + colW * 4 && mouse.X < capX + colW * 5 && mouse.Y >= capY && mouse.Y <= capY + capH)
                {
                    Theme.DrawTooltip((int)mouse.X, (int)mouse.Y, "CADENCE & RHYTHM", "Целевой темп стрейфов и частота ударов метронома в BPM.", "8 Стрейфов: 95ms (630 BPM)");
                }
            }
            else
            {
                float curDur = _recentStrafes.Count > 0 
                    ? (float)_recentStrafes.Take(3).Average(s => s.DurationMs) 
                    : (_freestyleAvgStrafeDurationMs > 20.0f ? _freestyleAvgStrafeDurationMs : 95.0f);
                float estStrafes = AppConfig.EstimateStrafesInJump(curDur);
                int liveBpm = (int)(60000.0f / Math.Max(20.0f, curDur));

                DrawCapsuleColumn(capX + colW * 4, capY, colW, capH, "CS2 ЭКВИВАЛЕНТ", $"~{estStrafes:F1} СТРЕЙФОВ", $"{curDur:F0} мс ({liveBpm} BPM)", Theme.NeonGold, scale);
                if (isFree && cfg.ShowTooltips && mouse.X >= capX + colW * 4 && mouse.X < capX + colW * 5 && mouse.Y >= capY && mouse.Y <= capY + capH)
                {
                    Theme.DrawTooltip((int)mouse.X, (int)mouse.Y, "РАСЧЁТ СТРЕЙФОВ В CS2", "Сколько стрейфов при вашей текущей скорости рук вы успеете сделать за 1 прыжок в CS2 (~800мс).", "PRO CS2: 8 - 10 стрейфов");
                }
            }

            // Col 6: STRAFES / STREAK
            string streakStr = _perfectStreak > 1 ? $"Streak: {_perfectStreak}" : "Completed";
            Color streakCol = _perfectStreak >= 5 ? Theme.NeonGold : Theme.TextWhite;
            DrawCapsuleColumn(capX + colW * 5, capY, colW, capH, "STRAFES", $"#{_totalStrafesCompleted}", streakStr, streakCol, scale);

            return capY + capH;
        }

        private void DrawCapsuleColumn(int x, int y, int w, int h, string label, string mainVal, string subVal, Color valColor, float scale)
        {
            Raylib.DrawLine(x, y + (int)(12 * scale), x, y + h - (int)(12 * scale), new Color((byte)Theme.Border.R, (byte)Theme.Border.G, (byte)Theme.Border.B, (byte)150));

            int cx = x + w / 2;

            // Label
            int lw = Theme.MeasureText(label, 12);
            Theme.DrawText(label, cx - lw / 2, y + (int)(10 * scale), 12, Theme.TextMuted);

            // Main Value
            int mw = Theme.MeasureText(mainVal, 20);
            Theme.DrawText(mainVal, cx - mw / 2, y + (int)(32 * scale), 20, valColor);

            // Sub Value
            int sw = Theme.MeasureText(subVal, 12);
            Theme.DrawText(subVal, cx - sw / 2, y + (int)(64 * scale), 12, Theme.TextDim);
        }

        private void DrawReversalAlert(int screenWidth, int topY)
        {
            if (_reversalFeedbackAnim <= 0.01f || string.IsNullOrEmpty(_reversalFeedbackText)) return;

            int cx = screenWidth / 2;
            int rtw = Theme.MeasureText(_reversalFeedbackText, 13);
            byte alpha = (byte)Math.Clamp(255 * _reversalFeedbackAnim, 0, 255);
            Color alertCol = new((byte)_reversalFeedbackCol.R, (byte)_reversalFeedbackCol.G, (byte)_reversalFeedbackCol.B, alpha);

            Theme.DrawText(_reversalFeedbackText, cx - rtw / 2, topY + 8, 13, alertCol);
        }

        private void DrawKZShowkeysHUD(int screenWidth, int topY)
        {
            var inp = InputManager.Instance;
            int cx = screenWidth / 2;

            int keyW = 46;
            int keyH = 42;
            int gap = 4;

            int row1Y = topY + 4;
            int row2Y = topY + 4 + keyH + gap;
            int row3Y = row2Y + keyH + gap;

            // Top Row: [W]
            DrawKZKey(cx - keyW / 2, row1Y, keyW, keyH, "W", inp.KeyW, _keyAnimW, false);

            // Middle Row: [A] [S] [D]
            int row2StartX = cx - (keyW * 3 + gap * 2) / 2;
            DrawKZKey(row2StartX, row2Y, keyW, keyH, "A", inp.KeyA, _keyAnimA, true);
            DrawKZKey(row2StartX + keyW + gap, row2Y, keyW, keyH, "S", inp.KeyS, _keyAnimS, false);
            DrawKZKey(row2StartX + (keyW + gap) * 2, row2Y, keyW, keyH, "D", inp.KeyD, _keyAnimD, true);

            // Bottom Row: [DUCK] [JUMP]
            int auxW = (keyW * 3 + gap * 2 - gap) / 2;
            int auxH = 26;
            DrawKZKey(row2StartX, row3Y, auxW, auxH, "DUCK", inp.KeyDuck, _keyAnimDuck, false, 11);
            DrawKZKey(row2StartX + auxW + gap, row3Y, auxW, auxH, "JUMP", inp.KeyJump, _keyAnimJump, false, 11);
        }

        private void DrawKZKey(int x, int y, int w, int h, string label, bool isPressed, float animVal, bool isStrafeKey, int fontSize = 16)
        {
            int grow = (int)(animVal * 4.0f);
            int kx = x - grow / 2;
            int ky = y - grow / 2;
            int kw = w + grow;
            int kh = h + grow;

            if (animVal > 0.05f)
            {
                byte aByte = (byte)Math.Clamp(255 * animVal, 0, 255);
                Color fillCol = isStrafeKey ? new Color((byte)245, (byte)252, (byte)255, aByte) : new Color((byte)230, (byte)240, (byte)255, aByte);
                Color borderCol = isStrafeKey ? (label == "A" ? Theme.NeonCyan : Theme.NeonOrange) : Theme.TextWhite;

                Raylib.DrawRectangle(kx, ky, kw, kh, fillCol);
                Raylib.DrawRectangleLines(kx, ky, kw, kh, borderCol);

                int tw = Raylib.MeasureText(label, fontSize);
                Raylib.DrawText(label, kx + (kw - tw) / 2, ky + (kh - fontSize) / 2 + 1, fontSize, new Color(10, 13, 20, 255));
            }
            else
            {
                Raylib.DrawRectangle(x, y, w, h, new Color(Theme.BgPanel.R, Theme.BgPanel.G, Theme.BgPanel.B, (byte)210));
                Raylib.DrawRectangleLines(x, y, w, h, Theme.Border);

                int tw = Raylib.MeasureText(label, fontSize);
                Color textCol = isStrafeKey ? Theme.TextWhite : Theme.TextMuted;
                Raylib.DrawText(label, x + (w - tw) / 2, y + (h - fontSize) / 2, fontSize, textCol);
            }
        }

        public void DrawHistoryModal(int screenWidth, int screenHeight)
        {
            int modalW = Math.Min(1100, screenWidth - 40);
            int modalH = Math.Min(580, screenHeight - 80);
            int modalX = (screenWidth - modalW) / 2;
            int modalY = (screenHeight - modalH) / 2;

            Raylib.DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 210));

            Vector2 mouse = Raylib.GetMousePosition();
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (mouse.X < modalX || mouse.X > modalX + modalW || mouse.Y < modalY || mouse.Y > modalY + modalH)
                {
                    ShowHistoryModal = false;
                    return;
                }
            }

            Raylib.DrawRectangle(modalX, modalY, modalW, modalH, new Color(Theme.BgDark.R, Theme.BgDark.G, Theme.BgDark.B, (byte)252));
            Raylib.DrawRectangleLines(modalX, modalY, modalW, modalH, Theme.NeonCyan);

            Raylib.DrawRectangle(modalX, modalY, modalW, 38, Theme.BgPanelHeader);
            Raylib.DrawRectangleLines(modalX, modalY, modalW, 38, Theme.Border);
            Theme.DrawText($"ИСТОРИЯ СТРЕЙФОВ (ВСЕГО: {_totalStrafesCompleted} | ЛУЧШИЙ СТРИК: {_bestStreak})", modalX + 16, modalY + 11, 14, Theme.TextWhite);

            if (Theme.DrawButton(modalX + modalW - 90, modalY + 6, 80, 26, "ЗАКРЫТЬ", false, 12))
            {
                ShowHistoryModal = false;
            }

            int tableY = modalY + 48;
            int tableW = modalW - 32;
            int rowH = 24;

            Raylib.DrawRectangle(modalX + 16, tableY, tableW, rowH, Theme.BgPanelHeader);
            Raylib.DrawRectangleLines(modalX + 16, tableY, tableW, rowH, Theme.Border);

            string[] heads = { "#", "Key", "Duration (ms)", "Target (ms)", "Pace Offset", "Angle Width", "Sync %", "Overlap", "Dead Air", "Gain", "Loss", "Rating" };
            int[] cols = { 40, 50, 100, 95, 100, 100, 85, 90, 90, 75, 75, 150 };

            int tx = modalX + 22;
            for (int h = 0; h < heads.Length; h++)
            {
                Theme.DrawText(heads[h], tx, tableY + 5, 11, Theme.TextMuted);
                tx += cols[h];
            }

            tableY += 26;
            int maxRows = Math.Min(_recentStrafes.Count, (modalH - 95) / rowH);

            for (int i = 0; i < maxRows; i++)
            {
                var s = _recentStrafes[i];
                Color rowBg = (i % 2 == 0) ? Theme.BgPanel : new Color(Theme.BgDark.R, Theme.BgDark.G, Theme.BgDark.B, (byte)255);
                Raylib.DrawRectangle(modalX + 16, tableY, tableW, rowH, rowBg);

                tx = modalX + 22;
                Theme.DrawText($"#{s.Index}", tx, tableY + 4, 11, Theme.TextWhite); tx += cols[0];
                Theme.DrawText(s.Key, tx, tableY + 4, 11, s.Key == "D" ? Theme.NeonOrange : Theme.NeonCyan); tx += cols[1];
                Theme.DrawText($"{s.DurationMs:F1} ms", tx, tableY + 4, 11, Theme.TextWhite); tx += cols[2];
                Theme.DrawText($"{s.TargetDurationMs:F1} ms", tx, tableY + 4, 11, Theme.TextMuted); tx += cols[3];

                float paceErr = s.DurationMs - s.TargetDurationMs;
                Color paceCol = MathF.Abs(paceErr) <= 25.0f ? Theme.NeonGreen : (paceErr > 0 ? Theme.NeonRed : Theme.NeonGold);
                Theme.DrawText($"{paceErr:+0.0;-0.0;0.0} ms", tx, tableY + 4, 11, paceCol); tx += cols[4];

                Color angCol = s.IsOptimalAngle ? Theme.NeonGreen : (s.AngleWidthDeg < 26 ? Theme.NeonCyan : Theme.NeonOrange);
                Theme.DrawText($"{s.AngleWidthDeg:F1} deg", tx, tableY + 4, 11, angCol); tx += cols[5];

                Color syncCol = s.SyncPct >= 80.0f ? Theme.NeonGreen : (s.SyncPct >= 65.0f ? Theme.NeonGold : Theme.NeonRed);
                Theme.DrawText($"{s.SyncPct:F1}%", tx, tableY + 4, 11, syncCol); tx += cols[6];

                Color overCol = s.OverlapMs <= 5.0f ? Theme.NeonGreen : Theme.NeonRed;
                Theme.DrawText($"{s.OverlapMs:F0} ms", tx, tableY + 4, 11, overCol); tx += cols[7];

                Color deadCol = s.DeadAirMs <= 15.0f ? Theme.NeonGreen : Theme.NeonGold;
                Theme.DrawText($"{s.DeadAirMs:F0} ms", tx, tableY + 4, 11, deadCol); tx += cols[8];

                Theme.DrawText($"+{s.EstGain:F1}", tx, tableY + 4, 11, Theme.NeonCyan); tx += cols[9];
                Theme.DrawText($"-{s.EstLoss:F2}", tx, tableY + 4, 11, Theme.NeonOrange); tx += cols[10];

                string rating = (s.IsOptimalPace && s.IsOptimalAngle && s.SyncPct >= 75.0f && s.OverlapMs <= 5.0f)
                    ? "[PERFECT] FLIGHT"
                    : (s.IsOptimalPace ? "[GOOD PACE]" : (s.IsOptimalAngle ? "[GOOD ANGLE]" : "ADJUST REVERSAL"));
                Color ratingCol = rating.Contains("PERFECT") ? Theme.NeonGreen : (rating.Contains("GOOD") ? Theme.NeonCyan : Theme.NeonOrange);
                Theme.DrawText(rating, tx, tableY + 4, 11, ratingCol);

                tableY += rowH + 1;
            }

            if (_recentStrafes.Count == 0)
            {
                string emptyMsg = "Начните водить мышь со стрейфами A/D для записи аналитики...";
                Theme.DrawText(emptyMsg, modalX + 25, tableY + 15, 12, Theme.TextDim);
            }
        }
    }
}
