using UnityEngine;

namespace Vehicle
{
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -5f);
        [SerializeField] private float followSharpness = 9f;
        private Transform target;
        public Camera Camera { get; private set; }

        private void Awake() => Camera = GetComponent<Camera>();

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

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.Lerp(transform.position, DesiredPosition(),
                1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
            LookAtTarget();
        }

        private Vector3 DesiredPosition() => target.position + Quaternion.Euler(0f, target.eulerAngles.y, 0f) * offset;
        private void LookAtTarget() => transform.LookAt(target.position + Vector3.up * .4f, Vector3.up);
    }
}
