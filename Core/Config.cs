using System;
using System.IO;
using System.Text.Json;

namespace LJTrainer.Core
{
    public enum PhysicsMode
    {
        CKZ,        // sv_airaccelerate 100, max pre 276.0
        Vanilla     // sv_airaccelerate 12, max pre 250.0
    }

    public enum AppMode
    {
        CadenceLab,         // Mode 1: Metronome Cadence Lab
        FreestyleAdaptive,  // Mode 2: Free Flow without metronome (tick on reversal + CS2 strafes estimator)
        Oscilloscope        // Mode 3: Real-time Waveforms
    }

    public enum ColorTheme
    {
        PhosphorMatrix,  // Terminal Matrix: Pure Phosphor Green (#00FF66) on Deep Carbon
        CyberCLI,        // Cyberpunk Workstation: Crisp Ice Cyan (#00E5FF) on Dark Graphite
        AmberCRT,        // Vintage Amber Terminal: Warm Amber (#FFB000) on Obsidian
        OLEDMonochrome   // Pure Black, Crisp White & Minimal Silver
    }

    public enum TrainerMode
    {
        StrafePractice     // Main Strafe Practice (Metronome toggleable ON / OFF)
    }

    public enum ReversalTriggerMode
    {
        ByMouseMovement,    // Default: Triggers when mouse changes turning direction (left <-> right)
        ByKeyPress          // Triggers when user presses opposite strafe key (A <-> D)
    }

    public class AppConfig
    {
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        public static AppConfig Instance { get; set; } = new();

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                    if (loaded != null)
                    {
                        Instance = loaded;
                    }
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
                File.WriteAllText(ConfigFilePath, json);
            }
            catch { }
        }

        // Mouse & Input
        public float Sensitivity { get; set; } = 1.0f;
        public float YawFactor { get; set; } = 0.022f; // CS2 standard m_yaw
        public int Dpi { get; set; } = 800;

        // Physics Settings
        public PhysicsMode Mode { get; set; } = PhysicsMode.CKZ;
        public int Tickrate { get; set; } = 128;
        public float Gravity { get; set; } = 800.0f;
        public float WishSpeed { get; set; } = 30.0f;
        public float MaxSpeed { get; set; } = 250.0f;
        public float JumpImpulseZ { get; set; } = 285.0f;
        public float StandardAirTimeSeconds { get; set; } = 0.760f; // 760 ms standard CS2 jump duration

        public float AirAccelerate => Mode == PhysicsMode.CKZ ? 100.0f : 12.0f;
        public float MaxPreSpeed => Mode == PhysicsMode.CKZ ? 276.0f : 250.0f;

        // Visual & UI/UX Settings
        public ColorTheme Theme { get; set; } = ColorTheme.CyberCLI;
        public TrainerMode ModeType { get; set; } = TrainerMode.StrafePractice;
        public bool MetronomeEnabled { get; set; } = false;
        public bool ShowCrtScanlines { get; set; } = false; // Authentic subtle terminal scanlines
        public ReversalTriggerMode FreestyleTrigger { get; set; } = ReversalTriggerMode.ByMouseMovement;
        public float UiScale { get; set; } = 1.50f; // 1.0f (Normal), 1.25f (Large), 1.50f (Extra Large - Default)
        public bool ShowTooltips { get; set; } = true;
        public bool ZenModeAutoFade { get; set; } = true;
        public bool ShowWelcomeGuideOnStartup { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true; // Minimize to system tray on [X] instead of closing
        public bool AutoCheckUpdates { get; set; } = true; // Automatically check for updates on startup
        public string CustomConsoleLogPath { get; set; } = "";
        public bool CaptureAllConsoleJumps { get; set; } = true; // Capture all JumpStats jumps printed to local console without strict nick filtering

        // Sound Settings
        public float MasterVolume { get; set; } = 0.75f;
        public bool SoundEnabled { get; set; } = true;
        public bool AnnouncerEnabled { get; set; } = true;
        public bool AudioBiofeedback { get; set; } = true;
        public bool AdaptiveAudioFeedback { get; set; } = true;
        public int SoundPresetIndex { get; set; } = 0; // 0 to 15 (16 sound presets)

        // Target Strafe Cadence Settings
        public int TargetStrafes { get; set; } = 8;
        public float CustomTargetDurationMs { get; set; } = 95.0f;
        public bool UseCustomDuration { get; set; } = false;

        public float TargetStrafeDurationMs
        {
            get
            {
                if (UseCustomDuration) return CustomTargetDurationMs;
                return TargetStrafes switch
                {
                    6 => 120.0f,
                    7 => 105.0f,
                    8 => 90.0f,
                    9 => 80.0f,
                    10 => 70.0f,
                    12 => 60.0f,
                    _ => (StandardAirTimeSeconds / TargetStrafes) * 1000.0f
                };
            }
            set
            {
                CustomTargetDurationMs = value;
                UseCustomDuration = true;
            }
        }

        public int CalculatedMetronomeBpm => (int)Math.Round(60000.0f / TargetStrafeDurationMs);

        /// <summary>
        /// Authentic CS2 Long Jump Strafe Count Estimator matching real KZ / LJ servers (GOKZ, SKZ, Cybershoke).
        /// Standard CS2 LJ air time with crouch-landing is ~800ms.
        /// </summary>
        public static float EstimateStrafesInJump(float strafeDurationMs)
        {
            if (strafeDurationMs <= 15.0f) return 0.0f;
            float totalAirTimeMs = 800.0f;
            float strafes = totalAirTimeMs / strafeDurationMs;
            return Math.Clamp(strafes, 1.0f, 16.0f);
        }
    }
}
