using System;
using UnityEngine;

namespace Presentation
{
    public enum TrackAnchorKind { Grid, Recovery, Pickup }

    /// <summary>Indexed pose placed by a designer; the transform is the authoritative authored pose.</summary>
    [DisallowMultipleComponent]
    public sealed class TrackAuthoringAnchor : MonoBehaviour
    {
        [SerializeField] private TrackAnchorKind kind;
        [Min(0)] [SerializeField] private int index;

        public TrackAnchorKind Kind => kind;
        public int Index => index;

        public void Configure(TrackAnchorKind anchorKind, int anchorIndex)
        {
            if (!Enum.IsDefined(typeof(TrackAnchorKind), anchorKind))
                throw new ArgumentOutOfRangeException(nameof(anchorKind));
            if (anchorIndex < 0) throw new ArgumentOutOfRangeException(nameof(anchorIndex));
            kind = anchorKind;
            index = anchorIndex;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = kind switch
            {
                TrackAnchorKind.Grid => Color.cyan,
                TrackAnchorKind.Recovery => Color.green,
                _ => Color.yellow
            };
            Gizmos.DrawWireSphere(transform.position, kind == TrackAnchorKind.Pickup ? .5f : .35f);
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
        }
    }
}
