using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
//  KiteFlightTopDown2D.cs
//  Attach to your Kite GameObject (needs a Rigidbody).
//  Replaces KiteFlightSendMessages — same input wiring (Send
//  Messages via PlayerInput), but rotation is now YAW-ONLY.
//
//  Why: in a top-down view, a flat 2D sprite must stay flat.
//  Any pitch (X-axis) or roll (Z-axis) rotation makes it look
//  like it's shrinking/turning edge-on to the camera — which is
//  exactly the bug you saw in the video.
//
//  Right stick (Move)   → turn left/right (yaw) + forward speed boost
//  Left stick (Rotate)  → turn left/right (yaw), alternate control
//  Both feed the SAME yaw value here — no separate pitch axis at all.
// ============================================================

[RequireComponent(typeof(Rigidbody))]
public class KiteFlightTopDown2D : MonoBehaviour
{
    [Header("Forward Speed")]
    public float cruiseSpeed = 6f;

    [Tooltip("Extra speed when pushing the stick forward (moveInput.y > 0)")]
    public float boostSpeed = 3f;

    [Header("Turning (Yaw only)")]
    [Tooltip("Degrees per second at full stick deflection")]
    public float turnSpeed = 90f;

    [Header("Altitude — Climbing (Pump to fly)")]
    [Tooltip("Both sticks must move together (same direction) within this tolerance to count as a synced pump")]
    public float syncTolerance = 0.4f;

    [Tooltip("Minimum stick speed (units/sec) to register as part of a pump stroke — filters out tiny drift")]
    public float minPumpSpeed = 1.5f;

    [Tooltip("How many direction-reversals per second of MAX pumping gives MAX lift")]
    public float pumpRateForMaxLift = 4f;

    [Tooltip("Maximum upward speed achieved at full vigorous pumping")]
    public float maxClimbSpeed = 8f;

    [Tooltip("How quickly the pump rate estimate decays when you slow down or stop")]
    public float pumpRateDecay = 2.5f;

    [Tooltip("Time window used to measure pump rate (seconds) — shorter = more responsive, longer = smoother")]
    public float pumpRateWindow = 1.2f;

    [Header("Altitude — Descending (Dive)")]
    [Tooltip("Right stick pulled down past this threshold triggers a descend")]
    public float diveThreshold = 0.3f;

    [Tooltip("Downward speed while diving (right stick down)")]
    public float descendSpeed = 3f;

    [Tooltip("Sink speed when not pumping and not actively diving — gravity wins if you stop")]
    public float passiveSinkSpeed = 1.5f;

    [Header("Altitude Limits")]
    public float minHeight = 0f;
    public float maxHeight = 25f;

    [Header("Animation")]
    [Tooltip("Drag your kite's Animator here — needs a bool parameter called 'IsFlapping'")]
    public Animator birdAnimator;

    [Header("Smoothing")]
    public float rotationSmoothing = 6f;
    public float speedSmoothing = 4f;
    public float verticalSmoothing = 5f;

    // ── Private ───────────────────────────────────────────────────
    private Rigidbody rb;
    private float currentYaw;
    private float currentSpeed;
    private float currentVerticalSpeed;
    private float fixedXRotation;   // captured from the sprite's own resting pose

    public bool IsFlapping { get; private set; }   // true whenever pumping vigorously enough to climb
    public bool IsDiving   { get; private set; }
    public float CurrentHeight => transform.position.y;
    public float CurrentPumpRate { get; private set; }  // exposed for debugging/UI — reversals per second

    // ── Pump tracking state ──────────────────────────────────────
    private float prevMoveY, prevRotateY;          // previous frame's stick Y, to detect direction
    private int prevDirection = 0;                  // -1, 0, or +1 — last frame's combined movement direction
    private readonly System.Collections.Generic.List<float> reversalTimestamps = new();
    private float flapAnimTimer;                     // brief flash for the animator bool

    // Cached most-recent stick values — updated by OnMove/OnRotate
    private Vector2 moveInput;
    private Vector2 rotateInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;
        // Freeze X and Z rotation completely — only Y (yaw) is ever allowed.
        // This is the key fix: even if something else tries to rotate the
        // kite on those axes, physics will not apply it.
        rb.constraints = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;
        rb.linearDamping = 0f;

        // FIX FOR JITTER: without this, the Rigidbody only updates its
        // visual position on physics steps (FixedUpdate), which don't
        // align with render frames — causing visible micro-stutter on
        // any object (or camera) that follows it. Interpolate smooths
        // the visual position between physics steps.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Capture whatever X rotation the sprite was set up with in the
        // editor (e.g. 90°, to lay it flat for a top-down view) and treat
        // that as the permanent baseline — never assume it's 0.
        fixedXRotation = transform.eulerAngles.x;
        currentYaw = transform.eulerAngles.y;
    }

    // ── Called automatically by PlayerInput (Send Messages) ───────
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnRotate(InputValue value)
    {
        rotateInput = value.Get<Vector2>();
    }

    void Update()
    {
        // ── DEBUG — remove once confirmed working ──────────────────
        if (moveInput != Vector2.zero || rotateInput != Vector2.zero)
            Debug.Log($"Move: {moveInput}   Rotate: {rotateInput}");

        // ── Left stick (Rotate) controls turning ONLY ──────────────
        float turnInput = rotateInput.x;
        turnInput = Mathf.Clamp(turnInput, -1f, 1f);

        currentYaw += turnInput * turnSpeed * Time.deltaTime;

        // ── Right stick (Move) controls forward speed ONLY ─────────
        // Pushing up increases speed, pulling down slows/reverses.
        // moveInput.x is ignored entirely — no turning from this stick.
        float targetSpeed = cruiseSpeed + moveInput.y * boostSpeed;
        targetSpeed = Mathf.Max(targetSpeed, 0f);   // prevent going backward; remove this line if you want reverse
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * speedSmoothing);

        // ── Altitude: PUMP-TO-CLIMB detection ───────────────────────
        // Model: both sticks must move TOGETHER (same direction, up or
        // down) — each time that combined direction REVERSES (up→down
        // or down→up), it counts as half a pump stroke. We measure how
        // many reversals happened in the last `pumpRateWindow` seconds
        // and convert that rate directly into climb speed — the faster
        // and more vigorously you alternate, the more lift you get.

        float moveVel = (moveInput.y - prevMoveY) / Mathf.Max(Time.deltaTime, 0.0001f);
        float rotateVel = (rotateInput.y - prevRotateY) / Mathf.Max(Time.deltaTime, 0.0001f);
        prevMoveY = moveInput.y;
        prevRotateY = rotateInput.y;

        // Are both sticks currently moving in the same direction, fast enough to count?
        bool movingTogether = Mathf.Abs(moveVel) > minPumpSpeed
                            && Mathf.Abs(rotateVel) > minPumpSpeed
                            && Mathf.Abs(moveVel - rotateVel) < syncTolerance * 10f; // same direction & similar speed

        int currentDirection = 0;
        if (movingTogether)
            currentDirection = (moveVel + rotateVel) > 0f ? 1 : -1;

        // Detect a reversal: direction flipped from + to - or vice versa (ignore the first sample)
        if (currentDirection != 0 && prevDirection != 0 && currentDirection != prevDirection)
        {
            reversalTimestamps.Add(Time.time);
        }
        if (currentDirection != 0)
            prevDirection = currentDirection;

        // Prune old reversals outside our measurement window
        reversalTimestamps.RemoveAll(t => Time.time - t > pumpRateWindow);

        // Reversals per second → this IS the pump rate (each up-down-up cycle has 2 reversals,
        // so dividing by window gives "reversals/sec", which scales the same as pump vigor)
        CurrentPumpRate = reversalTimestamps.Count / pumpRateWindow;

        float pumpNormalised = Mathf.Clamp01(CurrentPumpRate / pumpRateForMaxLift);

        IsFlapping = pumpNormalised > 0.05f;   // any meaningful pumping flashes the animator

        if (IsFlapping) flapAnimTimer = 0.15f;
        flapAnimTimer = Mathf.Max(0f, flapAnimTimer - Time.deltaTime);

        // ── Altitude: DESCEND — right stick HELD down, not pumping ──
        // Important: while pumping, the stick passes through negative
        // values too (that's the "down" half of each stroke). Diving
        // should only count when the player is deliberately holding
        // down rather than actively oscillating — so we require pump
        // rate to be low AND the stick held down.
        IsDiving = moveInput.y < -diveThreshold && pumpNormalised < 0.1f;

        float targetVerticalSpeed;
        if (pumpNormalised > 0.05f)
        {
            // Lift scales directly with how vigorously you're pumping right now
            targetVerticalSpeed = pumpNormalised * maxClimbSpeed;
        }
        else if (IsDiving)
        {
            targetVerticalSpeed = -descendSpeed;
        }
        else
        {
            // Not pumping, not diving — gravity wins, kite sinks
            targetVerticalSpeed = -passiveSinkSpeed;
        }

        currentVerticalSpeed = Mathf.Lerp(currentVerticalSpeed, targetVerticalSpeed,
                                           Time.deltaTime * verticalSmoothing);

        // ── Drive the flap animation ────────────────────────────────
        if (birdAnimator != null)
            birdAnimator.SetBool("IsFlapping", flapAnimTimer > 0f);
    }

    void FixedUpdate()
    {
        // ── Apply yaw rotation — X stays locked at its original sprite
        // angle (e.g. 90°), only Y (yaw) ever changes ─────────────────
        Quaternion targetRotation = Quaternion.Euler(fixedXRotation, currentYaw, 0f);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation,
                                          Time.fixedDeltaTime * rotationSmoothing));

        // ── Move forward along the ground plane (XZ) ───────────────
        // NOTE: transform.forward is WRONG here — with the sprite tilted
        // 90° on X, "forward" points straight up/down, not along the
        // ground. Instead, build the movement direction from yaw alone.
        Vector3 moveDirection = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
        Vector3 velocity = moveDirection * currentSpeed;

        // ── Apply vertical movement (climb/dive), with height clamp ──
        float verticalVel = currentVerticalSpeed;
        float predictedHeight = transform.position.y + verticalVel * Time.fixedDeltaTime;
        if (predictedHeight < minHeight || predictedHeight > maxHeight)
            verticalVel = 0f;   // stop at the limit rather than overshoot

        rb.linearVelocity = new Vector3(velocity.x, verticalVel, velocity.z);
    }
}
