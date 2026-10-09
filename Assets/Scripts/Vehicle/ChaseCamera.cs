using UnityEngine;

namespace Vehicle
{
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -5f);
        [SerializeField] private float followSharpness = 9f;
        private Transform target;
        private Vector3 presentationPositionOffset;
        private Vector3 presentationLookOffset;
        private float baselineFieldOfView;
        public Camera Camera { get; private set; }

        private void Awake()
        {
            Camera = GetComponent<Camera>();
            baselineFieldOfView = Camera.fieldOfView;
        }

        public void Initialize(Transform vehicle)
        {
            target = vehicle;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = DesiredPosition();
            LookAtTarget();
        }

        public float BaselineFieldOfView => baselineFieldOfView;

        public void SetPresentationResponse(Vector3 positionOffset, Vector3 lookOffset, float fieldOfView)
        {
            presentationPositionOffset = positionOffset;
            presentationLookOffset = lookOffset;
            Camera.fieldOfView = fieldOfView;
        }

        public void ClearPresentationResponse()
        {
            presentationPositionOffset = Vector3.zero;
            presentationLookOffset = Vector3.zero;
            Camera.fieldOfView = baselineFieldOfView;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.Lerp(transform.position, DesiredPosition(),
                1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
            LookAtTarget();
        }

        private Vector3 DesiredPosition() => target.position + Quaternion.Euler(0f, target.eulerAngles.y, 0f) * offset + presentationPositionOffset;
        private void LookAtTarget() => transform.LookAt(target.position + Vector3.up * .4f + presentationLookOffset, Vector3.up);
    }
}
