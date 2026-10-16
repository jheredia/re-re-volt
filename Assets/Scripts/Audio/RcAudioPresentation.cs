using System;
using Core;
using Presentation;
using UnityEngine;
using Vehicle;
using Weapons;

namespace AudioPresentation
{
    /// <summary>
    /// Session-owned, split-screen-safe audio feedback. Uses one central 2D mix and never creates listeners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RcAudioPresentation : MonoBehaviour
    {
        public const int MaximumOneShotVoices = 6;
        public const int MaximumEngineVoices = LocalPlayerRules.MaximumPlayers;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = .8f;
        [SerializeField, Range(0f, 1f)] private float cueVolume = .72f;
        [SerializeField, Range(0f, 1f)] private float engineVolume = .18f;

        [Header("Vehicle feedback")]
        [SerializeField, Min(0f)] private float minimumImpactImpulse = .75f;
        [SerializeField, Min(.01f)] private float fullImpactImpulse = 8f;
        [SerializeField, Min(0f)] private float impactCooldown = .14f;
        [SerializeField, Min(1f)] private float referenceTopSpeedKph = 90f;

        private readonly AudioSource[] oneShotSources = new AudioSource[MaximumOneShotVoices];
        private readonly float[] oneShotBusyUntil = new float[MaximumOneShotVoices];
        private readonly float[] oneShotStartedAt = new float[MaximumOneShotVoices];
        private readonly int[] oneShotPriorities = new int[MaximumOneShotVoices];
        private readonly int[] oneShotRecipients = new int[MaximumOneShotVoices];
        private readonly float[] oneShotGains = new float[MaximumOneShotVoices];
        private readonly AudioSource[] engineSources = new AudioSource[MaximumEngineVoices];
        private readonly LocalPlayer[] enginePlayers = new LocalPlayer[MaximumEngineVoices];
        private readonly VehicleImpactAudioEmitter[] impactEmitters =
            new VehicleImpactAudioEmitter[MaximumEngineVoices];
        private readonly bool[] ownsImpactEmitter = new bool[MaximumEngineVoices];
        private readonly float[] nextImpactAt = new float[MaximumEngineVoices];
        private readonly float[] nextSurfaceAt = new float[MaximumEngineVoices];
        private readonly bool[] skidding = new bool[MaximumEngineVoices];
        private readonly Action<RecoveryPresentationPayload>[] recoveryCallbacks =
            new Action<RecoveryPresentationPayload>[MaximumEngineVoices];
        private readonly float[,] nextCueAt = new float[(int)AudioCueId.Count, MaximumEngineVoices + 1];
        private readonly uint[,] cueSequences = new uint[(int)AudioCueId.Count, MaximumEngineVoices + 1];
        private readonly RaycastHit[] surfaceHits = new RaycastHit[16];

        private RaceManager race;
        private VehicleManager vehicles;
        private WeaponManager weapons;
        private Stage3RacePresentation presentation;
        private LayerMask surfaceMask = ~0;
        private ProceduralAudioBank bank;
        private GameObject audioRoot;
        private bool configured;
        private bool subscribed;
        private bool audioAvailable;
        private bool wasPaused;
        private float nextAmbienceAt;
        private uint ambienceSequence;

        public bool IsConfigured => configured;
        public bool AudioAvailable => audioAvailable;
        public int GeneratedClipCount => bank?.ClipCount ?? 0;
        public int OneShotVoiceCapacity => oneShotSources.Length;
        public int EngineVoiceCapacity => engineSources.Length;
        public int ActiveEngineVoiceCount { get; private set; }
        public int ImpactEmitterCount { get; private set; }
        public int AcceptedCueCount { get; private set; }
        public int DroppedCueCount { get; private set; }
        public int RequestedCueCount { get; private set; }
        public int StolenCueCount { get; private set; }
        public int CapacityDroppedCueCount { get; private set; }
        public int CooldownDroppedCueCount { get; private set; }
        public int SuppressedCueCount { get; private set; }
        public int SurfaceClassificationMissCount { get; private set; }
        public int OneShotHighWaterCount { get; private set; }
        public int LastCuePlayerIndex { get; private set; } = -1;
        public float CurrentDuckScale { get; private set; } = 1f;
        public AudioCueId LastCue { get; private set; } = AudioCueId.Count;

        public void Configure(RaceManager raceManager, VehicleManager roster, WeaponManager combat = null,
            Stage3RacePresentation racePresentation = null, LayerMask? drivableMask = null)
        {
            if (configured) throw new InvalidOperationException("Audio presentation is already configured.");
            if (raceManager == null) throw new ArgumentNullException(nameof(raceManager));
            if (roster == null) throw new ArgumentNullException(nameof(roster));

            race = raceManager;
            vehicles = roster;
            weapons = combat;
            presentation = racePresentation;
            surfaceMask = drivableMask ?? (LayerMask)~0;
            nextAmbienceAt = Time.unscaledTime + 9f;
            configured = true;

            try
            {
                bank = new ProceduralAudioBank();
                BuildSources();
                audioAvailable = true;
            }
            catch (Exception exception)
            {
                audioAvailable = false;
                bank?.Dispose();
                bank = null;
                ReleaseSourceRoot();
                Debug.LogWarning($"[Audio] Optional procedural audio is unavailable: {exception.Message}", this);
            }

            if (isActiveAndEnabled)
            {
                Subscribe();
                SynchronizePlayers();
            }
        }

        public void SetTrackSurfaceMask(LayerMask drivableMask)
        {
            surfaceMask = drivableMask;
        }

        private void BuildSources()
        {
            audioRoot = new GameObject("Stage8_RuntimeAudio");
            audioRoot.transform.SetParent(transform, false);
            for (int i = 0; i < oneShotSources.Length; i++)
                oneShotSources[i] = CreateSource($"OneShot_{i + 1}", false);
            for (int i = 0; i < engineSources.Length; i++)
            {
                AudioSource source = CreateSource($"Engine_{i + 1}", true);
                source.clip = bank.Get(AudioCueId.Engine);
                source.volume = 0f;
                engineSources[i] = source;
            }
        }

        private AudioSource CreateSource(string label, bool loop)
        {
            GameObject sourceObject = new GameObject(label);
            sourceObject.transform.SetParent(audioRoot.transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private void OnEnable()
        {
            if (!configured) return;
            if (audioRoot != null) audioRoot.SetActive(true);
            Subscribe();
            SynchronizePlayers();
            RefreshImpactSubscriptions();
        }

        private void Subscribe()
        {
            if (subscribed) return;
            race.CountdownChanged += OnCountdownChanged;
            race.RaceCompleted += OnRaceCompleted;
            race.RaceStateChanged += OnRaceStateChanged;
            vehicles.PlayerJoined += OnPlayerJoined;
            vehicles.PlayerLeft += OnPlayerLeft;
            vehicles.HandlingModelChanged += OnSelectionChanged;
            vehicles.VehicleFamilyChanged += OnSelectionChanged;
            vehicles.ControllerConnectionChanged += OnConnectionChanged;
            if (weapons != null)
            {
                weapons.PickupPresentationAcquired += OnPickupAcquired;
                weapons.WeaponPresentationSpawned += OnWeaponSpawned;
                weapons.WeaponPresentationResolved += OnWeaponResolved;
                weapons.CombatCleared += OnCombatCleared;
            }
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (race != null)
            {
                race.CountdownChanged -= OnCountdownChanged;
                race.RaceCompleted -= OnRaceCompleted;
                race.RaceStateChanged -= OnRaceStateChanged;
            }
            if (vehicles != null)
            {
                vehicles.PlayerJoined -= OnPlayerJoined;
                vehicles.PlayerLeft -= OnPlayerLeft;
                vehicles.HandlingModelChanged -= OnSelectionChanged;
                vehicles.VehicleFamilyChanged -= OnSelectionChanged;
                vehicles.ControllerConnectionChanged -= OnConnectionChanged;
            }
            if (weapons != null)
            {
                weapons.PickupPresentationAcquired -= OnPickupAcquired;
                weapons.WeaponPresentationSpawned -= OnWeaponSpawned;
                weapons.WeaponPresentationResolved -= OnWeaponResolved;
                weapons.CombatCleared -= OnCombatCleared;
            }
            subscribed = false;
        }

        private void SynchronizePlayers()
        {
            for (int slot = 0; slot < enginePlayers.Length; slot++)
            {
                LocalPlayer found = null;
                for (int i = 0; i < vehicles.Players.Count; i++)
                    if (vehicles.Players[i].PlayerIndex == slot) found = vehicles.Players[i];
                if (!ReferenceEquals(found, enginePlayers[slot]))
                {
                    ReleasePlayer(slot);
                    if (found != null) BindPlayer(found);
                }
            }
            RefreshCounts();
        }

        private void OnPlayerJoined(LocalPlayer player)
        {
            BindPlayer(player);
            RefreshCounts();
            TryPlayCue(AudioCueId.UiConfirm);
        }

        private void BindPlayer(LocalPlayer player)
        {
            int slot = player?.PlayerIndex ?? -1;
            if (slot < 0 || slot >= enginePlayers.Length || ReferenceEquals(enginePlayers[slot], player)) return;
            ReleasePlayer(slot);
            enginePlayers[slot] = player;
            if (player.Vehicle != null)
            {
                VehicleImpactAudioEmitter emitter = player.Vehicle.GetComponent<VehicleImpactAudioEmitter>();
                ownsImpactEmitter[slot] = emitter == null;
                if (emitter == null)
                {
                    emitter = player.Vehicle.gameObject.AddComponent<VehicleImpactAudioEmitter>();
                    emitter.Configure(slot, minimumImpactImpulse);
                }
                emitter.ImpactObserved += OnVehicleImpact;
                impactEmitters[slot] = emitter;
            }
            recoveryCallbacks[slot] = payload => OnRecovered(slot, payload);
            if (player.Recovery != null) player.Recovery.RecoveredAt += recoveryCallbacks[slot];
            AudioSource engine = engineSources[slot];
            if (audioAvailable && engine != null && engine.clip != null && !engine.isPlaying) engine.Play();
        }

        private void OnPlayerLeft(int playerIndex)
        {
            ReleasePlayer(playerIndex);
            RefreshCounts();
        }

        private void ReleasePlayer(int slot)
        {
            if (slot < 0 || slot >= enginePlayers.Length) return;
            VehicleImpactAudioEmitter emitter = impactEmitters[slot];
            if (emitter != null)
            {
                emitter.ImpactObserved -= OnVehicleImpact;
                if (ownsImpactEmitter[slot])
                {
                    if (Application.isPlaying) Destroy(emitter);
                    else DestroyImmediate(emitter);
                }
            }
            impactEmitters[slot] = null;
            if (enginePlayers[slot]?.Recovery != null && recoveryCallbacks[slot] != null)
                enginePlayers[slot].Recovery.RecoveredAt -= recoveryCallbacks[slot];
            recoveryCallbacks[slot] = null;
            ClearRecipient(slot);
            for (int i = 0; i < (int)AudioCueId.Count; i++)
            {
                nextCueAt[i, slot] = 0f;
                cueSequences[i, slot] = 0;
            }
            ownsImpactEmitter[slot] = false;
            enginePlayers[slot] = null;
            nextImpactAt[slot] = 0f;
            AudioSource source = engineSources[slot];
            if (source != null)
            {
                source.Stop();
                source.volume = 0f;
            }
        }

        private void RefreshCounts()
        {
            int players = 0;
            int emitters = 0;
            for (int i = 0; i < enginePlayers.Length; i++)
            {
                if (enginePlayers[i] != null) players++;
                if (impactEmitters[i] != null) emitters++;
            }
            ActiveEngineVoiceCount = players;
            ImpactEmitterCount = emitters;
        }

        private void RefreshImpactSubscriptions()
        {
            for (int i = 0; i < impactEmitters.Length; i++)
            {
                VehicleImpactAudioEmitter emitter = impactEmitters[i];
                if (emitter == null) continue;
                emitter.ImpactObserved -= OnVehicleImpact;
                emitter.ImpactObserved += OnVehicleImpact;
            }
            for (int i = 0; i < enginePlayers.Length; i++)
            {
                if (enginePlayers[i]?.Recovery == null || recoveryCallbacks[i] == null) continue;
                enginePlayers[i].Recovery.RecoveredAt -= recoveryCallbacks[i];
                enginePlayers[i].Recovery.RecoveredAt += recoveryCallbacks[i];
            }
        }

        private void Update()
        {
            if (!audioAvailable) return;
            bool paused = IsPaused;
            if (paused && !wasPaused) ClearTransientAudio();
            wasPaused = paused;
            float now = Time.unscaledTime;
            RefreshOneShotMix(now);
            float mixScale = RcAudioMixPolicy.PlayerScale(ActiveEngineVoiceCount);
            float response = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            for (int i = 0; i < enginePlayers.Length; i++)
            {
                LocalPlayer player = enginePlayers[i];
                AudioSource source = engineSources[i];
                if (player == null || player.Vehicle == null || source == null) continue;
                float speed = Mathf.Clamp01(player.Vehicle.CurrentSpeed / referenceTopSpeedKph);
                float throttle = Mathf.Abs(player.Vehicle.ThrottleInput);
                float targetVolume = masterVolume * engineVolume * mixScale * CurrentDuckScale *
                    (.32f + speed * .5f + throttle * .18f);
                if (!player.IsConnected) targetVolume *= .25f;
                if (paused) targetVolume = 0f;
                source.volume = paused ? 0f : Mathf.Lerp(source.volume, targetVolume, response);
                source.pitch = .72f + speed * 1.08f + throttle * .12f;
                if (!source.isPlaying && source.clip != null) source.Play();
                UpdateTires(i, player, now);
            }
            if (!paused && ActiveEngineVoiceCount > 0 && now >= nextAmbienceAt &&
                (race.CurrentState == RaceManager.RaceState.Idle || race.RaceRunning))
            {
                TryPlayCue(AudioCueId.ClubAmbience);
                // Sparse workshop tool/radio texture: one short cue every 9–13 seconds, no extra loop/source.
                nextAmbienceAt = now + 9f + (ambienceSequence++ % 5);
            }
        }

        private void OnCountdownChanged(int number)
        {
            if (number > 0) TryPlayCue(AudioCueId.Countdown);
            else if (number == 0) TryPlayCue(AudioCueId.Go);
        }

        private void OnRaceCompleted(System.Collections.Generic.IReadOnlyList<RacerState> standings)
        {
            // Results intentionally freeze gameplay time before RaceCompleted is published. Clear the racing mix,
            // mark the pause edge as handled, then allow only this unscaled session cue to survive in results.
            ClearTransientAudio();
            wasPaused = true;
            TryPlayRoutedCue(AudioCueId.Finish, -1, 1f, 1f, true);
        }

        private bool IsPaused => (presentation != null && presentation.IsPaused) || Time.timeScale <= 0f;

        private bool OwnsConnectedPlayer(int slot) => slot >= 0 && slot < enginePlayers.Length &&
            enginePlayers[slot] != null && enginePlayers[slot].IsConnected;

        private void OnSelectionChanged(LocalPlayer player) =>
            TryPlayPlayerCue(AudioCueId.UiConfirm, player.PlayerIndex);

        private void OnPickupAcquired(PickupAcquiredPresentationPayload payload)
        {
            if (race.RaceRunning) TryPlayPlayerCue(AudioCueId.Pickup, payload.CollectorPlayerIndex);
        }

        private void OnWeaponSpawned(WeaponSpawnedPresentationPayload payload)
        {
            if (race.RaceRunning) TryPlayPlayerCue(payload.Kind == WeaponKind.Bolt
                ? AudioCueId.BoltLaunch : AudioCueId.OilDrop, payload.SourcePlayerIndex);
        }

        private void OnWeaponResolved(WeaponResolvedPresentationPayload payload)
        {
            // Rejected/world hits do not fabricate a victim. Accepted payloads supply the exact recipient.
            if (race.RaceRunning && payload.AcceptedHit)
                TryPlayPlayerCue(payload.Kind == WeaponKind.Bolt ? AudioCueId.BoltHit : AudioCueId.OilHit,
                    payload.TargetPlayerIndex);
        }

        private void OnRecovered(int slot, RecoveryPresentationPayload payload)
        {
            ClearRecipient(slot);
            if (race.RaceRunning) TryPlayPlayerCue(AudioCueId.Recovery, slot);
        }

        private void OnConnectionChanged(LocalPlayer player)
        {
            if (!player.IsConnected) ClearRecipient(player.PlayerIndex);
        }

        private void OnRaceStateChanged(RaceManager.RaceState state)
        {
            if (state != RaceManager.RaceState.Racing) ClearTransientAudio();
            nextAmbienceAt = Time.unscaledTime + 9f;
        }

        private void OnCombatCleared()
        {
            for (int i = 0; i < enginePlayers.Length; i++) ClearRecipient(i);
        }

        private void OnVehicleImpact(VehicleImpactPayload payload)
        {
            if (!race.RaceRunning || IsPaused || !OwnsConnectedPlayer(payload.PlayerIndex)) return;
            if (!TryPlayImpactFeedback(payload.PlayerIndex, payload.Impulse)) return;
            // Body contact and explicit material texture are separate bounded voices. Unknown surfaces retain
            // the body cue only; vehicle contact is explicit and never masquerades as a track material.
            if (payload.OtherCollider != null &&
                payload.OtherCollider.GetComponentInParent<VehicleController>() != null) return;
            if (TrackSurfaceMarker.TryResolve(payload.OtherCollider, out TrackSurfaceKind surface) &&
                RcAudioCueData.TrySurfaceCue(surface, out AudioCueId cue))
                TryPlayPlayerCue(cue, payload.PlayerIndex, .65f);
            else SurfaceClassificationMissCount++;
        }

        private void UpdateTires(int slot, LocalPlayer player, float now)
        {
            VehicleController vehicle = player.Vehicle;
            if (IsPaused || !race.RaceRunning || !player.IsConnected || player.Racer.IsFinished ||
                !vehicle.isActiveAndEnabled || vehicle.GroundedWheelCount == 0 || vehicle.CurrentSpeed < 2f)
            {
                if (skidding[slot] && !IsPaused && race.RaceRunning && player.IsConnected &&
                    !player.Racer.IsFinished && vehicle.GroundedWheelCount > 0)
                    TryPlayPlayerCue(AudioCueId.SkidEnd, slot);
                skidding[slot] = false;
                return;
            }
            if (now < nextSurfaceAt[slot]) return;
            nextSurfaceAt[slot] = now + .32f + slot * .015f;
            if (!TryFindSurface(vehicle, out AudioCueId tireCue))
            {
                skidding[slot] = false;
                SurfaceClassificationMissCount++;
                return;
            }
            bool drift = vehicle.IsDrifting;
            float speed = Mathf.Clamp01(vehicle.CurrentSpeed / referenceTopSpeedKph);
            if (drift != skidding[slot])
                TryPlayPlayerCue(drift ? AudioCueId.SkidStart : AudioCueId.SkidEnd, slot);
            skidding[slot] = drift;
            TryPlayPlayerCue(tireCue, slot, .4f + speed * .6f);
            if (drift) TryPlayPlayerCue(AudioCueId.SkidScrub, slot, .4f + speed * .6f);
        }

        private bool TryFindSurface(VehicleController vehicle, out AudioCueId cue)
        {
            cue = AudioCueId.Count;
            int count = Physics.RaycastNonAlloc(vehicle.transform.position + Vector3.up * .5f, Vector3.down,
                surfaceHits, 2.5f, surfaceMask, QueryTriggerInteraction.Ignore);
            if (count == surfaceHits.Length) return false;
            Collider support = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = surfaceHits[i];
                if (hit.collider == null || hit.collider.GetComponentInParent<VehicleController>() != null ||
                    hit.normal.y < .5f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                support = hit.collider;
            }
            return TrackSurfaceMarker.TryResolve(support, out TrackSurfaceKind surface) &&
                RcAudioCueData.TrySurfaceCue(surface, out cue);
        }

        /// <summary>Bounded presentation seam used by collision and combat adapters.</summary>
        public bool TryPlayImpactFeedback(int playerIndex, float impulse)
        {
            if (!OwnsConnectedPlayer(playerIndex) || IsPaused || float.IsNaN(impulse) ||
                float.IsInfinity(impulse) || impulse < minimumImpactImpulse)
                return false;
            float now = Time.unscaledTime;
            if (now < nextImpactAt[playerIndex])
            {
                DroppedCueCount++;
                RequestedCueCount++;
                CooldownDroppedCueCount++;
                return false;
            }
            float intensity = Mathf.Clamp01(impulse / Mathf.Max(.01f, fullImpactImpulse));
            AudioCueId cue = impulse >= fullImpactImpulse ? AudioCueId.HeavyImpact : AudioCueId.Impact;
            if (!TryPlayPlayerCue(cue, playerIndex, Mathf.Lerp(.35f, 1f, intensity),
                    Mathf.Lerp(.82f, 1.12f, intensity)))
                return false;
            nextImpactAt[playerIndex] = now + impactCooldown;
            return true;
        }

        /// <summary>Legacy session-cue seam. One-shot capacity stays six; important cues steal only lower tiers.</summary>
        public bool TryPlayCue(AudioCueId cue, float volumeScale = 1f, float pitch = 1f)
            => TryPlayRoutedCue(cue, -1, volumeScale, pitch);

        public bool TryPlayPlayerCue(AudioCueId cue, int playerIndex, float volumeScale = 1f, float pitch = 1f)
        {
            if (!OwnsConnectedPlayer(playerIndex)) return false;
            return TryPlayRoutedCue(cue, playerIndex, volumeScale, pitch);
        }

        private bool TryPlayRoutedCue(AudioCueId cue, int recipient, float volumeScale, float pitch,
            bool allowWhilePaused = false)
        {
            int cueIndex = (int)cue;
            if (cueIndex <= (int)AudioCueId.Engine || cueIndex >= (int)AudioCueId.Count ||
                float.IsNaN(volumeScale) || float.IsInfinity(volumeScale) || float.IsNaN(pitch) ||
                float.IsInfinity(pitch))
                return false;
            RequestedCueCount++;
            if (!isActiveAndEnabled || !audioAvailable || (IsPaused && !allowWhilePaused))
            {
                SuppressedCueCount++;
                return false;
            }
            AudioClip clip = bank.Get(cue);
            if (clip == null) { SuppressedCueCount++; return false; }
            float now = Time.unscaledTime;
            int recipientSlot = recipient >= 0 ? recipient : MaximumEngineVoices;
            AudioCueDefinition data = RcAudioCueData.Get(cue);
            if (now < nextCueAt[cueIndex, recipientSlot])
            {
                DroppedCueCount++;
                CooldownDroppedCueCount++;
                return false;
            }
            int selectedVoice = RcAudioMixPolicy.SelectVoice(now, data.Priority, oneShotBusyUntil,
                oneShotStartedAt, oneShotPriorities, out bool stolen);
            if (selectedVoice >= 0)
            {
                AudioSource source = oneShotSources[selectedVoice];
                RcAudioCueData.Variation(cue, recipient, cueSequences[cueIndex, recipientSlot]++,
                    out float variedPitch, out float variedGain);
                source.Stop();
                source.clip = clip;
                source.pitch = Mathf.Clamp(pitch * variedPitch, .25f, 3f);
                source.priority = 128 - data.Priority * 24;
                oneShotGains[selectedVoice] = masterVolume * cueVolume * Mathf.Clamp01(volumeScale) * variedGain;
                oneShotPriorities[selectedVoice] = data.Priority;
                oneShotRecipients[selectedVoice] = recipient;
                oneShotStartedAt[selectedVoice] = now;
                oneShotBusyUntil[selectedVoice] = now + clip.length / Mathf.Abs(source.pitch);
                nextCueAt[cueIndex, recipientSlot] = now + data.Cooldown;
                RefreshOneShotMix(now);
                source.Play();
                LastCue = cue;
                LastCuePlayerIndex = recipient;
                AcceptedCueCount++;
                if (stolen) StolenCueCount++;
                return true;
            }
            DroppedCueCount++;
            CapacityDroppedCueCount++;
            return false;
        }

        private void RefreshOneShotMix(float now)
        {
            int active = 0;
            int priority = 0;
            for (int i = 0; i < oneShotSources.Length; i++)
                if (now < oneShotBusyUntil[i])
                {
                    active++;
                    priority = Mathf.Max(priority, oneShotPriorities[i]);
                }
            OneShotHighWaterCount = Mathf.Max(OneShotHighWaterCount, active);
            CurrentDuckScale = RcAudioMixPolicy.DuckScale(priority);
            for (int i = 0; i < oneShotSources.Length; i++)
            {
                if (oneShotSources[i] == null) continue;
                float scale = oneShotPriorities[i] >= 3 ? 1f :
                    RcAudioMixPolicy.PlayerScale(ActiveEngineVoiceCount) * CurrentDuckScale;
                oneShotSources[i].volume = oneShotGains[i] * scale;
            }
        }

        public int GetActiveRecipientVoiceCount(int playerIndex)
        {
            int count = 0;
            for (int i = 0; i < oneShotSources.Length; i++)
                if (Time.unscaledTime < oneShotBusyUntil[i] && oneShotRecipients[i] == playerIndex) count++;
            return count;
        }

        private void ClearRecipient(int slot)
        {
            if (slot < 0 || slot >= enginePlayers.Length) return;
            for (int i = 0; i < oneShotSources.Length; i++)
                if (oneShotRecipients[i] == slot) ClearVoice(i);
            skidding[slot] = false;
            nextSurfaceAt[slot] = 0f;
            nextImpactAt[slot] = 0f;
            Array.Clear(surfaceHits, 0, surfaceHits.Length);
        }

        private void ClearVoice(int voice)
        {
            if (oneShotSources[voice] != null)
            {
                oneShotSources[voice].Stop();
                oneShotSources[voice].clip = null;
                oneShotSources[voice].volume = 0f;
            }
            oneShotBusyUntil[voice] = 0f;
            oneShotStartedAt[voice] = 0f;
            oneShotPriorities[voice] = 0;
            oneShotRecipients[voice] = -1;
            oneShotGains[voice] = 0f;
        }

        private void ClearTransientAudio()
        {
            for (int i = 0; i < oneShotSources.Length; i++) ClearVoice(i);
            for (int i = 0; i < enginePlayers.Length; i++) ClearRecipient(i);
            Array.Clear(nextCueAt, 0, nextCueAt.Length);
            CurrentDuckScale = 1f;
        }

        private void OnDisable()
        {
            Unsubscribe();
            for (int i = 0; i < impactEmitters.Length; i++)
            {
                if (impactEmitters[i] != null) impactEmitters[i].ImpactObserved -= OnVehicleImpact;
                if (enginePlayers[i]?.Recovery != null && recoveryCallbacks[i] != null)
                    enginePlayers[i].Recovery.RecoveredAt -= recoveryCallbacks[i];
            }
            ClearTransientAudio();
            for (int i = 0; i < engineSources.Length; i++)
                if (engineSources[i] != null) engineSources[i].Stop();
            if (audioRoot != null) audioRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            for (int i = 0; i < enginePlayers.Length; i++) ReleasePlayer(i);
            RefreshCounts();
            audioAvailable = false;
            bank?.Dispose();
            bank = null;
            ReleaseSourceRoot();
        }

        private void ReleaseSourceRoot()
        {
            if (audioRoot != null)
            {
                audioRoot.SetActive(false);
                if (Application.isPlaying) Destroy(audioRoot);
                else DestroyImmediate(audioRoot);
            }
            audioRoot = null;
            for (int i = 0; i < oneShotSources.Length; i++) oneShotSources[i] = null;
            for (int i = 0; i < engineSources.Length; i++) engineSources[i] = null;
        }
    }
}
