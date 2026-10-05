using System;
using Core;
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
        private readonly AudioSource[] engineSources = new AudioSource[MaximumEngineVoices];
        private readonly LocalPlayer[] enginePlayers = new LocalPlayer[MaximumEngineVoices];
        private readonly VehicleImpactAudioEmitter[] impactEmitters =
            new VehicleImpactAudioEmitter[MaximumEngineVoices];
        private readonly bool[] ownsImpactEmitter = new bool[MaximumEngineVoices];
        private readonly float[] nextImpactAt = new float[MaximumEngineVoices];

        private RaceManager race;
        private VehicleManager vehicles;
        private WeaponManager weapons;
        private ProceduralAudioBank bank;
        private GameObject audioRoot;
        private bool configured;
        private bool subscribed;
        private bool audioAvailable;

        public bool IsConfigured => configured;
        public bool AudioAvailable => audioAvailable;
        public int GeneratedClipCount => bank?.ClipCount ?? 0;
        public int OneShotVoiceCapacity => oneShotSources.Length;
        public int EngineVoiceCapacity => engineSources.Length;
        public int ActiveEngineVoiceCount { get; private set; }
        public int ImpactEmitterCount { get; private set; }
        public int AcceptedCueCount { get; private set; }
        public int DroppedCueCount { get; private set; }
        public AudioCueId LastCue { get; private set; } = AudioCueId.Count;

        public void Configure(RaceManager raceManager, VehicleManager roster, WeaponManager combat = null)
        {
            if (configured) throw new InvalidOperationException("Audio presentation is already configured.");
            if (raceManager == null) throw new ArgumentNullException(nameof(raceManager));
            if (roster == null) throw new ArgumentNullException(nameof(roster));

            race = raceManager;
            vehicles = roster;
            weapons = combat;
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
            vehicles.PlayerJoined += OnPlayerJoined;
            vehicles.PlayerLeft += OnPlayerLeft;
            vehicles.HandlingModelChanged += OnSelectionChanged;
            vehicles.VehicleFamilyChanged += OnSelectionChanged;
            if (weapons != null)
            {
                weapons.WeaponCollected += OnWeaponCollected;
                weapons.WeaponFired += OnWeaponFired;
                weapons.WeaponHit += OnWeaponHit;
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
            }
            if (vehicles != null)
            {
                vehicles.PlayerJoined -= OnPlayerJoined;
                vehicles.PlayerLeft -= OnPlayerLeft;
                vehicles.HandlingModelChanged -= OnSelectionChanged;
                vehicles.VehicleFamilyChanged -= OnSelectionChanged;
            }
            if (weapons != null)
            {
                weapons.WeaponCollected -= OnWeaponCollected;
                weapons.WeaponFired -= OnWeaponFired;
                weapons.WeaponHit -= OnWeaponHit;
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
                if (emitter == null) emitter = player.Vehicle.gameObject.AddComponent<VehicleImpactAudioEmitter>();
                emitter.Configure(slot, minimumImpactImpulse);
                emitter.Impacted += OnVehicleImpact;
                impactEmitters[slot] = emitter;
            }
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
                emitter.Impacted -= OnVehicleImpact;
                if (ownsImpactEmitter[slot])
                {
                    if (Application.isPlaying) Destroy(emitter);
                    else DestroyImmediate(emitter);
                }
            }
            impactEmitters[slot] = null;
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
                emitter.Impacted -= OnVehicleImpact;
                emitter.Impacted += OnVehicleImpact;
            }
        }

        private void Update()
        {
            if (!audioAvailable) return;
            float mixScale = ActiveEngineVoiceCount switch
            {
                4 => .5f,
                3 => .58f,
                2 => .7f,
                _ => 1f
            };
            float response = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            for (int i = 0; i < enginePlayers.Length; i++)
            {
                LocalPlayer player = enginePlayers[i];
                AudioSource source = engineSources[i];
                if (player == null || player.Vehicle == null || source == null) continue;
                float speed = Mathf.Clamp01(player.Vehicle.CurrentSpeed / referenceTopSpeedKph);
                float throttle = Mathf.Abs(player.Vehicle.ThrottleInput);
                float targetVolume = masterVolume * engineVolume * mixScale * (.32f + speed * .5f + throttle * .18f);
                if (!player.IsConnected) targetVolume *= .25f;
                source.volume = Mathf.Lerp(source.volume, targetVolume, response);
                source.pitch = .72f + speed * 1.08f + throttle * .12f;
                if (!source.isPlaying && source.clip != null) source.Play();
            }
        }

        private void OnCountdownChanged(int number)
        {
            if (number > 0) TryPlayCue(AudioCueId.Countdown);
            else if (number == 0) TryPlayCue(AudioCueId.Go);
        }

        private void OnRaceCompleted(System.Collections.Generic.IReadOnlyList<RacerState> standings) =>
            TryPlayCue(AudioCueId.Finish);

        private void OnSelectionChanged(LocalPlayer player) => TryPlayCue(AudioCueId.UiConfirm);
        private void OnWeaponCollected(LocalPlayer player, WeaponKind kind) => TryPlayCue(AudioCueId.Pickup);
        private void OnWeaponFired(LocalPlayer player, WeaponKind kind) => TryPlayCue(AudioCueId.WeaponFire);
        private void OnWeaponHit(LocalPlayer player, WeaponKind kind) => TryPlayImpactFeedback(player.PlayerIndex,
            fullImpactImpulse);
        private void OnVehicleImpact(int playerIndex, float impulse) => TryPlayImpactFeedback(playerIndex, impulse);

        /// <summary>Bounded presentation seam used by collision and combat adapters.</summary>
        public bool TryPlayImpactFeedback(int playerIndex, float impulse)
        {
            if (playerIndex < 0 || playerIndex >= nextImpactAt.Length || impulse < minimumImpactImpulse)
                return false;
            float now = Time.unscaledTime;
            if (now < nextImpactAt[playerIndex])
            {
                DroppedCueCount++;
                return false;
            }
            float intensity = Mathf.Clamp01(impulse / Mathf.Max(.01f, fullImpactImpulse));
            if (!TryPlayCue(AudioCueId.Impact, Mathf.Lerp(.35f, 1f, intensity), Mathf.Lerp(.82f, 1.12f, intensity)))
                return false;
            nextImpactAt[playerIndex] = now + impactCooldown;
            return true;
        }

        /// <summary>Plays through the fixed one-shot pool; requests are dropped when all voices are occupied.</summary>
        public bool TryPlayCue(AudioCueId cue, float volumeScale = 1f, float pitch = 1f)
        {
            int cueIndex = (int)cue;
            if (!isActiveAndEnabled || !audioAvailable || cueIndex <= (int)AudioCueId.Engine ||
                cueIndex >= (int)AudioCueId.Count)
                return false;
            AudioClip clip = bank.Get(cue);
            if (clip == null) return false;
            float now = Time.unscaledTime;
            int selectedVoice = -1;
            for (int i = 0; i < oneShotSources.Length; i++)
            {
                if (oneShotSources[i] != null && now >= oneShotBusyUntil[i])
                {
                    selectedVoice = i;
                    break;
                }
            }
            if (selectedVoice < 0 && IsPriorityCue(cue))
            {
                float earliestRelease = float.PositiveInfinity;
                for (int i = 0; i < oneShotSources.Length; i++)
                    if (oneShotSources[i] != null && oneShotBusyUntil[i] < earliestRelease)
                    {
                        earliestRelease = oneShotBusyUntil[i];
                        selectedVoice = i;
                    }
            }
            if (selectedVoice >= 0)
            {
                AudioSource source = oneShotSources[selectedVoice];
                source.Stop();
                source.clip = clip;
                source.pitch = Mathf.Clamp(pitch, .25f, 3f);
                source.volume = masterVolume * cueVolume * Mathf.Clamp01(volumeScale);
                source.Play();
                oneShotBusyUntil[selectedVoice] = now + clip.length / Mathf.Abs(source.pitch);
                LastCue = cue;
                AcceptedCueCount++;
                return true;
            }
            DroppedCueCount++;
            return false;
        }

        private static bool IsPriorityCue(AudioCueId cue) =>
            cue == AudioCueId.Countdown || cue == AudioCueId.Go || cue == AudioCueId.Finish;

        private void OnDisable()
        {
            Unsubscribe();
            for (int i = 0; i < impactEmitters.Length; i++)
                if (impactEmitters[i] != null) impactEmitters[i].Impacted -= OnVehicleImpact;
            for (int i = 0; i < oneShotSources.Length; i++)
                if (oneShotSources[i] != null)
                {
                    oneShotSources[i].Stop();
                    oneShotBusyUntil[i] = 0f;
                }
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
