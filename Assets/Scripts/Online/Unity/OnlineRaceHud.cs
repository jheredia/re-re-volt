using System;
using System.Collections.Generic;
using System.Globalization;
using Core;
using Presentation;
using ReReVolt.Online.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Weapons;

namespace ReReVolt.Online.Unity
{
    /// <summary>
    /// Peer-local, read-only race information. Host labels use the exact published
    /// snapshot; client labels use only snapshots accepted by the receiver.
    /// </summary>
    public sealed class OnlineRaceHud : MonoBehaviour
    {
        private sealed class LocalLabel
        {
            public uint RacerId;
            public int Slot;
            public TextMeshProUGUI Text;
            public string LastText;
        }

        private readonly List<LocalLabel> labels = new List<LocalLabel>(4);
        private OnlineRaceMessageRouting routing;
        private GameObject canvasRoot;
        private ulong epoch;
        private uint round;
        private int totalLaps;
        private int totalRacers;

        public int LocalLabelCount => labels.Count;

        /// <summary>Read-only diagnostic text for a local racer; never exposes UI or race authority.</summary>
        public bool TryGetLocalStatus(uint racerId, out string status)
        {
            foreach (LocalLabel label in labels)
                if (label.RacerId == racerId)
                {
                    status = label.Text != null ? label.Text.text : null;
                    return status != null;
                }
            status = null;
            return false;
        }

        public void Initialize(LobbySnapshot lockedRoster, string localPeer)
        {
            if (canvasRoot != null) throw new InvalidOperationException("The online HUD already owns a round.");
            if (lockedRoster == null || lockedRoster.Racers == null || lockedRoster.Round == 0 ||
                string.IsNullOrWhiteSpace(localPeer) ||
                !TrackCatalog.TryGet(lockedRoster.TrackId, out TrackDefinition track) || !track.IsAvailable)
                throw new ArgumentException("A locked roster, authenticated peer, and available track are required.");

            var owned = new List<LobbyRacer>(4);
            foreach (LobbyRacer racer in lockedRoster.Racers)
                if (racer != null && !racer.Bot && string.Equals(racer.Peer, localPeer, StringComparison.Ordinal))
                    owned.Add(racer);
            owned.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            if (owned.Count == 0 || owned.Count > LocalPlayerRules.MaximumPlayers)
                throw new ArgumentException("This peer needs one to four local human racers.", nameof(localPeer));

            epoch = lockedRoster.Epoch;
            round = lockedRoster.Round;
            totalLaps = track.LapCount;
            totalRacers = lockedRoster.Racers.Length;
            try
            {
                canvasRoot = new GameObject("Online Race HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasRoot.transform.SetParent(transform, false);
                Canvas canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 350;
                CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                for (int i = 0; i < owned.Count; i++) CreateLabel(owned[i], i, owned.Count);
            }
            catch
            {
                Shutdown();
                throw;
            }
        }

        public void Bind(OnlineRaceMessageRouting messageRouting)
        {
            if (canvasRoot == null || routing != null || messageRouting == null)
                throw new InvalidOperationException("Initialize an unbound HUD before connecting a race router.");
            routing = messageRouting;
            if (routing.IsHost) routing.HostSnapshotPublished += ApplySnapshot;
            else
            {
                routing.SnapshotAccepted += ApplySnapshot;
                if (routing.LatestSnapshot != null) ApplySnapshot(routing.LatestSnapshot);
            }
        }

        /// <summary>Render only a host-published or contract-accepted snapshot; no gameplay state is written.</summary>
        public void ApplySnapshot(DrivingWorldSnapshot snapshot)
        {
            if (canvasRoot == null || snapshot == null || snapshot.Racers == null ||
                snapshot.Epoch != epoch || snapshot.Round != round) return;
            foreach (LocalLabel label in labels)
            {
                for (int i = 0; i < snapshot.Racers.Length; i++)
                {
                    DrivingRacerState state = snapshot.Racers[i];
                    if (state.RacerId != label.RacerId) continue;
                    string next = Format(label.Slot, state, snapshot);
                    if (!string.Equals(next, label.LastText, StringComparison.Ordinal))
                    {
                        label.Text.text = next;
                        label.LastText = next;
                    }
                    break;
                }
            }
        }

        private void CreateLabel(LobbyRacer racer, int displayIndex, int localCount)
        {
            Viewport viewport = LocalPlayerRules.GetViewport(localCount, displayIndex);
            GameObject viewportObject = new GameObject("Online Viewport " + (displayIndex + 1), typeof(RectTransform));
            viewportObject.transform.SetParent(canvasRoot.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(viewport.X, viewport.Y);
            viewportRect.anchorMax = new Vector2(viewport.X + viewport.Width, viewport.Y + viewport.Height);
            viewportRect.offsetMin = viewportRect.offsetMax = Vector2.zero;

            GameObject panelObject = new GameObject("P" + (racer.Slot + 1) + " Online Status",
                typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(viewportObject.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(14, -14);
            panelRect.sizeDelta = localCount >= 3 ? new Vector2(318, 74) :
                localCount == 2 ? new Vector2(370, 84) : new Vector2(420, 88);
            Image background = panelObject.GetComponent<Image>();
            background.color = new Color(.025f, .055f, .065f, .88f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject("Status Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 6);
            textRect.offsetMax = new Vector2(-10, -5);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = localCount >= 3 ? 18 : 22;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.text = "P" + (racer.Slot + 1) + "   GET READY";
            labels.Add(new LocalLabel { RacerId = racer.Id, Slot = racer.Slot, Text = text });
        }

        private string Format(int slot, DrivingRacerState state, DrivingWorldSnapshot snapshot)
        {
            string player = "P" + (slot + 1);
            if (snapshot.Phase == DrivingPhase.Countdown)
                return player + "   START IN " + snapshot.Countdown + "\nGET READY";
            if (state.FinishPosition > 0)
                return player + "   FINISHED #" + state.FinishPosition +
                    "\n" + TimeLabel(state.FinishTime) + "  •  LOBBY REMATCH";
            int lap = Mathf.Clamp(state.CompletedLaps + 1, 1, totalLaps);
            string item = state.HeldWeapon == WeaponKind.None ? "NO ITEM" :
                state.HeldWeapon == WeaponKind.Bolt ? "BOLT" : "OIL";
            return player + "   LAP " + lap + "/" + totalLaps + "   POS " + state.PositionInRace + "/" + totalRacers +
                "\n" + TimeLabel(snapshot.Elapsed) + "  •  " + item;
        }

        private static string TimeLabel(float seconds)
        {
            float safe = Mathf.Max(0, seconds);
            int minutes = Mathf.FloorToInt(safe / 60f);
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                (safe - minutes * 60f).ToString("00.0", CultureInfo.InvariantCulture);
        }

        public void Shutdown()
        {
            if (routing != null)
            {
                routing.HostSnapshotPublished -= ApplySnapshot;
                routing.SnapshotAccepted -= ApplySnapshot;
            }
            routing = null;
            labels.Clear();
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(false);
                if (Application.isPlaying) Destroy(canvasRoot); else DestroyImmediate(canvasRoot);
            }
            canvasRoot = null;
            epoch = 0;
            round = 0;
            totalLaps = totalRacers = 0;
        }

        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
