using System;
using System.Collections.Generic;
using NUnit.Framework;
using Presentation;
using Vehicle;

namespace Tests.EditMode
{
    public sealed class DemoFlowPreferencesTests
    {
        private sealed class MemoryStore : ILocalPreferenceStore
        {
            private readonly Dictionary<string, string> values = new();

            public bool FailReads { get; set; }
            public bool FailWrites { get; set; }
            public int WriteAttempts { get; private set; }

            public bool TryRead(string key, out string value)
            {
                if (FailReads) throw new InvalidOperationException("read failed");
                return values.TryGetValue(key, out value);
            }

            public void Write(string key, string value)
            {
                WriteAttempts++;
                if (FailWrites) throw new InvalidOperationException("write failed");
                values[key] = value;
            }

            public void Seed(string value) => values[DemoFlowPreferences.StorageKey] = value;

            public string ReadRaw() => values[DemoFlowPreferences.StorageKey];
        }

        [Test]
        public void MissingPreferencesUseSafeVersionOneDefaults()
        {
            DemoFlowPreferences preferences = new(new MemoryStore());

            preferences.Load();

            Assert.That(preferences.VisualStyle, Is.EqualTo(VisualStyleMode.Clean3D));
            Assert.That(preferences.RecoveryMessage, Is.Empty);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(preferences.GetHandlingModel(i), Is.EqualTo(HandlingModelId.A));
                Assert.That(preferences.GetVehicleFamily(i), Is.EqualTo(VehicleFamilyId.Touring));
            }
        }

        [Test]
        public void SelectionsRoundTripThroughVersionedPayload()
        {
            MemoryStore store = new();
            DemoFlowPreferences writer = new(store);
            Assert.That(writer.SetPlayerSelection(0, HandlingModelId.D, VehicleFamilyId.Drift), Is.True);
            Assert.That(writer.SetPlayerSelection(3, HandlingModelId.B, VehicleFamilyId.RcTruck), Is.True);
            Assert.That(writer.SetVisualStyle(VisualStyleMode.InkLite), Is.True);

            DemoFlowPreferences reader = new(store);
            reader.Load();

            Assert.That(store.ReadRaw(), Does.Contain($"\"version\":{DemoFlowPreferences.CurrentVersion}"));
            Assert.That(reader.VisualStyle, Is.EqualTo(VisualStyleMode.InkLite));
            Assert.That(reader.GetHandlingModel(0), Is.EqualTo(HandlingModelId.D));
            Assert.That(reader.GetVehicleFamily(0), Is.EqualTo(VehicleFamilyId.Drift));
            Assert.That(reader.GetHandlingModel(3), Is.EqualTo(HandlingModelId.B));
            Assert.That(reader.GetVehicleFamily(3), Is.EqualTo(VehicleFamilyId.RcTruck));
            Assert.That(reader.RecoveryMessage, Is.Empty);
        }

        [TestCase("{ definitely-not-json")]
        [TestCase("{\"version\":99,\"visualStyle\":2}")]
        public void CorruptOrUnknownPayloadKeepsRawDataAndFallsBackSafely(string raw)
        {
            MemoryStore store = new();
            store.Seed(raw);
            DemoFlowPreferences preferences = new(store);

            preferences.Load();

            Assert.That(preferences.VisualStyle, Is.EqualTo(VisualStyleMode.Clean3D));
            Assert.That(preferences.GetHandlingModel(0), Is.EqualTo(HandlingModelId.A));
            Assert.That(preferences.GetVehicleFamily(0), Is.EqualTo(VehicleFamilyId.Touring));
            Assert.That(preferences.RecoveryMessage, Is.Not.Empty);
            Assert.That(store.ReadRaw(), Is.EqualTo(raw), "Recovery must not silently overwrite unknown data.");
        }

        [Test]
        public void PartialCurrentPayloadKeepsValidFieldsAndRepairsMissingSlots()
        {
            MemoryStore store = new();
            store.Seed("{\"version\":1,\"visualStyle\":2,\"handlingModels\":[2],\"vehicleFamilies\":[3]}");
            DemoFlowPreferences preferences = new(store);

            preferences.Load();

            Assert.That(preferences.VisualStyle, Is.EqualTo(VisualStyleMode.FullInk));
            Assert.That(preferences.GetHandlingModel(0), Is.EqualTo(HandlingModelId.C));
            Assert.That(preferences.GetVehicleFamily(0), Is.EqualTo(VehicleFamilyId.Drift));
            Assert.That(preferences.GetHandlingModel(1), Is.EqualTo(HandlingModelId.A));
            Assert.That(preferences.GetVehicleFamily(1), Is.EqualTo(VehicleFamilyId.Touring));
            Assert.That(preferences.RecoveryMessage, Does.Contain("SAFE DEFAULTS"));
        }

        [Test]
        public void StorageFailuresDoNotPreventSessionSelections()
        {
            MemoryStore store = new() { FailReads = true };
            DemoFlowPreferences preferences = new(store);
            Assert.DoesNotThrow(preferences.Load);
            Assert.That(preferences.RecoveryMessage, Does.Contain("COULD NOT BE READ"));

            store.FailReads = false;
            store.FailWrites = true;
            Assert.That(preferences.SetPlayerSelection(2, HandlingModelId.B, VehicleFamilyId.Buggy), Is.False);
            Assert.That(preferences.GetHandlingModel(2), Is.EqualTo(HandlingModelId.B));
            Assert.That(preferences.GetVehicleFamily(2), Is.EqualTo(VehicleFamilyId.Buggy));
            Assert.That(preferences.RecoveryMessage, Does.Contain("COULD NOT BE SAVED"));
        }

        [Test]
        public void FailedSelectionSaveRetriesWhenTheSameValuesAreSubmitted()
        {
            MemoryStore store = new() { FailWrites = true };
            DemoFlowPreferences preferences = new(store);
            Assert.That(preferences.SetPlayerSelection(1, HandlingModelId.C, VehicleFamilyId.RcTruck), Is.False);
            Assert.That(store.WriteAttempts, Is.EqualTo(1));

            store.FailWrites = false;
            Assert.That(preferences.SetPlayerSelection(1, HandlingModelId.C, VehicleFamilyId.RcTruck), Is.True);
            Assert.That(store.WriteAttempts, Is.EqualTo(2));
            Assert.That(preferences.RecoveryMessage, Is.Empty);

            DemoFlowPreferences reloaded = new(store);
            reloaded.Load();
            Assert.That(reloaded.GetHandlingModel(1), Is.EqualTo(HandlingModelId.C));
            Assert.That(reloaded.GetVehicleFamily(1), Is.EqualTo(VehicleFamilyId.RcTruck));
        }

        [Test]
        public void FailedStyleSaveRetriesWhenTheSameValueIsSubmitted()
        {
            MemoryStore store = new() { FailWrites = true };
            DemoFlowPreferences preferences = new(store);
            Assert.That(preferences.SetVisualStyle(VisualStyleMode.FullInk), Is.False);
            Assert.That(store.WriteAttempts, Is.EqualTo(1));

            store.FailWrites = false;
            Assert.That(preferences.SetVisualStyle(VisualStyleMode.FullInk), Is.True);
            Assert.That(store.WriteAttempts, Is.EqualTo(2));
            Assert.That(preferences.RecoveryMessage, Is.Empty);

            DemoFlowPreferences reloaded = new(store);
            reloaded.Load();
            Assert.That(reloaded.VisualStyle, Is.EqualTo(VisualStyleMode.FullInk));
        }

        [Test]
        public void VersionOnePayloadMigratesWithFullMotionDefaultsAndVersionTwoRoundTrips()
        {
            MemoryStore store = new();
            store.Seed("{\"version\":1,\"visualStyle\":0,\"handlingModels\":[0,0,0,0],\"vehicleFamilies\":[0,0,0,0]}");
            DemoFlowPreferences preferences = new(store);
            preferences.Load();

            Assert.That(preferences.CameraMotion, Is.EqualTo(PresentationResponseLevel.Full));
            Assert.That(preferences.VehicleMotion, Is.EqualTo(PresentationResponseLevel.Full));
            Assert.That(preferences.SetPresentationResponseLevels(PresentationResponseLevel.Off,
                PresentationResponseLevel.Reduced), Is.True);

            DemoFlowPreferences reloaded = new(store);
            reloaded.Load();
            Assert.That(reloaded.CameraMotion, Is.EqualTo(PresentationResponseLevel.Off));
            Assert.That(reloaded.VehicleMotion, Is.EqualTo(PresentationResponseLevel.Reduced));
        }

        [Test]
        public void VersionTwoPayloadMigratesWithFullVfxDensityAndVersionThreeRoundTrips()
        {
            MemoryStore store = new();
            store.Seed("{\"version\":2,\"visualStyle\":0,\"handlingModels\":[0,0,0,0]," +
                       "\"vehicleFamilies\":[0,0,0,0],\"cameraMotion\":1,\"vehicleMotion\":2," +
                       "\"hasPresentationResponseSettings\":true}");
            DemoFlowPreferences preferences = new(store);
            preferences.Load();

            Assert.That(preferences.VfxDensity, Is.EqualTo(VfxDensityLevel.Full));
            Assert.That(preferences.CameraMotion, Is.EqualTo(PresentationResponseLevel.Reduced));
            Assert.That(preferences.SetVfxDensity(VfxDensityLevel.Reduced), Is.True);

            DemoFlowPreferences reloaded = new(store);
            reloaded.Load();
            Assert.That(store.ReadRaw(), Does.Contain($"\"version\":{DemoFlowPreferences.CurrentVersion}"));
            Assert.That(reloaded.VfxDensity, Is.EqualTo(VfxDensityLevel.Reduced));
        }

        [Test]
        public void PartialOrFailedVfxDensityStaysSafeAndRetries()
        {
            MemoryStore partial = new();
            partial.Seed("{\"version\":3,\"visualStyle\":0,\"handlingModels\":[0,0,0,0]," +
                         "\"vehicleFamilies\":[0,0,0,0],\"cameraMotion\":2,\"vehicleMotion\":2," +
                         "\"hasPresentationResponseSettings\":true}");
            DemoFlowPreferences repaired = new(partial);
            repaired.Load();
            Assert.That(repaired.VfxDensity, Is.EqualTo(VfxDensityLevel.Full));
            Assert.That(repaired.RecoveryMessage, Does.Contain("SAFE DEFAULTS"));

            MemoryStore failing = new() { FailWrites = true };
            DemoFlowPreferences preferences = new(failing);
            Assert.That(preferences.SetVfxDensity(VfxDensityLevel.Off), Is.False);
            failing.FailWrites = false;
            Assert.That(preferences.SetVfxDensity(VfxDensityLevel.Off), Is.True);
        }

        [Test]
        public void PartialOrFailedMotionSettingsStaySafeAndRetry()
        {
            MemoryStore partial = new();
            partial.Seed("{\"version\":2,\"visualStyle\":0,\"handlingModels\":[0,0,0,0],\"vehicleFamilies\":[0,0,0,0]}");
            DemoFlowPreferences repaired = new(partial);
            repaired.Load();
            Assert.That(repaired.CameraMotion, Is.EqualTo(PresentationResponseLevel.Full));
            Assert.That(repaired.RecoveryMessage, Does.Contain("SAFE DEFAULTS"));

            MemoryStore failing = new() { FailWrites = true };
            DemoFlowPreferences preferences = new(failing);
            Assert.That(preferences.SetPresentationResponseLevels(PresentationResponseLevel.Reduced,
                PresentationResponseLevel.Off), Is.False);
            failing.FailWrites = false;
            Assert.That(preferences.SetPresentationResponseLevels(PresentationResponseLevel.Reduced,
                PresentationResponseLevel.Off), Is.True);
        }

        [Test]
        public void InvalidSlotAndCatalogValuesAreRejected()
        {
            DemoFlowPreferences preferences = new(new MemoryStore());
            Assert.Throws<ArgumentOutOfRangeException>(() => preferences.GetHandlingModel(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => preferences.GetVehicleFamily(4));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                preferences.SetPlayerSelection(0, (HandlingModelId)99, VehicleFamilyId.Touring));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                preferences.SetPlayerSelection(0, HandlingModelId.A, (VehicleFamilyId)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => preferences.SetVisualStyle((VisualStyleMode)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => preferences.SetPresentationResponseLevels(
                (PresentationResponseLevel)99, PresentationResponseLevel.Full));
            Assert.Throws<ArgumentOutOfRangeException>(() => preferences.SetVfxDensity((VfxDensityLevel)99));
        }
    }
}
