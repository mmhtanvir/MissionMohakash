using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// CharacterController-based player: WASD (camera-relative), Shift sprint, Space jump, E interact.
    /// Gravity/jump are tuned for game feel (floaty), NOT real lunar physics.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }
        public const float PlayAreaRadius = 110f;

        public float walkSpeed = 3f;
        public float sprintSpeed = 5.5f;
        public float jumpSpeed = 3.4f;
        public float gravity = -4.5f;
        public float interactRange = 3f;

        public ThirdPersonCamera cam;
        public AstronautRig rig;
        public Interactable Nearest { get; private set; }

        CharacterController cc;
        Vector3 velocity;
        float walkPhase;
        float swing;
        Vector3 spawn;

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
            spawn = transform.position;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var mission = MissionState.Instance;
            bool active = mission == null || mission.Phase == MissionPhase.Playing;

            Vector2 input = active ? InputReader.Move : Vector2.zero;
            bool sprinting = active && InputReader.Sprint && input.sqrMagnitude > 0.01f;
            if (mission != null) mission.playerSprinting = sprinting;

            Vector3 fwd = Vector3.forward, right = Vector3.right;
            if (cam != null)
            {
                fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                right = Vector3.Cross(Vector3.up, fwd);
            }
            Vector3 dir = fwd * input.y + right * input.x;
            float speed = sprinting ? sprintSpeed : walkSpeed;

            bool grounded = cc.isGrounded;
            if (grounded && velocity.y < 0f) velocity.y = -1f;
            if (active && grounded && InputReader.JumpPressed) velocity.y = jumpSpeed;
            velocity.y += gravity * Time.deltaTime;

            cc.Move((dir * speed + Vector3.up * velocity.y) * Time.deltaTime);

            // Keep inside the play area
            Vector3 flat = new Vector3(transform.position.x, 0f, transform.position.z);
            if (flat.magnitude > PlayAreaRadius)
            {
                cc.Move(-flat.normalized * (flat.magnitude - PlayAreaRadius));
                if (mission != null) mission.Say("Out of mission area - turn back.");
            }
            if (transform.position.y < -20f) { cc.enabled = false; transform.position = spawn; cc.enabled = true; velocity = Vector3.zero; }

            if (rig != null)
            {
                if (dir.sqrMagnitude > 0.01f)
                    rig.visual.rotation = Quaternion.Slerp(rig.visual.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
                Animate(dir.magnitude > 0.01f && grounded, speed);
            }

            FindNearest();
            if (active && InputReader.InteractPressed && Nearest != null && mission != null) mission.Interact(Nearest);
        }

        void Animate(bool walking, float speed)
        {
            float target = 0f;
            if (walking)
            {
                walkPhase += Time.deltaTime * speed * 2.2f;
                target = Mathf.Sin(walkPhase) * 35f;
            }
            swing = Mathf.Lerp(swing, target, 15f * Time.deltaTime);
            rig.legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            rig.legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            rig.armL.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            rig.armR.localRotation = Quaternion.Euler(swing, 0f, 0f);
        }

        void FindNearest()
        {
            Nearest = null;
            float best = interactRange;
            foreach (var it in Interactable.All)
            {
                if (it.Used) continue;
                float d = Vector3.Distance(transform.position, it.transform.position);
                if (d < best) { best = d; Nearest = it; }
            }
        }
    }
}
