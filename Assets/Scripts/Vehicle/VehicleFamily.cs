using System;
using System.Collections.Generic;

namespace Vehicle
{
    /// <summary>Stable identifiers for the basic visual vehicle roster.</summary>
    public enum VehicleFamilyId
    {
        Touring = 0,
        Buggy = 1,
        RcTruck = 2,
        Drift = 3
    }

    /// <summary>Read-only public data used by selection and presentation without depending on prototype geometry.</summary>
    public sealed class VehicleFamilyDefinition
    {
        internal VehicleFamilyDefinition(VehicleFamilyId id, string publicLabel)
        {
            Id = id;
            PublicLabel = publicLabel;
        }

        public VehicleFamilyId Id { get; }
        public string PublicLabel { get; }
    }

    /// <summary>Canonical ordering and wrapping rules for the four basic vehicle families.</summary>
    public static class VehicleFamilyCatalog
    {
        private static readonly VehicleFamilyDefinition[] OrderedDefinitions =
        {
            new(VehicleFamilyId.Touring, "TOURING"),
            new(VehicleFamilyId.Buggy, "BUGGY"),
            new(VehicleFamilyId.RcTruck, "RC TRUCK"),
            new(VehicleFamilyId.Drift, "DRIFT")
        };

        private static readonly IReadOnlyList<VehicleFamilyDefinition> DefinitionsView =
            Array.AsReadOnly(OrderedDefinitions);

        public static VehicleFamilyId DefaultId => VehicleFamilyId.Touring;
        public static int Count => OrderedDefinitions.Length;
        public static IReadOnlyList<VehicleFamilyDefinition> Definitions => DefinitionsView;

        public static bool IsDefined(VehicleFamilyId id)
        {
            int value = (int)id;
            return value >= 0 && value < OrderedDefinitions.Length && OrderedDefinitions[value].Id == id;
        }

        public static VehicleFamilyDefinition GetDefinition(VehicleFamilyId id)
        {
            if (!IsDefined(id)) throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown vehicle family.");
            return OrderedDefinitions[(int)id];
        }

        public static string GetPublicLabel(VehicleFamilyId id) =>
            IsDefined(id) ? OrderedDefinitions[(int)id].PublicLabel : "VEHICLE ?";

        /// <summary>
        /// Applies an arbitrary signed offset in canonical roster order. Unknown current values start at Touring.
        /// </summary>
        public static VehicleFamilyId Wrap(VehicleFamilyId current, int offset)
        {
            long start = IsDefined(current) ? (int)current : (int)DefaultId;
            long wrapped = (start + offset) % OrderedDefinitions.Length;
            if (wrapped < 0) wrapped += OrderedDefinitions.Length;
            return OrderedDefinitions[(int)wrapped].Id;
        }
    }
}
