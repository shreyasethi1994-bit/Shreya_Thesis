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

    [Header("Smoothing")]
    public float rotationSmoothing = 6f;
    public float speedSmoothing = 4f;

    // ── Private ───────────────────────────────────────────────────
    private Rigidbody rb;
    private float currentYaw;
    private float currentSpeed;
    private float fixedXRotation;   // captured from the sprite's own resting pose

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
        rb.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);
    }
}
