using AudioPresentation;
using NUnit.Framework;
using Presentation;

namespace Tests.EditMode
{
    public sealed class Stage9BAudioPolicyTests
    {
        [Test]
        public void EveryCueHasBoundedRecipeAndRepeatableRecipientVariation()
        {
            for (int i = 0; i < (int)AudioCueId.Count; i++)
            {
                AudioCueId cue = (AudioCueId)i;
                AudioCueDefinition data = RcAudioCueData.Get(cue);
                Assert.That(data.Duration, Is.InRange(.01f, .5f));
                Assert.That(data.Amplitude, Is.InRange(0f, .3f));
                Assert.That(data.Gain, Is.InRange(0f, 1f));
                Assert.That(data.Priority, Is.InRange(0, 4));
                for (uint sequence = 0; sequence < 32; sequence++)
                {
                    RcAudioCueData.Variation(cue, 2, sequence, out float pitch, out float gain);
                    RcAudioCueData.Variation(cue, 2, sequence, out float repeatedPitch, out float repeatedGain);
                    Assert.That(repeatedPitch, Is.EqualTo(pitch));
                    Assert.That(repeatedGain, Is.EqualTo(gain));
                    Assert.That(pitch, Is.InRange(1f - data.PitchVariation, 1f + data.PitchVariation));
                    Assert.That(gain, Is.InRange(data.Gain * (1f - data.GainVariation),
                        data.Gain * (1f + data.GainVariation)));
                }
            }
            RcAudioCueData.Variation(AudioCueId.BoltLaunch, 0, 0, out float first, out _);
            RcAudioCueData.Variation(AudioCueId.BoltLaunch, 1, 0, out float second, out _);
            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void SurfaceCuesAreExplicitAndUnknownOrVehicleContactNeverInventTireMaterial()
        {
            for (int i = (int)TrackSurfaceKind.CuttingMat; i <= (int)TrackSurfaceKind.Rubber; i++)
            {
                Assert.That(RcAudioCueData.TrySurfaceCue((TrackSurfaceKind)i, out AudioCueId cue), Is.True);
                Assert.That(RcAudioCueData.Get(cue).Priority, Is.EqualTo(1));
            }
            Assert.That(RcAudioCueData.TrySurfaceCue(TrackSurfaceKind.Unclassified, out _), Is.False);
            Assert.That(RcAudioCueData.TrySurfaceCue(TrackSurfaceKind.VehicleContact, out _), Is.False);
            Assert.That(RcAudioCueData.TrySurfaceCue((TrackSurfaceKind)999, out _), Is.False);
        }

        [Test]
        public void SaturationDropsOrdinaryCuesAndStealsOnlyOldestLowerPriorityVoice()
        {
            float[] busy = { 10, 10, 10, 10, 10, 10 };
            float[] started = { 0, 1, 2, 3, 4, 5 };
            int[] priorities = { 4, 3, 1, 0, 2, 4 };
            Assert.That(RcAudioMixPolicy.SelectVoice(6, 2, busy, started, priorities, out bool stolen),
                Is.EqualTo(-1));
            Assert.That(stolen, Is.False);
            Assert.That(RcAudioMixPolicy.SelectVoice(6, 3, busy, started, priorities, out stolen), Is.EqualTo(2));
            Assert.That(stolen, Is.True);
            Assert.That(RcAudioMixPolicy.SelectVoice(6, 4, busy, started, priorities, out stolen), Is.EqualTo(1));
            Assert.That(stolen, Is.True);
            for (int i = 0; i < priorities.Length; i++) priorities[i] = 4;
            Assert.That(RcAudioMixPolicy.SelectVoice(6, 4, busy, started, priorities, out stolen), Is.EqualTo(-1));
            Assert.That(stolen, Is.False);
            busy[5] = 6;
            Assert.That(RcAudioMixPolicy.SelectVoice(6, 0, busy, started, priorities, out stolen), Is.EqualTo(5));
            Assert.That(stolen, Is.False);
        }

        [TestCase(1, 1f)]
        [TestCase(2, .7f)]
        [TestCase(3, .58f)]
        [TestCase(4, .5f)]
        public void MixScaleAndImportantCueDuckingAreDeclaredForEachPlayerCount(int players, float scale)
        {
            Assert.That(RcAudioMixPolicy.PlayerScale(players), Is.EqualTo(scale));
            Assert.That(RcAudioMixPolicy.DuckScale(2), Is.EqualTo(1f));
            Assert.That(RcAudioMixPolicy.DuckScale(3), Is.EqualTo(.55f));
            Assert.That(RcAudioMixPolicy.DuckScale(4), Is.EqualTo(.35f));
        }
    }
}
