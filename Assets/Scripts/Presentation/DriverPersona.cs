using System;
using System.Collections.Generic;
using UnityEngine;

namespace Presentation
{
    /// <summary>Stable identities for the neighborhood RC club's seven-person roster.</summary>
    public enum DriverPersonaId
    {
        Mica = 0,
        Beto = 1,
        Noa = 2,
        Luz = 3,
        Tavi = 4,
        Cata = 5,
        Vale = 6
    }

    /// <summary>The two 2x2 portrait sheets used by the seven-person catalog.</summary>
    public enum DriverPortraitSheet
    {
        Founders = 0,
        Neighbors = 1
    }

    /// <summary>Read-only identity and portrait data; selection authority can consume this later.</summary>
    public sealed class DriverPersonaDefinition
    {
        internal DriverPersonaDefinition(DriverPersonaId id, string callsign, string clubRole,
            DriverPortraitSheet portraitSheet, Rect portraitUv)
        {
            Id = id;
            Callsign = callsign;
            ClubRole = clubRole;
            PortraitSheet = portraitSheet;
            PortraitUv = portraitUv;
        }

        public DriverPersonaId Id { get; }
        public string Callsign { get; }
        public string ClubRole { get; }
        public DriverPortraitSheet PortraitSheet { get; }
        public Rect PortraitUv { get; }
    }

    /// <summary>Canonical persona order and deterministic Stage 8 slot assignment.</summary>
    public static class DriverPersonaCatalog
    {
        public const int DefaultPlayerSlotCount = 4;

        private static readonly DriverPersonaDefinition[] OrderedDefinitions =
        {
            new(DriverPersonaId.Mica, "MICA", "Pit-bench fabricator and setup lead.",
                DriverPortraitSheet.Founders, new Rect(0f, .5f, .5f, .5f)),
            new(DriverPersonaId.Beto, "BETO", "Club mechanic and safety scrutineer.",
                DriverPortraitSheet.Founders, new Rect(.5f, .5f, .5f, .5f)),
            new(DriverPersonaId.Noa, "NOA", "Radio marshal and audio tinkerer.",
                DriverPortraitSheet.Founders, new Rect(0f, 0f, .5f, .5f)),
            new(DriverPersonaId.Luz, "LUZ", "Course builder and night-race marshal.",
                DriverPortraitSheet.Founders, new Rect(.5f, 0f, .5f, .5f)),
            new(DriverPersonaId.Tavi, "TAVI", "Junior electronics tinkerer and battery runner.",
                DriverPortraitSheet.Neighbors, new Rect(0f, .5f, .5f, .5f)),
            new(DriverPersonaId.Cata, "CATA", "Family-pit host and race-day coordinator.",
                DriverPortraitSheet.Neighbors, new Rect(.5f, .5f, .5f, .5f)),
            new(DriverPersonaId.Vale, "VALE", "Body-shell painter and livery restorer.",
                DriverPortraitSheet.Neighbors, new Rect(0f, 0f, .5f, .5f))
        };

        private static readonly IReadOnlyList<DriverPersonaDefinition> DefinitionsView =
            Array.AsReadOnly(OrderedDefinitions);

        public static int Count => OrderedDefinitions.Length;
        public static IReadOnlyList<DriverPersonaDefinition> Definitions => DefinitionsView;

        public static bool IsDefined(DriverPersonaId id)
        {
            int value = (int)id;
            return value >= 0 && value < OrderedDefinitions.Length && OrderedDefinitions[value].Id == id;
        }

        public static DriverPersonaDefinition GetDefinition(DriverPersonaId id)
        {
            if (!IsDefined(id)) throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown driver persona.");
            return OrderedDefinitions[(int)id];
        }

        /// <summary>Stage 8 assigns the four canonical identities directly to local player slots 0-3.</summary>
        public static DriverPersonaDefinition GetForPlayerSlot(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= DefaultPlayerSlotCount)
                throw new ArgumentOutOfRangeException(nameof(playerIndex), playerIndex,
                    "Driver personas are available for local player slots 0-3.");
            return OrderedDefinitions[playerIndex];
        }
    }

    /// <summary>Two-sheet portrait source with one lazy Resources cache entry per atlas.</summary>
    public sealed class DriverPortraitAtlas
    {
        public const string FoundersResourcePath = "UI/DriverPortraitAtlas";
        public const string NeighborsResourcePath = "UI/DriverPortraitAtlasNeighbors";
        private static readonly DriverPortraitAtlas RuntimeInstance = new(true);
        private readonly Texture[] textures = new Texture[2];
        private readonly bool[] loadAttempted = new bool[2];
        private readonly bool resourcesBacked;

        private DriverPortraitAtlas(bool useResources)
        {
            resourcesBacked = useResources;
        }

        /// <summary>Creates an injectable two-sheet source for focused UI tests or alternate presentation.</summary>
        public DriverPortraitAtlas(Texture founders, Texture neighbors)
        {
            textures[(int)DriverPortraitSheet.Founders] = founders;
            textures[(int)DriverPortraitSheet.Neighbors] = neighbors;
        }

        public static DriverPortraitAtlas Runtime => RuntimeInstance;

        public Texture GetTexture(DriverPortraitSheet sheet)
        {
            int index = GetSheetIndex(sheet);
            if (resourcesBacked && !loadAttempted[index])
            {
                loadAttempted[index] = true;
                textures[index] = Resources.Load<Texture2D>(GetResourcePath(sheet));
            }
            return textures[index];
        }

        public static string GetResourcePath(DriverPortraitSheet sheet)
        {
            return sheet switch
            {
                DriverPortraitSheet.Founders => FoundersResourcePath,
                DriverPortraitSheet.Neighbors => NeighborsResourcePath,
                _ => throw new ArgumentOutOfRangeException(nameof(sheet), sheet, "Unknown portrait sheet.")
            };
        }

        private static int GetSheetIndex(DriverPortraitSheet sheet)
        {
            int index = (int)sheet;
            if (index < 0 || index >= 2)
                throw new ArgumentOutOfRangeException(nameof(sheet), sheet, "Unknown portrait sheet.");
            return index;
        }
    }
}
