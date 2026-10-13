using System;
using Presentation;

namespace AudioPresentation
{
    public enum AudioRecipe { Engine, Tone, HarmonicTone, Noise }

    /// <summary>Authored immutable recipe and mix bounds. All cues use the accepted central 2D policy.</summary>
    public readonly struct AudioCueDefinition
    {
        public AudioCueDefinition(AudioRecipe recipe, float duration, float start, float end, float amplitude,
            float gain, float pitchVariation, float gainVariation, float cooldown, int priority, uint seed)
        {
            Recipe = recipe;
            Duration = duration;
            StartFrequency = start;
            EndFrequency = end;
            Amplitude = amplitude;
            Gain = gain;
            PitchVariation = pitchVariation;
            GainVariation = gainVariation;
            Cooldown = cooldown;
            Priority = priority;
            Seed = seed;
        }

        public AudioRecipe Recipe { get; }
        public float Duration { get; }
        public float StartFrequency { get; }
        public float EndFrequency { get; }
        public float Amplitude { get; }
        public float Gain { get; }
        public float PitchVariation { get; }
        public float GainVariation { get; }
        public float Cooldown { get; }
        public int Priority { get; }
        public uint Seed { get; }
    }

    public static class RcAudioCueData
    {
        // Priority: 0 club decoration, 1 tire/UI, 2 combat/pickup/light contact,
        // 3 heavy contact/recovery, 4 countdown/go/finish. Never steal equal or higher priority.
        // Records follow the stable AudioCueId order; comments make the authored association reviewable.
        private static readonly AudioCueDefinition[] Definitions =
        {
            D(AudioRecipe.Engine, .5f, 82, 82, .12f, 1, 0, 0, 0, 1, 1), // Engine
            D(AudioRecipe.Tone, .13f, 620, 930, .18f, 1, 0, 0, .08f, 4, 2), // Countdown
            D(AudioRecipe.HarmonicTone, .24f, 760, 1260, .22f, 1, 0, 0, .1f, 4, 3), // Go
            D(AudioRecipe.HarmonicTone, .5f, 520, 1040, .2f, 1, 0, 0, .2f, 4, 4), // Finish
            D(AudioRecipe.Tone, .075f, 880, 1320, .12f, .8f, .02f, .03f, 0, 1, 5), // UiConfirm
            D(AudioRecipe.HarmonicTone, .2f, 540, 1480, .16f, .9f, .03f, .04f, .1f, 2, 6), // Pickup
            D(AudioRecipe.Noise, .16f, 150, 150, .18f, .8f, .04f, .05f, .08f, 2, 7), // WeaponFire (legacy)
            D(AudioRecipe.Noise, .12f, 85, 85, .25f, .75f, .05f, .06f, .14f, 2, 8), // Impact (light)
            D(AudioRecipe.Noise, .1f, 190, 190, .12f, .3f, .06f, .08f, .32f, 1, 11), // TireMat
            D(AudioRecipe.Noise, .11f, 125, 125, .14f, .3f, .06f, .08f, .32f, 1, 12), // TireWood
            D(AudioRecipe.Noise, .12f, 65, 65, .1f, .25f, .06f, .08f, .32f, 1, 13), // TireFoam
            D(AudioRecipe.Noise, .1f, 240, 240, .11f, .28f, .06f, .08f, .32f, 1, 14), // TireCardboard
            D(AudioRecipe.Tone, .08f, 780, 420, .1f, .25f, .05f, .08f, .32f, 1, 15), // TireMetal
            D(AudioRecipe.Noise, .11f, 310, 310, .12f, .3f, .06f, .08f, .32f, 1, 16), // TireRubber
            D(AudioRecipe.Tone, .12f, 580, 960, .13f, .4f, .05f, .06f, .1f, 1, 17), // SkidStart
            D(AudioRecipe.Noise, .14f, 850, 850, .12f, .35f, .06f, .08f, .22f, 1, 18), // SkidScrub
            D(AudioRecipe.Tone, .1f, 720, 340, .1f, .3f, .05f, .06f, .1f, 1, 19), // SkidEnd
            D(AudioRecipe.Noise, .22f, 48, 48, .28f, 1, .04f, .04f, .14f, 3, 20), // HeavyImpact
            D(AudioRecipe.Tone, .13f, 1450, 380, .2f, .85f, .04f, .04f, .08f, 2, 21), // BoltLaunch
            D(AudioRecipe.Noise, .16f, 420, 420, .22f, .9f, .04f, .04f, .1f, 2, 22), // BoltHit
            D(AudioRecipe.HarmonicTone, .18f, 260, 90, .17f, .75f, .04f, .04f, .1f, 2, 23), // OilDrop
            D(AudioRecipe.Noise, .24f, 105, 105, .16f, .8f, .05f, .05f, .12f, 2, 24), // OilHit
            D(AudioRecipe.HarmonicTone, .32f, 340, 1020, .18f, .95f, .02f, .03f, .3f, 3, 25), // Recovery
            D(AudioRecipe.Noise, .32f, 135, 135, .08f, .18f, .08f, .1f, 8f, 0, 26) // ClubAmbience
        };

        private static AudioCueDefinition D(AudioRecipe recipe, float duration, float start, float end,
            float amplitude, float gain, float pitch, float gainVariation, float cooldown, int priority, uint seed) =>
            new(recipe, duration, start, end, amplitude, gain, pitch, gainVariation, cooldown, priority,
                0x51F15EEDu + seed * 7919u);

        public static AudioCueDefinition Get(AudioCueId cue)
        {
            int index = (int)cue;
            if (index < 0 || index >= Definitions.Length) throw new ArgumentOutOfRangeException(nameof(cue));
            return Definitions[index];
        }

        /// <summary>Variation depends only on authored seed, cue recipient, and accepted sequence; no Unity RNG.</summary>
        public static void Variation(AudioCueId cue, int recipient, uint sequence, out float pitch, out float gain)
        {
            AudioCueDefinition data = Get(cue);
            uint value = unchecked(data.Seed ^ ((uint)(recipient + 2) * 2654435761u) ^ sequence * 2246822519u);
            value = unchecked(value * 1664525u + 1013904223u);
            pitch = 1f + ((value & 65535u) / 32767.5f - 1f) * data.PitchVariation;
            value = unchecked(value * 1664525u + 1013904223u);
            gain = data.Gain * (1f + ((value & 65535u) / 32767.5f - 1f) * data.GainVariation);
        }

        public static bool TrySurfaceCue(TrackSurfaceKind surface, out AudioCueId cue)
        {
            cue = surface switch
            {
                TrackSurfaceKind.CuttingMat => AudioCueId.TireMat,
                TrackSurfaceKind.WorkbenchWood => AudioCueId.TireWood,
                TrackSurfaceKind.Foam => AudioCueId.TireFoam,
                TrackSurfaceKind.Cardboard => AudioCueId.TireCardboard,
                TrackSurfaceKind.Metal => AudioCueId.TireMetal,
                TrackSurfaceKind.Rubber => AudioCueId.TireRubber,
                _ => AudioCueId.Count
            };
            return cue != AudioCueId.Count;
        }
    }

    /// <summary>Shared readable mix rules for 1–4 humans; AI adds no engine/tire/listener ownership.</summary>
    public static class RcAudioMixPolicy
    {
        public static float PlayerScale(int count) => count switch { 4 => .5f, 3 => .58f, 2 => .7f, _ => 1f };
        public static float DuckScale(int priority) => priority >= 4 ? .35f : priority >= 3 ? .55f : 1f;

        public static int SelectVoice(float now, int requestedPriority, float[] busyUntil, float[] startedAt,
            int[] priorities, out bool stolen)
        {
            stolen = false;
            for (int i = 0; i < busyUntil.Length; i++) if (now >= busyUntil[i]) return i;
            int selected = -1;
            float oldest = float.PositiveInfinity;
            // Ordinary/decorative cues drop under saturation. Important cues steal the oldest lower tier.
            if (requestedPriority < 3) return -1;
            for (int i = 0; i < busyUntil.Length; i++)
                if (priorities[i] < requestedPriority && startedAt[i] < oldest)
                {
                    selected = i;
                    oldest = startedAt[i];
                }
            stolen = selected >= 0;
            return selected;
        }
    }
}
