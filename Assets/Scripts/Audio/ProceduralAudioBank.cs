using System;
using UnityEngine;

namespace AudioPresentation
{
    /// <summary>Stable Stage 8 identifiers followed by the bounded Stage 9B sound set.</summary>
    public enum AudioCueId
    {
        Engine,
        Countdown,
        Go,
        Finish,
        UiConfirm,
        Pickup,
        WeaponFire,
        Impact,
        TireMat,
        TireWood,
        TireFoam,
        TireCardboard,
        TireMetal,
        TireRubber,
        SkidStart,
        SkidScrub,
        SkidEnd,
        HeavyImpact,
        BoltLaunch,
        BoltHit,
        OilDrop,
        OilHit,
        Recovery,
        ClubAmbience,
        Count
    }

    /// <summary>Creates and owns one deterministic runtime clip for every prototype cue.</summary>
    public sealed class ProceduralAudioBank : IDisposable
    {
        public const int SampleRate = 22050;

        private readonly AudioClip[] clips = new AudioClip[(int)AudioCueId.Count];
        private bool disposed;

        public ProceduralAudioBank()
        {
            try
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    AudioCueDefinition cue = RcAudioCueData.Get((AudioCueId)i);
                    string label = "RC " + (AudioCueId)i;
                    clips[i] = cue.Recipe switch
                    {
                        AudioRecipe.Engine => CreateEngineLoop(),
                        AudioRecipe.Noise => CreateNoiseBurst(label, cue.Duration, cue.StartFrequency,
                            cue.Amplitude, cue.Seed),
                        _ => CreateTone(label, cue.Duration, cue.StartFrequency, cue.EndFrequency,
                            cue.Amplitude, cue.Recipe == AudioRecipe.HarmonicTone)
                    };
                }
            }
            catch
            {
                ReleaseClips();
                disposed = true;
                throw;
            }
        }

        public int ClipCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] != null) count++;
                return count;
            }
        }

        public AudioClip Get(AudioCueId cue)
        {
            int index = (int)cue;
            if (disposed || index < (int)AudioCueId.Engine || index >= (int)AudioCueId.Count)
                return null;
            return clips[index];
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ReleaseClips();
        }

        private void ReleaseClips()
        {
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[i];
                clips[i] = null;
                if (clip == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(clip);
                else UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        private static AudioClip CreateEngineLoop()
        {
            const float duration = .5f;
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / SampleRate;
                float phase = time * 82f * Mathf.PI * 2f;
                // A quiet harmonic pulse reads as a small electric motor without importing an asset.
                samples[i] = (Mathf.Sin(phase) * .12f + Mathf.Sin(phase * 2f) * .045f +
                              Mathf.Sin(phase * 4.01f) * .018f) * SeamlessLoopEnvelope(i, sampleCount);
            }
            return CreateClip("RC engine loop", samples);
        }

        private static AudioClip CreateTone(string name, float duration, float startFrequency,
            float endFrequency, float amplitude, bool addHarmony = false)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float normalized = (float)i / Mathf.Max(1, sampleCount - 1);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, normalized);
                phase += frequency * Mathf.PI * 2f / SampleRate;
                float signal = Mathf.Sin(phase);
                if (addHarmony) signal += Mathf.Sin(phase * 1.5f) * .28f;
                samples[i] = signal * amplitude * OneShotEnvelope(normalized);
            }
            return CreateClip(name, samples);
        }

        private static AudioClip CreateNoiseBurst(string name, float duration, float bodyFrequency, float amplitude,
            uint seed)
        {
            int sampleCount = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            uint noiseState = seed;
            for (int i = 0; i < sampleCount; i++)
            {
                float normalized = (float)i / Mathf.Max(1, sampleCount - 1);
                noiseState = noiseState * 1664525u + 1013904223u;
                float noise = ((noiseState >> 8) / 8388607.5f) - 1f;
                float body = Mathf.Sin((float)i / SampleRate * bodyFrequency * Mathf.PI * 2f);
                samples[i] = (noise * .7f + body * .3f) * amplitude * OneShotEnvelope(normalized);
            }
            return CreateClip(name, samples);
        }

        private static AudioClip CreateClip(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            if (clip == null || !clip.SetData(samples, 0))
            {
                if (clip != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(clip);
                    else UnityEngine.Object.DestroyImmediate(clip);
                }
                throw new InvalidOperationException($"Could not construct procedural audio clip '{name}'.");
            }
            return clip;
        }

        private static float OneShotEnvelope(float normalized)
        {
            float attack = Mathf.Clamp01(normalized / .08f);
            float release = Mathf.Clamp01((1f - normalized) / .28f);
            return attack * release;
        }

        private static float SeamlessLoopEnvelope(int index, int sampleCount)
        {
            const int edgeSamples = 96;
            if (index < edgeSamples) return (float)index / edgeSamples;
            int remaining = sampleCount - index - 1;
            return remaining < edgeSamples ? (float)remaining / edgeSamples : 1f;
        }
    }
}
