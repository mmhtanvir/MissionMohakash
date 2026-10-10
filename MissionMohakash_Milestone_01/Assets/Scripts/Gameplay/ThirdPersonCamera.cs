using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Orbit third-person camera with a spring-arm (sphere cast) so it does not clip through rocks/the lander,
    /// plus a hard floor so it never dips under the ground. Esc releases the cursor; left-click re-locks it.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 5.5f;
        public float pivotHeight = 1.7f;
        public float minPitch = -15f;
        public float maxPitch = 65f;
        public float yaw;
        public float pitch = 18f;

        // Player lives on layer 2 (Ignore Raycast); exclude it from the spring-arm cast.
        const int PlayerLayer = 2;

        public bool CursorLocked { get { return Cursor.lockState == CursorLockMode.Locked; } }

        void Start() { SetLock(true); }

        void OnDisable() { SetLock(false); }

        void OnApplicationFocus(bool focus) { if (focus && wantLock) SetLock(true); }

        bool wantLock = true;

        void SetLock(bool locked)
        {
            wantLock = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            if (InputReader.EscapePressed) SetLock(false);
            else if (!CursorLocked && InputReader.ClickPressed) SetLock(true);

            if (CursorLocked)
            {
                Vector2 look = InputReader.LookDelta;
                yaw += look.x;
                pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
            }
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 dir = rot * Vector3.back;

            float d = distance;
            RaycastHit hit;
            int mask = ~(1 << PlayerLayer);
            if (Physics.SphereCast(pivot, 0.25f, dir, out hit, distance, mask, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(0.6f, hit.distance);

            Vector3 pos = pivot + dir * d;
            if (pos.y < 0.25f) pos.y = 0.25f; // ground is at y = 0
            transform.position = pos;
            transform.LookAt(pivot);
        }
    }
}
