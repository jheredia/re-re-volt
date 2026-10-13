using System;
using UnityEngine;

namespace Presentation
{
    /// <summary>Explicit authored surface identity for presentation. It carries no grip or physics authority.</summary>
    public enum TrackSurfaceKind
    {
        Unclassified = 0,
        CuttingMat = 1,
        WorkbenchWood = 2,
        Foam = 3,
        Cardboard = 4,
        Metal = 5,
        Rubber = 6,
        VehicleContact = 7
    }

    [DisallowMultipleComponent]
    public sealed class TrackSurfaceMarker : MonoBehaviour
    {
        [SerializeField] private TrackSurfaceKind kind;

        public TrackSurfaceKind Kind => kind;

        public void Configure(TrackSurfaceKind surfaceKind)
        {
            if (!Enum.IsDefined(typeof(TrackSurfaceKind), surfaceKind) ||
                surfaceKind == TrackSurfaceKind.Unclassified)
                throw new ArgumentOutOfRangeException(nameof(surfaceKind));
            kind = surfaceKind;
        }

        public static bool TryResolve(Collider collider, out TrackSurfaceKind surfaceKind)
        {
            surfaceKind = TrackSurfaceKind.Unclassified;
            if (collider == null) return false;
            TrackSurfaceMarker marker = collider.GetComponentInParent<TrackSurfaceMarker>();
            if (marker == null || marker.kind == TrackSurfaceKind.Unclassified) return false;
            surfaceKind = marker.kind;
            return true;
        }
    }
}
