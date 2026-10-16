using System;
using UnityEngine;

namespace Mohakash
{
    /// <summary>
    /// Third-person player: camera-relative WASD, sprint, lunar-gravity jump, pick things up and carry them, bunk sleep.
    /// Two rigged characters share one root:
    ///   - AstronautEVA.glb : white pressure suit (outside the habitat)
    ///   - CrewCasual.glb   : suitless crew member in T-shirt and joggers (inside the habitat, when CasualIndoors is true)
    /// Both skeletons have the same clips: Idle Walk Run Jump Wave Interact Death LieDown Sleep GetUp PickUp PutDown CarryIdle CarryWalk CarryRun
    /// and the same sockets (Socket_Hold in front of the chest, Socket_HandL/R, Socket_Back).
    ///
    /// Carrying:  PickUp(item,...) walks to the item, crouches, grabs it, stands up holding it in both hands at Socket_Hold.
    ///            While held, locomotion switches to CarryIdle / CarryWalk / CarryRun.
    ///            PutDown(point,...) lowers it to the ground, lets go and stands up.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Astronaut : MonoBehaviour
    {
        public const float WalkSpeed  = 2.4f;
        public const float RunSpeed   = 4.6f;
        public const float MoonGravity = 1.62f;
        public const float Height      = 1.8f;

        /// <summary>true: the suited astronaut changes into the casual crew model inside the habitat. false: the suited astronaut stays (and sleeps in the bunk too).</summary>
        public static bool CasualIndoors = true;

        const float ClipWalkSpeed = 1.5f;   // m/s the Walk clip was authored for (no foot sliding)
        const float ClipRunSpeed  = 3.8f;
        const float WalkToRunSpeed = 3.2f;
        const float RunToWalkSpeed = 2.6f;

        // PickUp / PutDown clip timing (seconds into the 2.3 s clips, see tools/crew_v3/poses_extra.py)
        const float PickGrabAt = 0.95f, PickLiftFrom = 1.0f, PickLiftTo = 2.0f, PickMoveToHands = 0.70f;
        const float PutLowerFrom = 0.30f, PutLowerTo = 1.30f, PutReleaseAt = 1.40f;
        const float StandBackFromItem = 0.38f;   // root-to-item distance when crouching down for it

        public bool  Frozen;
        public float SpeedFactor = 1f;
        public bool  IsSprinting { get; private set; }
        public bool  Dead        { get; private set; }
        public bool  Indoors     { get; private set; }
        public bool  InBed       => bed != BedState.None;
        public bool  IsCarrying  => held != null;
        /// <summary>true while walking to an item / crouching / lowering it. Input is ignored during this.</summary>
        public bool  Busy        => act != Act.None;
        public GameObject Held   => held;

        CharacterController cc;
        GameObject evaModel, casualModel;
        CrewRig evaRig, casualRig;
        GameObject helmetLamp;
        Transform cam;

        Vector3 velocity, horizVel, prevPosition;
        float ungroundedTime, landTimer, oneShotTimer, landSquashTimer;
        bool wasGrounded, isRunning, jumpPlayed;
        const float LandSquashDur = 0.22f;

        // ---- bunk sequence
        enum BedState { None, Approach, Lying, Asleep, GettingUp, Leave }
        BedState bed = BedState.None;
        float bedTimer;
        Vector3 bedFrom, bedPos, bedExit; Quaternion bedFromRot, bedRot;

        // ---- pick up / put down sequence
        enum Act { None, ApproachPick, Pick, Put }
        Act act = Act.None;
        float actTimer;
        GameObject held;                 // object currently in the hands (parented to Socket_Hold) or being moved by an act
        float heldScale = 0.45f;         // final world scale of the held object
        Vector3 actStand, actItemStart, actTarget; Quaternion actItemRot;
        Vector3 actItemScaleStart; float actTargetScale;
        bool actGrabbed, actReleased, actKeep;
        Action actOnGrab, actOnRelease, actOnDone;

        bool UseCasual => Indoors && CasualIndoors && casualRig != null;
        CrewRig Rig => UseCasual ? casualRig : evaRig;
        GameObject Model => UseCasual ? casualModel : evaModel;

        // ── Factory ────────────────────────────────────────────────────────
        public static Astronaut Create(Vector3 pos)
        {
            var root = new GameObject("Astronaut (player)");
            root.transform.position = pos;
            root.layer = 2;

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.92f, 0);
            cc.slopeLimit = 40f; cc.stepOffset = 0.4f;

            var a = root.AddComponent<Astronaut>();
            a.evaModel    = MoonWorld.Spawn("AstronautEVA", pos, Quaternion.identity, 1f, root.transform, "Astronaut (EVA suit)");
            a.casualModel = MoonWorld.Spawn("CrewCasual",   pos, Quaternion.identity, 1f, root.transform, "Crew member (casual clothes)");
            a.evaRig    = Prepare(a.evaModel);
            a.casualRig = Prepare(a.casualModel);
            if (a.casualModel) a.casualModel.SetActive(false);
            a.AddHelmetLamp(root.transform);
            root.AddComponent<Footsteps>();
            return a;
        }

        static CrewRig Prepare(GameObject model)
        {
            if (model == null) return null;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            var rig = model.AddComponent<CrewRig>();
            rig.Init();
            rig.Play("Idle", 0f);
            return rig;
        }

        void AddHelmetLamp(Transform root)
        {
            helmetLamp = new GameObject("Helmet lamp"); helmetLamp.transform.SetParent(root, false);
            helmetLamp.transform.localPosition = new Vector3(0f, 1.78f, 0.22f); helmetLamp.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            var lamp = helmetLamp.AddComponent<Light>();
            lamp.type = LightType.Spot; lamp.range = 18f; lamp.spotAngle = 70f; lamp.intensity = 0f; lamp.shadows = LightShadows.None; lamp.color = new Color(1f, 0.97f, 0.9f);
            Light sun = null; foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) sun = l;
            var nl = helmetLamp.AddComponent<NightLight>(); nl.Lamp = lamp; nl.Sun = sun; nl.Intensity = 12f;
        }

        void Awake() { cc = GetComponent<CharacterController>(); }
        void Start() { prevPosition = transform.position; }

        // ── Public API ─────────────────────────────────────────────────────
        public void SetIndoors(bool indoors)
        {
            if (indoors == Indoors && (evaModel != null || casualModel != null)) { if (!indoors) ForceStandUp(); return; }
            ForceStandUp();
            CancelAct();
            Indoors = indoors;
            bool casual = UseCasual;
            if (evaModel) evaModel.SetActive(!casual);
            if (casualModel) casualModel.SetActive(casual);
            if (helmetLamp) helmetLamp.SetActive(!indoors);
            AttachHeldToHands();
            Rig?.Play(IsCarrying ? "CarryIdle" : "Idle", 0f);
        }

        public void PlayWave()
        {
            if (Dead || bed != BedState.None || IsCarrying || Busy) return;
            oneShotTimer = 2.3f; Rig?.Play("Wave", 0.2f, 1f, true);
        }

        public void PlayInteract()
        {
            if (Dead || bed != BedState.None || IsCarrying || Busy) return;
            oneShotTimer = 1.5f; Rig?.Play("Interact", 0.1f, 1f, true);
        }

        public void Die()
        {
            if (Dead) return;
            Dead = true; ForceStandUp(); CancelAct();
            if (held) held.SetActive(false);
            Rig?.Play("Death", 0.2f, 1f, true);
        }

        public void Teleport(Vector3 p)
        {
            cc.enabled = false;
            transform.position = p;
            cc.enabled = true;
            velocity = Vector3.zero; horizVel = Vector3.zero; prevPosition = p;
        }

        // ── Carrying ───────────────────────────────────────────────────────
        /// <summary>
        /// Walk to 'item', crouch, grab it with both hands, stand up holding it. 'item' must already exist in the world; the astronaut takes ownership of it.
        /// onGrab fires at the moment the hands close (change mission state / hide the original there). keep=false: the item vanishes at the end (goes into the bag).
        /// Returns false (and does nothing) if busy, dead, asleep or already carrying.
        /// </summary>
        public bool PickUp(GameObject item, float carriedScale, Action onGrab = null, bool keep = true, Action onDone = null)
        {
            if (item == null || Dead || Busy || IsCarrying || bed != BedState.None || Rig == null || !Rig.Has("PickUp")) return false;
            foreach (var t in item.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            var rb = item.GetComponent<Rigidbody>(); if (rb) Destroy(rb);
            foreach (var c in item.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            held = item; heldScale = carriedScale; actKeep = keep;
            actOnGrab = onGrab; actOnDone = onDone; actOnRelease = null;
            actGrabbed = false; actReleased = false; actTimer = 0f;

            Vector3 to = item.transform.position - transform.position; to.y = 0;
            Vector3 dir = to.sqrMagnitude > 0.0001f ? to.normalized : transform.forward;
            actStand = item.transform.position - dir * StandBackFromItem; actStand.y = transform.position.y;
            act = Act.ApproachPick;
            horizVel = Vector3.zero;
            Rig.Play(Flat(actStand - transform.position).magnitude > 0.15f ? "Walk" : "Idle", 0.1f);
            return true;
        }

        /// <summary>Lower the held object to 'groundPoint' (world, centre of the object), let go (onPlaced fires, the held object is destroyed: build the placed version there), stand up.</summary>
        public bool PutDown(Vector3 groundPoint, float placedScale, Action onPlaced = null, Action onDone = null)
        {
            if (!IsCarrying || Dead || Busy || bed != BedState.None || Rig == null || !Rig.Has("PutDown")) return false;
            actTarget = groundPoint; actTargetScale = placedScale;
            actOnRelease = onPlaced; actOnDone = onDone; actOnGrab = null;
            actReleased = false; actTimer = 0f; act = Act.Put;
            held.transform.SetParent(null, true);
            actItemScaleStart = held.transform.lossyScale;
            horizVel = Vector3.zero;
            Rig.Play("PutDown", 0.1f, 1f, true);
            return true;
        }

        /// <summary>Instantly remove whatever is in the hands (used by scripted tests and when entering vehicles).</summary>
        public void DropHeld(bool destroy = true)
        {
            CancelAct();
            if (held == null) return;
            if (destroy) Destroy(held); else held.transform.SetParent(null, true);
            held = null;
        }

        void CancelAct()
        {
            if (act == Act.None) return;
            // an interrupted pick-up gives the object back at the spot it was lifted from
            if (act != Act.Put && held != null && !actGrabbed) { Destroy(held); held = null; }
            act = Act.None;
        }

        void AttachHeldToHands()
        {
            if (held == null) return;
            var socket = Rig != null ? Rig.Socket("Socket_Hold") : null;
            if (socket == null) { held.transform.SetParent(transform, true); return; }
            held.transform.SetParent(socket, true);
            held.transform.localPosition = Vector3.zero; held.transform.localRotation = Quaternion.identity;
            SetWorldScale(held.transform, heldScale);
        }

        static void SetWorldScale(Transform t, float s)
        {
            var p = t.parent; float ps = p ? p.lossyScale.x : 1f;
            t.localScale = Vector3.one * (s / Mathf.Max(0.0001f, ps));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        Vector3 HandsMid()
        {
            var l = Rig.Socket("Socket_HandL"); var r = Rig.Socket("Socket_HandR");
            if (l && r) return (l.position + r.position) * 0.5f;
            var h = Rig.Socket("Socket_Hold"); return h ? h.position : transform.position + Vector3.up;
        }

        void UpdateAct(float dt)
        {
            actTimer += dt;
            var rig = Rig;
            // gravity / ground contact while the player cannot steer
            velocity.y = cc.isGrounded ? -1f : velocity.y - MoonGravity * dt;

            switch (act)
            {
                case Act.ApproachPick:
                {
                    Vector3 to = actStand - transform.position; to.y = 0;
                    Vector3 look = (held ? held.transform.position : actStand) - transform.position; look.y = 0;
                    if (look.sqrMagnitude > 0.0004f)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-12f * dt));
                    bool arrived = to.magnitude < 0.07f || actTimer > 3.2f;
                    Vector3 step = arrived ? Vector3.zero : to.normalized * Mathf.Min(WalkSpeed * SpeedFactor, to.magnitude / Mathf.Max(dt, 0.001f));
                    cc.Move((step + Vector3.up * velocity.y) * dt);
                    if (!arrived) { rig.Play("Walk", 0.1f); rig.SetSpeed(Mathf.Clamp(step.magnitude / ClipWalkSpeed, 0.5f, 1.9f)); }
                    bool facing = look.sqrMagnitude < 0.0004f || Vector3.Angle(transform.forward, look) < 12f;
                    if (arrived && (facing || actTimer > 3.6f))
                    {
                        act = Act.Pick; actTimer = 0f;
                        actItemStart = held.transform.position; actItemRot = held.transform.rotation; actItemScaleStart = held.transform.lossyScale;
                        rig.Play("PickUp", 0.12f, 1f, true);
                    }
                    break;
                }
                case Act.Pick:
                {
                    cc.Move(Vector3.up * velocity.y * dt);
                    float t = actTimer;
                    if (!actGrabbed && t >= PickGrabAt) { actGrabbed = true; actOnGrab?.Invoke(); }
                    if (held != null)
                    {
                        Quaternion faceRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                        if (t < PickLiftFrom)
                        {
                            float k = Smooth(PickMoveToHands, PickGrabAt + 0.05f, t);        // object slides into the hands at the bottom of the crouch
                            held.transform.position = Vector3.Lerp(actItemStart, HandsMid(), k);
                            held.transform.rotation = Quaternion.Slerp(actItemRot, faceRot, k);
                            SetScaleBlend(actItemScaleStart.x, heldScale, k);
                        }
                        else
                        {
                            float k = Smooth(PickLiftFrom, PickLiftTo, t);                    // lifted to the chest while standing up
                            var hold = rig.Socket("Socket_Hold");
                            Vector3 holdPos = hold ? hold.position : transform.position + Vector3.up;
                            held.transform.position = Vector3.Lerp(HandsMid(), holdPos, k);
                            held.transform.rotation = Quaternion.Slerp(faceRot, hold ? hold.rotation : faceRot, k);
                            SetScaleBlend(heldScale, heldScale, 1f);
                        }
                    }
                    float len = Mathf.Max(2.2f, rig.Length("PickUp") - 0.02f);
                    if (t >= len)
                    {
                        act = Act.None;
                        if (actKeep) { AttachHeldToHands(); rig.Play("CarryIdle", 0.2f, 1f, true); }
                        else { if (held) Destroy(held); held = null; rig.Play("Idle", 0.2f); }
                        var d = actOnDone; actOnDone = null; d?.Invoke();
                    }
                    break;
                }
                case Act.Put:
                {
                    cc.Move(Vector3.up * velocity.y * dt);
                    float t = actTimer;
                    if (held != null && !actReleased)
                    {
                        var hold = rig.Socket("Socket_Hold");
                        Vector3 holdPos = hold ? hold.position : transform.position + Vector3.up;
                        float k = Smooth(PutLowerFrom, PutLowerTo, t);
                        held.transform.position = Vector3.Lerp(holdPos, actTarget, k);
                        Quaternion faceRot = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                        held.transform.rotation = Quaternion.Slerp(hold ? hold.rotation : faceRot, faceRot, k);
                        SetScaleBlend(heldScale, actTargetScale, k);
                    }
                    if (!actReleased && t >= PutReleaseAt)
                    {
                        actReleased = true;
                        if (held) Destroy(held); held = null;
                        var r = actOnRelease; actOnRelease = null; r?.Invoke();
                    }
                    float len = Mathf.Max(2.2f, rig.Length("PutDown") - 0.02f);
                    if (t >= len)
                    {
                        act = Act.None; rig.Play("Idle", 0.2f);
                        var d = actOnDone; actOnDone = null; d?.Invoke();
                    }
                    break;
                }
            }
        }

        void SetScaleBlend(float from, float to, float k)
        {
            if (held == null) return;
            held.transform.localScale = Vector3.one * Mathf.Lerp(from, to, k);
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        /// <summary>Call every frame. want=true: go to the bunk and sleep; want=false: get up and walk back to 'exit'.</summary>
        public void SetSleeping(bool want, Vector3 pos, float yawDeg, Vector3 exit)
        {
            if (Dead || !Indoors || Rig == null) return;
            var rig = Rig;
            bedPos = pos; bedRot = Quaternion.Euler(0, yawDeg, 0); bedExit = exit;
            if (want)
            {
                if (bed == BedState.None && act == Act.None)
                {
                    bed = BedState.Approach; bedTimer = 0f;
                    bedFrom = transform.position; bedFromRot = transform.rotation;
                    cc.enabled = false; horizVel = Vector3.zero; velocity = Vector3.zero;
                    rig.Play("Walk", 0.1f, 1f);
                }
            }
            else
            {
                if (bed == BedState.Approach) { bed = BedState.None; cc.enabled = true; rig.Play(IsCarrying ? "CarryIdle" : "Idle", 0.15f); }
                else if (bed == BedState.Lying || bed == BedState.Asleep)
                {
                    bed = BedState.GettingUp; bedTimer = 0f; rig.Play("GetUp", 0.05f, 1f, true);
                }
            }
        }

        void ForceStandUp()
        {
            if (bed == BedState.None) return;
            bed = BedState.None;
            if (cc != null) { cc.enabled = false; transform.position = bedExit; cc.enabled = true; }
            Rig?.Play(IsCarrying ? "CarryIdle" : "Idle", 0f);
        }

        // ── Bunk state machine ─────────────────────────────────────────────
        void UpdateBed(float dt)
        {
            var rig = Rig;
            bedTimer += dt;
            switch (bed)
            {
                case BedState.Approach:
                {
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(bedTimer / 0.6f));
                    transform.position = Vector3.Lerp(bedFrom, bedPos, k);
                    transform.rotation = Quaternion.Slerp(bedFromRot, bedRot, k);
                    if (bedTimer >= 0.6f) { bed = BedState.Lying; bedTimer = 0f; rig.Play("LieDown", 0.05f, 1f, true); }
                    break;
                }
                case BedState.Lying:
                    transform.position = bedPos; transform.rotation = bedRot;
                    if (bedTimer >= Mathf.Max(1.8f, rig.Length("LieDown") - 0.05f)) { bed = BedState.Asleep; rig.Play("Sleep", 0.2f); }
                    break;
                case BedState.Asleep:
                    transform.position = bedPos; transform.rotation = bedRot;
                    break;
                case BedState.GettingUp:
                    transform.position = bedPos; transform.rotation = bedRot;
                    if (bedTimer >= Mathf.Max(1.8f, rig.Length("GetUp") - 0.05f))
                    {
                        bed = BedState.Leave; bedTimer = 0f; bedFrom = bedPos;
                        bedFromRot = bedRot;
                        rig.Play(IsCarrying ? "CarryWalk" : "Walk", 0.1f, 1f);
                    }
                    break;
                case BedState.Leave:
                {
                    float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01(bedTimer / 0.6f));
                    transform.position = Vector3.Lerp(bedFrom, bedExit, k);
                    Vector3 d = bedExit - bedFrom; d.y = 0;
                    if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(bedFromRot, Quaternion.LookRotation(d), k);
                    if (bedTimer >= 0.6f)
                    {
                        bed = BedState.None; cc.enabled = false; transform.position = bedExit; cc.enabled = true;
                        prevPosition = transform.position; rig.Play(IsCarrying ? "CarryIdle" : "Idle", 0.15f);
                    }
                    break;
                }
            }
        }

        string Clip(string baseName) => IsCarrying && Rig != null && Rig.Has("Carry" + baseName) ? "Carry" + baseName : baseName;

        // ── Main update ────────────────────────────────────────────────────
        void Update()
        {
            if (cam == null && Camera.main) cam = Camera.main.transform;
            if (Dead) return;
            float dt = Time.deltaTime;

            if (held != null && act == Act.None) held.SetActive(bed == BedState.None);   // not visible on the pillow
            if (bed != BedState.None) { UpdateBed(dt); return; }
            if (act != Act.None) { UpdateAct(dt); return; }

            Vector2 input = Frozen ? Vector2.zero : GameInput.Move;
            Vector3 fwd   = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = cam ? Vector3.ProjectOnPlane(cam.right,   Vector3.up).normalized : Vector3.right;
            Vector3 moveDir = fwd * input.y + right * input.x;
            float inputMag = Mathf.Clamp01(moveDir.magnitude);

            IsSprinting = !Frozen && GameInput.Sprint && inputMag > 0.01f;
            float topSpeed = (IsSprinting ? RunSpeed : WalkSpeed) * SpeedFactor;

            bool grounded = cc.isGrounded;
            if (grounded && velocity.y < 0f) velocity.y = -1f;
            ungroundedTime = grounded ? 0f : ungroundedTime + dt;

            if (grounded && !Frozen && GameInput.Jump)
            {
                velocity.y = Mathf.Sqrt(2f * MoonGravity * 1.8f);   // ~1.8 m apex on the Moon
                jumpPlayed = false;
            }
            float gravScale = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(1f - Mathf.Abs(velocity.y) / 2f));
            velocity.y -= MoonGravity * gravScale * dt;

            Vector3 targetVel = moveDir.normalized * (topSpeed * inputMag);
            float accel = !grounded ? 3f : (targetVel.sqrMagnitude > horizVel.sqrMagnitude ? 11f : 15f);
            horizVel = Vector3.MoveTowards(horizVel, targetVel, accel * dt);
            Vector3 horizontal = horizVel;
            cc.Move((horizontal + Vector3.up * velocity.y) * dt);

            Vector3 faceDir = moveDir.sqrMagnitude > 0.01f ? moveDir : (horizVel.sqrMagnitude > 0.25f ? horizVel : Vector3.zero);
            if (faceDir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), 1f - Mathf.Exp(-11f * dt));

            float horizSpeed = horizontal.magnitude;
            prevPosition = transform.position;

            bool justLanded = !wasGrounded && grounded;
            if (justLanded && ungroundedTime < 0.001f) { landTimer = 0.22f; landSquashTimer = LandSquashDur; }
            wasGrounded = grounded;
            landTimer -= dt; oneShotTimer -= dt;
            bool airborne = ungroundedTime > 0.14f;

            var rig = Rig;
            if (rig != null)
            {
                if (airborne)
                {
                    if (!jumpPlayed) { rig.Play("Jump", 0.08f, 1f, true); jumpPlayed = true; }
                }
                else
                {
                    jumpPlayed = false;
                    if (oneShotTimer > 0f && inputMag < 0.05f) { /* let Wave / Interact finish */ }
                    else if (inputMag < 0.05f) rig.Play(Clip("Idle"), 0.2f);
                    else
                    {
                        if (!isRunning && horizSpeed >= WalkToRunSpeed) isRunning = true;
                        if (isRunning && horizSpeed <= RunToWalkSpeed) isRunning = false;
                        if (isRunning || IsSprinting) { rig.Play(Clip("Run"), 0.15f); rig.SetSpeed(Mathf.Clamp(horizSpeed / ClipRunSpeed, 0.6f, 1.6f)); }
                        else { rig.Play(Clip("Walk"), 0.15f); rig.SetSpeed(Mathf.Clamp(horizSpeed / ClipWalkSpeed, 0.5f, 1.9f)); }
                    }
                }
            }

            // landing squash on the active model
            var model = Model;
            if (model != null)
            {
                Vector3 target = Vector3.one;
                if (landSquashTimer > 0f)
                {
                    landSquashTimer -= dt;
                    float t = Mathf.Clamp01(landSquashTimer / LandSquashDur);
                    float sy = Mathf.Lerp(1f, 0.9f, Mathf.Sin(t * Mathf.PI)), sx = Mathf.Lerp(1f, 1.07f, Mathf.Sin(t * Mathf.PI));
                    target = new Vector3(sx, sy, sx);
                }
                model.transform.localScale = Vector3.Lerp(model.transform.localScale, target, 12f * dt);
            }
        }
    }
}
