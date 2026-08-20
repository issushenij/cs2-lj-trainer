using System;
using System.Collections.Generic;
using Raylib_cs;

namespace LJTrainer.Core
{
    public static unsafe class AudioEngine
    {
        public static readonly string[] SoundPresetNames =
        {
            "1. Mechanical Click",
            "2. Soft Woodblock",
            "3. Digital Beep",
            "4. Crisp Hi-Hat",
            "5. Glass UI Tap",
            "6. Deep Kick Punch",
            "7. Cyber Laser Blip",
            "8. Metallic Triangle",
            "9. Cowbell Rimshot",
            "10. Acoustic Snap",
            "11. Low Synth Thud",
            "12. High Sine Ping",
            "13. Dual Tone (L/R)",
            "14. Bubble Pop",
            "15. Subtle Tick",
            "16. Sine Sub Pulse"
        };

        private static readonly List<Sound> _metronomeSounds = new();
        private static Sound _sndTakeoff;
        private static Sound _sndLanding;
        private static Sound _sndOverlapBuzz;
        private static Sound _sndDualToneHigh;
        private static Sound _sndDualToneLow;
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                Raylib.InitAudioDevice();

                // 16 Metronome Sound Presets
                // 0: Mechanical Click (Cherry MX)
                _metronomeSounds.Add(GenerateTone(2400, 1200, 0.016f, Waveform.Square, 0.45f));
                // 1: Soft Woodblock
                _metronomeSounds.Add(GenerateTone(880, 520, 0.035f, Waveform.Sine, 0.55f));
                // 2: Digital Beep (880 Hz)
                _metronomeSounds.Add(GenerateTone(880, 880, 0.025f, Waveform.Sine, 0.5f));
                // 3: Crisp Hi-Hat
                _metronomeSounds.Add(GenerateNoiseBurst(0.025f, 0.4f));
                // 4: Glass UI Tap
                _metronomeSounds.Add(GenerateTone(1400, 2200, 0.030f, Waveform.Sine, 0.45f));
                // 5: Deep Kick Punch
                _metronomeSounds.Add(GenerateTone(160, 45, 0.045f, Waveform.Sine, 0.65f));
                // 6: Cyber Laser Blip
                _metronomeSounds.Add(GenerateTone(1800, 380, 0.028f, Waveform.Sawtooth, 0.4f));
                // 7: Metallic Triangle
                _metronomeSounds.Add(GenerateChord(new[] { 2400f, 3600f, 4800f }, 0.05f, 0.35f));
                // 8: Cowbell Rimshot
                _metronomeSounds.Add(GenerateChord(new[] { 587f, 845f }, 0.04f, 0.45f));
                // 9: Acoustic Snap
                _metronomeSounds.Add(GenerateTone(1200, 200, 0.018f, Waveform.Square, 0.4f));
                // 10: Low Synth Thud
                _metronomeSounds.Add(GenerateTone(220, 110, 0.032f, Waveform.Square, 0.45f));
                // 11: High Sine Ping
                _metronomeSounds.Add(GenerateTone(1760, 1760, 0.030f, Waveform.Sine, 0.45f));
                // 12: Dual Tone
                _sndDualToneHigh = GenerateTone(1050, 1050, 0.025f, Waveform.Sine, 0.5f);
                _sndDualToneLow = GenerateTone(700, 700, 0.025f, Waveform.Sine, 0.5f);
                _metronomeSounds.Add(_sndDualToneHigh);
                // 13: Bubble Pop
                _metronomeSounds.Add(GenerateTone(420, 1100, 0.022f, Waveform.Sine, 0.5f));
                // 14: Subtle Tick
                _metronomeSounds.Add(GenerateTone(1100, 1100, 0.012f, Waveform.Sine, 0.35f));
                // 15: Sine Sub Pulse
                _metronomeSounds.Add(GenerateTone(130, 90, 0.040f, Waveform.Sine, 0.55f));

                _sndTakeoff = GenerateTone(320, 180, 0.12f, Waveform.Sine, 0.4f);
                _sndLanding = GenerateTone(120, 60, 0.15f, Waveform.Sine, 0.6f);
                _sndOverlapBuzz = GenerateTone(110, 100, 0.08f, Waveform.Sawtooth, 0.35f);
                
                // 3-Tier Biofeedback:
                // 1. Clean / Perfect: Crisp bright harmonic chime (1600Hz -> 1850Hz)
                _sndBioClean = GenerateTone(1600, 1850, 0.038f, Waveform.Sine, 0.42f);
                // 2. Minor error / Slight desync: Same harmonic chime lowered by 3 semitones (1250Hz -> 1450Hz)
                _sndBioMinor = GenerateTone(1250, 1450, 0.038f, Waveform.Sine, 0.38f);
                // 3. Hard error / Severe Overlap: Low dull thud
                _sndBioHardError = GenerateTone(180, 80, 0.055f, Waveform.Sine, 0.35f);

                // Epic PB Fanfare: Multi-layered sub-bass + soaring crystal arpeggio + majestic golden crown chord
                _sndEpicPBFanfare = GenerateEpicPbFanfare();
                _pbSoundsReady = true;

                _initialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioEngine] Init error: {ex.Message}");
            }
        }

        private static Sound _sndBioClean;
        private static Sound _sndBioMinor;
        private static Sound _sndBioHardError;
        private static Sound _sndEpicPBFanfare;
        private static bool _pbSoundsReady = false;

        public enum BiofeedbackTier { Clean, MinorDesync, HardError }

        public static void PlayMetronomeTick(int presetIndex = 0, bool isRightSide = false)
        {
            if (!_initialized || !AppConfig.Instance.SoundEnabled || _metronomeSounds.Count == 0) return;

            int idx = Math.Clamp(presetIndex, 0, _metronomeSounds.Count - 1);
            
            // Dual Tone special handling: high pitch for Left, low pitch for Right
            if (idx == 12)
            {
                Sound s = isRightSide ? _sndDualToneLow : _sndDualToneHigh;
                Play(s);
                return;
            }

            Play(_metronomeSounds[idx]);
        }

        public static void PlayBiofeedback(BiofeedbackTier tier)
        {
            if (!_initialized || !AppConfig.Instance.SoundEnabled || !AppConfig.Instance.AudioBiofeedback) return;
            Sound s = tier switch
            {
                BiofeedbackTier.Clean => _sndBioClean,
                BiofeedbackTier.MinorDesync => _sndBioMinor,
                _ => _sndBioHardError
            };
            Play(s);
        }

        public static void PlayPBSound()
        {
            if (!_initialized || !_pbSoundsReady || !AppConfig.Instance.SoundEnabled) return;
            Play(_sndEpicPBFanfare);
        }

        private static Sound GenerateEpicPbFanfare()
        {
            int sampleRate = 44100;
            float duration = 1.35f;
            int sampleCount = (int)(sampleRate * duration);

            byte[] wavBytes = CreateWavFile(sampleRate, 1, 16, sampleCount, (i, t) =>
            {
                double sample = 0;

                // 1. Sub-bass punch at t=0 (78Hz -> 38Hz)
                if (t < 0.35f)
                {
                    float bassEnv = MathF.Pow(1.0f - (t / 0.35f), 1.8f);
                    double bassPhase = (2.0 * Math.PI * (78.0 - 40.0 * (t / 0.35f))) * (i / (double)sampleRate);
                    sample += Math.Sin(bassPhase) * bassEnv * 0.48;
                }

                // 2. Sparkling Fast Arpeggio (C5 -> E5 -> G5 -> B5 -> C6 -> E6)
                float[] arpFreqs = { 523.25f, 659.25f, 783.99f, 987.77f, 1046.50f, 1318.51f };
                float noteDuration = 0.058f;
                for (int n = 0; n < arpFreqs.Length; n++)
                {
                    float noteStart = n * noteDuration;
                    if (t >= noteStart && t < noteStart + 0.32f)
                    {
                        float localT = (t - noteStart) / 0.32f;
                        float noteEnv = MathF.Pow(1.0f - localT, 2.2f);
                        if (t - noteStart < 0.010f) noteEnv *= (t - noteStart) / 0.010f;

                        float freq = arpFreqs[n];
                        double p1 = (2.0 * Math.PI * freq) * (i / (double)sampleRate);
                        double p2 = (2.0 * Math.PI * (freq * 2.003f)) * (i / (double)sampleRate);
                        sample += (Math.Sin(p1) * 0.65 + Math.Sin(p2) * 0.35) * noteEnv * 0.34;
                    }
                }

                // 3. Sustained Golden Crown Chord (C5 + G5 + C6 + E6) starting at t=0.35s with warm shimmering tail
                float chordStart = 0.34f;
                if (t >= chordStart)
                {
                    float localT = (t - chordStart) / (duration - chordStart);
                    float chordEnv = MathF.Pow(1.0f - localT, 1.4f);
                    if (t - chordStart < 0.030f) chordEnv *= (t - chordStart) / 0.030f;

                    float[] chordFreqs = { 523.25f, 783.99f, 1046.50f, 1318.51f, 2093.00f };
                    float[] chordGains = { 0.26f, 0.22f, 0.28f, 0.20f, 0.09f };

                    for (int c = 0; c < chordFreqs.Length; c++)
                    {
                        double p = (2.0 * Math.PI * chordFreqs[c]) * (i / (double)sampleRate);
                        double pDetune = (2.0 * Math.PI * (chordFreqs[c] * 1.0025f)) * (i / (double)sampleRate);
                        sample += (Math.Sin(p) + Math.Sin(pDetune) * 0.5) * chordEnv * chordGains[c];
                    }
                }

                // Master Soft Clipper
                sample = Math.Clamp(sample * 0.85, -0.95, 0.95);
                return (short)(sample * short.MaxValue);
            });

            return LoadSoundFromBytes(wavBytes);
        }

        public static void PlayOverlapBuzz() => Play(_sndOverlapBuzz);
        public static void PlayTakeoff() => Play(_sndTakeoff);
        public static void PlayLanding() => Play(_sndLanding);

        private static void Play(Sound sound)
        {
            if (!_initialized || !AppConfig.Instance.SoundEnabled) return;
            Raylib.SetSoundVolume(sound, AppConfig.Instance.MasterVolume);
            Raylib.PlaySound(sound);
        }

        private enum Waveform { Sine, Square, Sawtooth }

        private static Sound GenerateTone(float startFreq, float endFreq, float duration, Waveform form, float gain)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            byte[] wavBytes = CreateWavFile(sampleRate, 1, 16, sampleCount, (i, t) =>
            {
                float currentFreq = startFreq + (endFreq - startFreq) * t;
                double phase = (2.0 * Math.PI * currentFreq) * (i / (double)sampleRate);

                float envelope = MathF.Pow(1.0f - t, 2.0f);
                if (t < 0.05f) envelope = t / 0.05f;

                double sample = form switch
                {
                    Waveform.Sine => Math.Sin(phase),
                    Waveform.Square => Math.Sin(phase) >= 0 ? 0.7 : -0.7,
                    Waveform.Sawtooth => 2.0 * (phase / (2.0 * Math.PI) - Math.Floor(0.5 + phase / (2.0 * Math.PI))),
                    _ => Math.Sin(phase)
                };

                return (short)(sample * envelope * gain * short.MaxValue);
            });

            return LoadSoundFromBytes(wavBytes);
        }

        private static Sound GenerateNoiseBurst(float duration, float gain)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            var rand = new Random(42);

            byte[] wavBytes = CreateWavFile(sampleRate, 1, 16, sampleCount, (i, t) =>
            {
                float envelope = MathF.Pow(1.0f - t, 3.0f);
                double sample = (rand.NextDouble() * 2.0 - 1.0);
                return (short)(sample * envelope * gain * short.MaxValue);
            });

            return LoadSoundFromBytes(wavBytes);
        }

        private static Sound GenerateChord(float[] freqs, float duration, float gain)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            byte[] wavBytes = CreateWavFile(sampleRate, 1, 16, sampleCount, (i, t) =>
            {
                double sum = 0;
                foreach (var f in freqs)
                {
                    double phase = (2.0 * Math.PI * f) * (i / (double)sampleRate);
                    sum += Math.Sin(phase);
                }
                sum /= freqs.Length;
                float envelope = MathF.Pow(1.0f - t, 1.8f);
                return (short)(sum * envelope * gain * short.MaxValue);
            });

            return LoadSoundFromBytes(wavBytes);
        }

        private static byte[] CreateWavFile(int sampleRate, short channels, short bitsPerSample, int sampleCount, Func<int, float, short> generator)
        {
            int subChunk2Size = sampleCount * channels * (bitsPerSample / 8);
            int chunkSize = 36 + subChunk2Size;
            int byteRate = sampleRate * channels * (bitsPerSample / 8);
            short blockAlign = (short)(channels * (bitsPerSample / 8));

            byte[] file = new byte[44 + subChunk2Size];
            using var ms = new System.IO.MemoryStream(file);
            using var bw = new System.IO.BinaryWriter(ms);

            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(chunkSize);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bitsPerSample);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(subChunk2Size);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                short sample = generator(i, t);
                bw.Write(sample);
            }

            return file;
        }

        private static Sound LoadSoundFromBytes(byte[] wavBytes)
        {
            fixed (byte* ptr = wavBytes)
            {
                sbyte* fileType = (sbyte*)System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(".wav");
                Wave wave = Raylib.LoadWaveFromMemory(fileType, ptr, wavBytes.Length);
                Sound sound = Raylib.LoadSoundFromWave(wave);
                Raylib.UnloadWave(wave);
                System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)fileType);
                return sound;
            }
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            foreach (var s in _metronomeSounds)
            {
                Raylib.UnloadSound(s);
            }
            _metronomeSounds.Clear();
            Raylib.CloseAudioDevice();
            _initialized = false;
        }
    }
}
