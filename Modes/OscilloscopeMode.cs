using System;
using System.Collections.Generic;
using System.Linq;
using Raylib_cs;
using LJTrainer.Core;
using LJTrainer.UI;

namespace LJTrainer.Modes
{
    public class OscilloscopeMode
    {
        private readonly List<WavePoint> _history = new();
        private const int MaxHistory = 240;

        private float _liveSyncPct = 0;
        private float _liveGain = 0;
        private float _liveOverlapMs = 0;
        private float _liveDeadAirMs = 0;

        public void Reset()
        {
            _history.Clear();
            _liveSyncPct = 0;
            _liveGain = 0;
            _liveOverlapMs = 0;
            _liveDeadAirMs = 0;
        }

        public void Update(float frameDt)
        {
            var inp = InputManager.Instance;
            var cfg = AppConfig.Instance;

            float deltaYaw = inp.DeltaYawDegrees;
            bool isOverlap = inp.KeyA && inp.KeyD;
            bool isDeadAir = !inp.KeyA && !inp.KeyD;
            bool isSync = (inp.KeyA && !inp.KeyD && deltaYaw > 0) || (inp.KeyD && !inp.KeyA && deltaYaw < 0);

            // Estimated instantaneous gain assuming base speed ~300 u/s
            float instantGain = 0;
            if (isSync && MathF.Abs(deltaYaw) > 0.05f)
            {
                instantGain = Math.Clamp(cfg.AirAccelerate * 30.0f * (1.0f / 128.0f) * 0.85f, 0.0f, 2.5f);
            }

            if (isOverlap && _history.Count % 12 == 0)
            {
                AudioEngine.PlayOverlapBuzz();
            }

            _history.Add(new WavePoint
            {
                MouseYawVelocity = deltaYaw,
                KeyA = inp.KeyA,
                KeyD = inp.KeyD,
                IsSync = isSync,
                IsOverlap = isOverlap,
                InstantGain = instantGain
            });

            if (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }

            // Compute rolling stats over last 60 samples
            int window = Math.Min(60, _history.Count);
            if (window > 5)
            {
                var subset = _history.TakeLast(window).ToList();
                int syncCount = subset.Count(p => p.IsSync);
                int overlapCount = subset.Count(p => p.IsOverlap);
                int deadAirCount = subset.Count(p => !p.KeyA && !p.KeyD);

                _liveSyncPct = (float)syncCount / window * 100.0f;
                _liveGain = subset.Average(p => p.InstantGain) * 128.0f; // scaled to u/s
                _liveOverlapMs = (overlapCount / 128.0f) * 1000.0f;
                _liveDeadAirMs = (deadAirCount / 128.0f) * 1000.0f;
            }
        }

        public void Draw(int screenWidth, int screenHeight)
        {
            float scale = AppConfig.Instance.UiScale;
            int margin = (int)(16 * scale);
            int topBarY = (int)(46 * scale);

            int oscW = screenWidth - margin * 2;
            int oscH = screenHeight - topBarY - (int)(34 * scale);

            OscilloscopeView.Draw(_history, margin, topBarY, oscW, oscH, _liveSyncPct, _liveGain, _liveOverlapMs, _liveDeadAirMs);
        }
    }
}
