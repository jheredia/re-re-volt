using System;
using Core;
using UnityEngine;
using Vehicle;

namespace Presentation
{
    /// <summary>Small storage boundary so demo-flow preferences can be tested without global PlayerPrefs state.</summary>
    public interface ILocalPreferenceStore
    {
        bool TryRead(string key, out string value);
        void Write(string key, string value);
    }

    /// <summary>Runtime adapter for the platform-owned local preference store.</summary>
    public sealed class PlayerPrefsLocalPreferenceStore : ILocalPreferenceStore
    {
        public bool TryRead(string key, out string value)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                value = null;
                return false;
            }

            value = PlayerPrefs.GetString(key);
            return true;
        }

        public void Write(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Versioned, slot-scoped demo preferences. Runtime ownership stays with Stage3Bootstrap and no gameplay state is
    /// serialized. Unknown or corrupt payloads remain in storage until the player makes a new valid selection.
    /// </summary>
    public sealed class DemoFlowPreferences
    {
        [Serializable]
        private sealed class Payload
        {
            public int version;
            public int visualStyle;
            public int[] handlingModels;
            public int[] vehicleFamilies;
            public int cameraMotion;
            public int vehicleMotion;
            public bool hasPresentationResponseSettings;
            public int vfxDensity;
            public bool hasVfxDensitySetting;
        }

        public const int CurrentVersion = 3;
        public const string StorageKey = "re-re-volt.demo-flow.preferences";

        private readonly ILocalPreferenceStore store;
        private readonly HandlingModelId[] handlingModels = new HandlingModelId[LocalPlayerRules.MaximumPlayers];
        private readonly VehicleFamilyId[] vehicleFamilies = new VehicleFamilyId[LocalPlayerRules.MaximumPlayers];
        private bool dirty;

        public DemoFlowPreferences(ILocalPreferenceStore preferenceStore)
        {
            store = preferenceStore ?? throw new ArgumentNullException(nameof(preferenceStore));
            ResetToDefaults();
        }

        public VisualStyleMode VisualStyle { get; private set; }
        public PresentationResponseLevel CameraMotion { get; private set; }
        public PresentationResponseLevel VehicleMotion { get; private set; }
        public VfxDensityLevel VfxDensity { get; private set; }
        public string RecoveryMessage { get; private set; } = string.Empty;

        public void Load()
        {
            ResetToDefaults();
            RecoveryMessage = string.Empty;
            dirty = false;
            try
            {
                if (!store.TryRead(StorageKey, out string json) || string.IsNullOrWhiteSpace(json)) return;
                LoadJson(json);
            }
            catch (Exception)
            {
                ReportRecovery("LOCAL SETTINGS COULD NOT BE READ — SAFE DEFAULTS ARE ACTIVE");
            }
        }

        private void LoadJson(string json)
        {
            Payload payload;
            try
            {
                payload = JsonUtility.FromJson<Payload>(json);
            }
            catch (Exception)
            {
                ReportRecovery("LOCAL SETTINGS COULD NOT BE READ — SAFE DEFAULTS ARE ACTIVE");
                return;
            }

            if (payload == null || (payload.version != 1 && payload.version != 2 && payload.version != CurrentVersion))
            {
                ReportRecovery("LOCAL SETTINGS VERSION WAS NOT RECOGNIZED — SAFE DEFAULTS ARE ACTIVE");
                return;
            }

            bool repaired = false;
            if (Enum.IsDefined(typeof(VisualStyleMode), payload.visualStyle))
                VisualStyle = (VisualStyleMode)payload.visualStyle;
            else
                repaired = true;

            for (int i = 0; i < LocalPlayerRules.MaximumPlayers; i++)
            {
                if (payload.handlingModels != null && i < payload.handlingModels.Length &&
                    HandlingModelCatalog.IsDefined((HandlingModelId)payload.handlingModels[i]))
                    handlingModels[i] = (HandlingModelId)payload.handlingModels[i];
                else
                    repaired = true;

                if (payload.vehicleFamilies != null && i < payload.vehicleFamilies.Length &&
                    VehicleFamilyCatalog.IsDefined((VehicleFamilyId)payload.vehicleFamilies[i]))
                    vehicleFamilies[i] = (VehicleFamilyId)payload.vehicleFamilies[i];
                else
                    repaired = true;
            }

            if (payload.version >= 2 && payload.hasPresentationResponseSettings)
            {
                if (IsPresentationLevel(payload.cameraMotion)) CameraMotion = (PresentationResponseLevel)payload.cameraMotion;
                else repaired = true;
                if (IsPresentationLevel(payload.vehicleMotion)) VehicleMotion = (PresentationResponseLevel)payload.vehicleMotion;
                else repaired = true;
            }
            else if (payload.version >= 2)
                repaired = true;

            if (payload.version == CurrentVersion && payload.hasVfxDensitySetting)
            {
                if (IsVfxDensity(payload.vfxDensity)) VfxDensity = (VfxDensityLevel)payload.vfxDensity;
                else repaired = true;
            }
            else if (payload.version == CurrentVersion)
                repaired = true;

            if (repaired)
                ReportRecovery("SOME LOCAL SETTINGS WERE INVALID — SAFE DEFAULTS FILLED THE GAPS");
        }

        public HandlingModelId GetHandlingModel(int playerIndex)
        {
            ValidatePlayerIndex(playerIndex);
            return handlingModels[playerIndex];
        }

        public VehicleFamilyId GetVehicleFamily(int playerIndex)
        {
            ValidatePlayerIndex(playerIndex);
            return vehicleFamilies[playerIndex];
        }

        public bool SetPlayerSelection(int playerIndex, HandlingModelId handlingModel, VehicleFamilyId vehicleFamily)
        {
            ValidatePlayerIndex(playerIndex);
            if (!HandlingModelCatalog.IsDefined(handlingModel))
                throw new ArgumentOutOfRangeException(nameof(handlingModel));
            if (!VehicleFamilyCatalog.IsDefined(vehicleFamily))
                throw new ArgumentOutOfRangeException(nameof(vehicleFamily));
            if (handlingModels[playerIndex] == handlingModel && vehicleFamilies[playerIndex] == vehicleFamily)
                return !dirty || Save();

            handlingModels[playerIndex] = handlingModel;
            vehicleFamilies[playerIndex] = vehicleFamily;
            dirty = true;
            return Save();
        }

        public bool SetVisualStyle(VisualStyleMode mode)
        {
            if (!Enum.IsDefined(typeof(VisualStyleMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (VisualStyle == mode) return !dirty || Save();
            VisualStyle = mode;
            dirty = true;
            return Save();
        }

        public bool SetPresentationResponseLevels(PresentationResponseLevel cameraMotion,
            PresentationResponseLevel vehicleMotion)
        {
            if (!IsPresentationLevel((int)cameraMotion)) throw new ArgumentOutOfRangeException(nameof(cameraMotion));
            if (!IsPresentationLevel((int)vehicleMotion)) throw new ArgumentOutOfRangeException(nameof(vehicleMotion));
            if (CameraMotion == cameraMotion && VehicleMotion == vehicleMotion) return !dirty || Save();
            CameraMotion = cameraMotion;
            VehicleMotion = vehicleMotion;
            dirty = true;
            return Save();
        }

        public bool SetVfxDensity(VfxDensityLevel vfxDensity)
        {
            if (!IsVfxDensity((int)vfxDensity)) throw new ArgumentOutOfRangeException(nameof(vfxDensity));
            if (VfxDensity == vfxDensity) return !dirty || Save();
            VfxDensity = vfxDensity;
            dirty = true;
            return Save();
        }

        internal void ReportRecovery(string message)
        {
            RecoveryMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
        }

        private bool Save()
        {
            Payload payload = new()
            {
                version = CurrentVersion,
                visualStyle = (int)VisualStyle,
                handlingModels = new int[LocalPlayerRules.MaximumPlayers],
                vehicleFamilies = new int[LocalPlayerRules.MaximumPlayers],
                cameraMotion = (int)CameraMotion,
                vehicleMotion = (int)VehicleMotion,
                hasPresentationResponseSettings = true,
                vfxDensity = (int)VfxDensity,
                hasVfxDensitySetting = true
            };
            for (int i = 0; i < LocalPlayerRules.MaximumPlayers; i++)
            {
                payload.handlingModels[i] = (int)handlingModels[i];
                payload.vehicleFamilies[i] = (int)vehicleFamilies[i];
            }

            try
            {
                store.Write(StorageKey, JsonUtility.ToJson(payload));
                dirty = false;
                RecoveryMessage = string.Empty;
                return true;
            }
            catch (Exception)
            {
                dirty = true;
                ReportRecovery("LOCAL SETTINGS COULD NOT BE SAVED — THIS SESSION CAN CONTINUE");
                return false;
            }
        }

        private void ResetToDefaults()
        {
            VisualStyle = VisualStyleMode.Clean3D;
            CameraMotion = PresentationResponseLevel.Full;
            VehicleMotion = PresentationResponseLevel.Full;
            VfxDensity = VfxDensityLevel.Full;
            for (int i = 0; i < LocalPlayerRules.MaximumPlayers; i++)
            {
                handlingModels[i] = HandlingModelId.A;
                vehicleFamilies[i] = VehicleFamilyCatalog.DefaultId;
            }
        }

        private static void ValidatePlayerIndex(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= LocalPlayerRules.MaximumPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
        }

        private static bool IsPresentationLevel(int value) =>
            Enum.IsDefined(typeof(PresentationResponseLevel), value);

        private static bool IsVfxDensity(int value) => Enum.IsDefined(typeof(VfxDensityLevel), value);
    }
}
