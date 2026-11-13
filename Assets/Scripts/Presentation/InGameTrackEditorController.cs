using System;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.InputSystem;
using Vehicle;

namespace Presentation
{
    /// <summary>
    /// First player-build editing workspace. It edits local drafts only and prepares
    /// validated revisions for the existing local race composition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InGameTrackEditorController : MonoBehaviour
    {
        private readonly Stack<(string id, TrackRoadSurface surface)> redo = new();
        private readonly List<(string id, string name)> saved = new();
        private RaceManager race;
        private VehicleManager vehicles;
        private Stage3RacePresentation presentation;
        private Func<InGameTrackDraft, bool> prepareTestDrive;
        private Action catalogChanged;
        private InGameTrackDraftStore store;
        private InGameTrackDraft draft;
        private string nameBuffer;
        private string message = "Choose a road piece to snap it to the open end.";
        private TrackRoadSurface selectedSurface;
        private int selectedPropIndex;
        private bool placingProp;
        private Vector2 paletteScroll;
        private Vector2 libraryScroll;
        private Vector2 mapPan;
        private float mapZoom = 1f;
        private bool open;
        private bool dirty;
        private bool confirmClose;

        public bool IsOpen => open;
        public string CurrentDraftId => draft?.LocalId;

        public void Configure(RaceManager raceManager, VehicleManager vehicleManager,
            Stage3RacePresentation racePresentation, Func<InGameTrackDraft, bool> testDriveCommand,
            Action onCatalogChanged)
        {
            race = raceManager ?? throw new ArgumentNullException(nameof(raceManager));
            vehicles = vehicleManager ?? throw new ArgumentNullException(nameof(vehicleManager));
            presentation = racePresentation ?? throw new ArgumentNullException(nameof(racePresentation));
            prepareTestDrive = testDriveCommand ?? throw new ArgumentNullException(nameof(testDriveCommand));
            catalogChanged = onCatalogChanged ?? throw new ArgumentNullException(nameof(onCatalogChanged));
            store = new InGameTrackDraftStore();
        }

        private bool CanOpen => race != null && vehicles != null && presentation != null &&
                                race.CurrentState == RaceManager.RaceState.Idle &&
                                vehicles.Players.Count == 0 && presentation.LobbyVisible &&
                                presentation.CurrentPage == Stage3RacePresentation.DemoFlowPage.Title;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || store == null) return;
            if (!open)
            {
                if (CanOpen && keyboard.f9Key.wasPressedThisFrame) Open();
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame || keyboard.f9Key.wasPressedThisFrame)
                TryClose();
            bool command = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed ||
                           keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed;
            if (command && keyboard.zKey.wasPressedThisFrame) Undo();
            if (command && keyboard.yKey.wasPressedThisFrame) Redo();
        }

        private void OnDisable()
        {
            if (!open) return;
            open = false;
            if (presentation?.PresentationCanvas != null) presentation.PresentationCanvas.enabled = true;
        }

        public bool Open()
        {
            if (!CanOpen || open) return false;
            draft ??= InGameTrackDraft.Create();
            nameBuffer = draft.Name;
            RefreshLibrary();
            open = true;
            confirmClose = false;
            if (presentation.PresentationCanvas != null) presentation.PresentationCanvas.enabled = false;
            return true;
        }

        private void TryClose()
        {
            if (dirty && !confirmClose)
            {
                confirmClose = true;
                message = "Unsaved changes. Choose Save Draft or press Close again to discard them.";
                return;
            }
            if (dirty)
            {
                draft = null;
                redo.Clear();
                dirty = false;
            }
            open = false;
            confirmClose = false;
            if (presentation.PresentationCanvas != null) presentation.PresentationCanvas.enabled = true;
        }

        private void OnGUI()
        {
            if (store == null) return;
            if (!open)
            {
                if (CanOpen && GUI.Button(new Rect(Screen.width - 216f, 20f, 190f, 44f),
                        "CREATE TRACK  [F9]")) Open();
                return;
            }

            Color before = GUI.color;
            GUI.color = new Color(.035f, .05f, .055f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = before;

            float sideWidth = Mathf.Clamp(Screen.width * .28f, 300f, 410f);
            Rect map = new(24f, 108f, Screen.width - sideWidth - 60f, Screen.height - 174f);
            Rect side = new(Screen.width - sideWidth - 20f, 108f, sideWidth, Screen.height - 174f);
            GUI.Label(new Rect(28f, 20f, Screen.width - 300f, 42f),
                "RC CLUB TRACK LAB  /  " + draft.Name);
            GUI.Label(new Rect(28f, 60f, Screen.width - 300f, 34f),
                $"{draft.Layout.Route.Count} PIECES  •  LEVEL {draft.Layout.OpenEnd.Level}  •  " +
                (draft.Layout.IsClosed ? "LOOP CLOSED" : "OPEN END"));
            if (GUI.Button(new Rect(Screen.width - 196f, 22f, 160f, 46f),
                    confirmClose ? "DISCARD & CLOSE" : "CLOSE  [F9]")) TryClose();

            DrawMap(map);
            DrawControls(side);
            GUI.Label(new Rect(28f, Screen.height - 56f, Screen.width - 56f, 45f), message);
        }

        private void DrawControls(Rect side)
        {
            GUI.Box(side, string.Empty);
            float x = side.x + 14f;
            float width = side.width - 28f;
            float y = side.y + 14f;
            GUI.Label(new Rect(x, y, width, 25f), "TRACK NAME");
            y += 26f;
            string updatedName = GUI.TextField(new Rect(x, y, width, 34f), nameBuffer ?? string.Empty, 48);
            if (updatedName != nameBuffer)
            {
                nameBuffer = updatedName;
                dirty = true;
                confirmClose = false;
            }
            y += 42f;
            if (GUI.Button(new Rect(x, y, width * .48f, 35f), "SAVE DRAFT")) Save();
            if (GUI.Button(new Rect(x + width * .52f, y, width * .48f, 35f), "NEW DRAFT")) NewDraft();
            y += 43f;
            if (GUI.Button(new Rect(x, y, width * .48f, 35f), "UNDO  [CTRL Z]")) Undo();
            if (GUI.Button(new Rect(x + width * .52f, y, width * .48f, 35f), "REDO  [CTRL Y]")) Redo();
            y += 43f;
            if (GUI.Button(new Rect(x, y, width, 34f), "THEME: " + draft.ThemeId.ToUpperInvariant()))
            {
                draft.SetTheme(draft.ThemeId == "workshop" ? "patio" :
                    draft.ThemeId == "patio" ? "market" : "workshop");
                dirty = true;
            }
            y += 40f;
            if (GUI.Button(new Rect(x, y, width, 34f), "ROAD SURFACE: " + selectedSurface))
                selectedSurface = (TrackRoadSurface)(((int)selectedSurface + 1) % 3);
            y += 42f;
            if (GUI.Button(new Rect(x, y, width, 34f),
                    (placingProp ? "PROP PLACEMENT ON" : "PROP PLACEMENT OFF") + " — CLICK ROAD"))
                placingProp = !placingProp;
            y += 39f;
            if (GUI.Button(new Rect(x, y, width * .66f, 32f),
                    "PROP: " + InGameTrackPropKit.All[selectedPropIndex].Id.ToUpperInvariant()))
                selectedPropIndex = (selectedPropIndex + 1) % InGameTrackPropKit.All.Count;
            if (GUI.Button(new Rect(x + width * .68f, y, width * .32f, 32f), "REMOVE"))
            {
                if (draft.TryRemoveLastProp())
                { dirty = true; message = "Last prop removed."; }
            }
            y += 40f;

            GUI.Label(new Rect(x, y, width, 25f), "SNAP ROAD PIECES TO THE OPEN END");
            y += 27f;
            float paletteHeight = Mathf.Max(96f, side.height * .22f);
            Rect scroll = new(x, y, width, paletteHeight);
            paletteScroll = GUI.BeginScrollView(scroll, paletteScroll,
                new Rect(0f, 0f, width - 18f, (InGameTrackKit.All.Count - 1) * 31f));
            for (int i = 1; i < InGameTrackKit.All.Count; i++)
            {
                TrackPieceSpec piece = InGameTrackKit.All[i];
                if (GUI.Button(new Rect(0f, (i - 1) * 31f, width - 20f, 29f),
                        piece.Id.Replace('-', ' ').ToUpperInvariant())) Append(piece.Id);
            }
            GUI.EndScrollView();
            y += paletteHeight + 10f;
            GUI.Label(new Rect(x, y, width, 25f), "LOCAL DRAFTS");
            y += 27f;
            float libraryHeight = Mathf.Max(45f, side.yMax - y - 56f);
            libraryScroll = GUI.BeginScrollView(new Rect(x, y, width, libraryHeight), libraryScroll,
                new Rect(0f, 0f, width - 18f, saved.Count * 32f));
            for (int i = 0; i < saved.Count; i++)
            {
                (string id, string name) entry = saved[i];
                if (GUI.Button(new Rect(0f, i * 32f, width - 20f, 30f), entry.name)) Load(entry.id);
            }
            GUI.EndScrollView();
            if (GUI.Button(new Rect(x, side.yMax - 43f, width, 32f), "PREPARE LOCAL TEST DRIVE"))
                PrepareTestDrive();
        }

        private void DrawMap(Rect area)
        {
            GUI.Box(area, string.Empty);
            IReadOnlyList<TrackPiecePlacement> route = draft.Layout.Route;
            int minX = 0, maxX = 0, minZ = 0, maxZ = 0;
            foreach (TrackPiecePlacement piece in route)
            {
                minX = Mathf.Min(minX, Mathf.Min(piece.Entry.X, piece.Exit.X));
                maxX = Mathf.Max(maxX, Mathf.Max(piece.Entry.X, piece.Exit.X));
                minZ = Mathf.Min(minZ, Mathf.Min(piece.Entry.Z, piece.Exit.Z));
                maxZ = Mathf.Max(maxZ, Mathf.Max(piece.Entry.Z, piece.Exit.Z));
            }
            float spanX = Mathf.Max(22f, maxX - minX + 12f);
            float spanZ = Mathf.Max(22f, maxZ - minZ + 12f);
            float scale = Mathf.Min((area.width - 60f) / spanX, (area.height - 60f) / spanZ) * mapZoom;
            float centerX = area.center.x - (minX + maxX) * .5f * scale + mapPan.x;
            float centerY = area.center.y + (minZ + maxZ) * .5f * scale + mapPan.y;
            for (int i = 0; i < route.Count; i++)
            {
                TrackPiecePlacement piece = route[i];
                Vector2 a = new(centerX + piece.Entry.X * scale, centerY - piece.Entry.Z * scale);
                Vector2 b = new(centerX + piece.Exit.X * scale, centerY - piece.Exit.Z * scale);
                Color color = piece.Surface switch
                {
                    TrackRoadSurface.Slick => new Color(.55f, .78f, 1f),
                    TrackRoadSurface.HighGrip => new Color(.85f, .95f, .5f),
                    _ => new Color(.98f, .74f, .23f)
                };
                if (piece.Exit.Level > 0 || piece.Entry.Level > 0)
                    color = Color.Lerp(color, Color.magenta, .3f);
                DrawLine(a, b, Mathf.Clamp(scale * 1.4f, 8f, 28f), color);
                GUI.Label(new Rect(b.x - 13f, b.y - 13f, 35f, 25f), i.ToString());
            }
            foreach (InGameTrackProp prop in draft.Props)
            {
                Vector3 world = InGameTrackRouteValidator.PropPosition(route[prop.RoutePiece], prop);
                Vector2 point = new(centerX + world.x / InGameTrackRuntimeBuilder.CellSize * scale,
                    centerY - world.z / InGameTrackRuntimeBuilder.CellSize * scale);
                GUI.color = prop.Spec.Collides ? new Color(1f, .36f, .25f) : Color.white;
                GUI.DrawTexture(new Rect(point.x - 5f, point.y - 5f, 10f, 10f),
                    Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            TrackSocket end = draft.Layout.OpenEnd;
            Vector2 openPoint = new(centerX + end.X * scale, centerY - end.Z * scale);
            GUI.color = draft.Layout.IsClosed ? Color.green : Color.cyan;
            GUI.DrawTexture(new Rect(openPoint.x - 8f, openPoint.y - 8f, 16f, 16f),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(area.x + 18f, area.y + 12f, area.width - 35f, 28f),
                "TOP-DOWN ROUTE  •  COLORS = SURFACE  •  PURPLE = RAISED ROAD");
            IReadOnlyList<string> issues = draft.Layout.ValidateTopology();
            GUI.Label(new Rect(area.x + 18f, area.yMax - 36f, area.width - 35f, 28f),
                issues.Count == 0 ? "ROUTE TOPOLOGY CLOSED — PHYSICAL CHECKS STILL REQUIRED" : issues[0]);

            Event input = Event.current;
            if (input.type == EventType.ScrollWheel && area.Contains(input.mousePosition))
            { mapZoom = Mathf.Clamp(mapZoom * (input.delta.y > 0 ? .9f : 1.1f), .5f, 3f); input.Use(); }
            if (input.type == EventType.MouseDrag && input.button == 2 && area.Contains(input.mousePosition))
            { mapPan += input.delta; input.Use(); }
            if (placingProp && input.type == EventType.MouseDown && input.button == 0 &&
                area.Contains(input.mousePosition))
            {
                PlacePropOnMap(input.mousePosition, centerX, centerY, scale);
                input.Use();
            }
        }

        private void PlacePropOnMap(Vector2 mouse, float centerX, float centerY, float scale)
        {
            IReadOnlyList<TrackPiecePlacement> route = draft.Layout.Route;
            int bestPiece = -1;
            float bestU = 0f;
            float bestDistance = float.PositiveInfinity;
            Vector2 bestPoint = default;
            for (int pieceIndex = 0; pieceIndex < route.Count; pieceIndex++)
            {
                for (int sample = 0; sample <= 20; sample++)
                {
                    float u = sample / 20f;
                    Vector3 world = InGameTrackRuntimeBuilder.Sample(route[pieceIndex], u);
                    Vector2 point = new(centerX + world.x / InGameTrackRuntimeBuilder.CellSize * scale,
                        centerY - world.z / InGameTrackRuntimeBuilder.CellSize * scale);
                    float distance = (mouse - point).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestPiece = pieceIndex;
                    bestU = u;
                    bestPoint = point;
                }
            }
            float halfWidthPixels = InGameTrackRuntimeBuilder.RoadWidth * .5f /
                                    InGameTrackRuntimeBuilder.CellSize * scale;
            if (bestPiece < 0 || bestDistance > halfWidthPixels * halfWidthPixels)
            { message = "Click a road surface to place this prop."; return; }
            Vector3 tangent = InGameTrackRuntimeBuilder.Tangent(route[bestPiece], bestU).normalized;
            Vector2 right = new(tangent.z, tangent.x);
            float v = Mathf.Clamp(Vector2.Dot(mouse - bestPoint, right) / halfWidthPixels, -1f, 1f);
            if (!draft.TryAddProp(InGameTrackPropKit.All[selectedPropIndex].Id,
                    bestPiece, bestU, v, 0f, out string error))
            { message = error; return; }
            dirty = true;
            message = "Prop placed. Test Drive will check route clearance.";
        }

        private static void DrawLine(Vector2 a, Vector2 b, float thickness, Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            Color before = GUI.color;
            GUI.color = color;
            Vector2 delta = b - a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * .5f, delta.magnitude, thickness),
                Texture2D.whiteTexture);
            GUI.matrix = matrix;
            GUI.color = before;
        }

        private void Append(string pieceId)
        {
            if (!draft.Layout.TryAppend(pieceId, selectedSurface, out string error))
            { message = error; return; }
            redo.Clear();
            dirty = true;
            confirmClose = false;
            message = "Piece snapped to the open end. Close the loop to prepare a Test Drive.";
        }

        private void Undo()
        {
            IReadOnlyList<TrackPiecePlacement> route = draft.Layout.Route;
            if (route.Count <= 1) return;
            foreach (InGameTrackProp prop in draft.Props)
                if (prop.RoutePiece == route.Count - 1)
                { message = "Remove props from the last road piece before undoing it."; return; }
            TrackPiecePlacement last = route[route.Count - 1];
            if (!draft.Layout.TryRemoveLast()) return;
            redo.Push((last.Spec.Id, last.Surface));
            dirty = true;
            message = "Removed the last road piece.";
        }

        private void Redo()
        {
            if (redo.Count == 0) return;
            (string id, TrackRoadSurface surface) next = redo.Peek();
            if (!draft.Layout.TryAppend(next.id, next.surface, out string error))
            { message = error; return; }
            redo.Pop();
            dirty = true;
            message = "Road piece restored.";
        }

        private bool Save()
        {
            try { draft.Rename(nameBuffer); }
            catch (ArgumentException exception) { message = exception.Message; return false; }
            if (!store.TrySave(draft, out string error)) { message = error; return false; }
            dirty = false;
            confirmClose = false;
            message = "Draft saved locally. Close the loop to prepare a local Test Drive.";
            RefreshLibrary();
            catalogChanged();
            return true;
        }

        private void PrepareTestDrive()
        {
            IReadOnlyList<string> errors = InGameTrackRouteValidator.Validate(draft);
            if (errors.Count != 0) { message = errors[0]; return; }
            if (!Save()) return;
            if (!prepareTestDrive(draft))
            { message = "Could not prepare Test Drive. Return to the title and try again."; return; }
            open = false;
            confirmClose = false;
            if (presentation.PresentationCanvas != null) presentation.PresentationCanvas.enabled = true;
            message = "Test Drive ready. Start the race from Setup after the track loads.";
        }

        private void NewDraft()
        {
            if (dirty && !confirmClose)
            { confirmClose = true; message = "Unsaved changes. Choose New Draft again to discard them."; return; }
            draft = InGameTrackDraft.Create();
            nameBuffer = draft.Name;
            redo.Clear();
            dirty = false;
            confirmClose = false;
            mapZoom = 1f;
            mapPan = Vector2.zero;
            message = "New track draft. Choose a piece from the road palette.";
        }

        private void Load(string id)
        {
            if (dirty && !confirmClose)
            { confirmClose = true; message = "Unsaved changes. Choose the draft again to discard them."; return; }
            if (!store.TryLoad(id, out InGameTrackDraft loaded, out string error))
            { message = error; return; }
            draft = loaded;
            nameBuffer = draft.Name;
            redo.Clear();
            dirty = false;
            confirmClose = false;
            mapPan = Vector2.zero;
            mapZoom = 1f;
            message = "Local track draft loaded.";
        }

        private void RefreshLibrary()
        {
            saved.Clear();
            foreach (string id in store.ListLocalIds())
                if (store.TryLoad(id, out InGameTrackDraft loaded, out _))
                    saved.Add((id, loaded.Name));
        }
    }
}
