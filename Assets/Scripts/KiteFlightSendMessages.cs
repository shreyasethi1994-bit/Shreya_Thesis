using UnityEngine;
using UnityEngine.InputSystem;

// ============================================================
//  KiteFlightSendMessages.cs
//  Attach to the SAME GameObject that has the "Player Input"
//  component (the one in your screenshot).
//
//  Behavior = "Send Messages" means Unity automatically calls
//  OnMove() and OnRotate() below whenever those actions fire —
//  you do NOT need to manually Enable/Disable anything, and you
//  do NOT need the generated C# class for this approach.
//
//  Method names matter: they must be exactly "On" + ActionName.
//  Your actions are called "Move" and "Rotate", so the methods
//  must be OnMove and OnRotate (capitalisation matters).
// ============================================================

[RequireComponent(typeof(Rigidbody))]
public class KiteFlightSendMessages : MonoBehaviour
{
    [Header("Forward Speed")]
    public float cruiseSpeed = 10f;

    [Header("Turning (Right Stick / Move)")]
    public float glidePitchSpeed = 40f;
    public float glideTurnSpeed = 70f;

    [Header("Rotating (Left Stick / Rotate)")]
    public float bankRotateSpeed = 55f;

    [Header("Gravity")]
    public float glideGravity = 3f;

    [Header("Smoothing")]
    public float rotationSmoothing = 5f;

    // ── Private ───────────────────────────────────────────────────
    private Rigidbody rb;
    private Quaternion targetRotation;
    private float verticalVelocity;

    // Cached most-recent stick values — updated by OnMove/OnRotate,
    // consumed every frame in Update/FixedUpdate
    private Vector2 moveInput;
    private Vector2 rotateInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;            // your screenshot shows Use Gravity is OFF already — good
        rb.isKinematic = false;           // your screenshot shows this is OFF — good, this was the #1 suspect
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.linearDamping = 0f;
        // Angular Damping in your screenshot is 0.05 — fine, we override rotation manually anyway

        targetRotation = transform.rotation;
    }

    // ── These are called AUTOMATICALLY by the PlayerInput component ──
    // No Enable()/Disable() needed — "Send Messages" handles that.

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
        // ── DEBUG — leave this in until you confirm input is flowing ──
        if (moveInput != Vector2.zero || rotateInput != Vector2.zero)
            Debug.Log($"Move: {moveInput}   Rotate: {rotateInput}");

        ApplyRotationFromInput();
    }

    void ApplyRotationFromInput()
    {
        if (Mathf.Abs(moveInput.y) > 0.01f)
        {
            float pitch = -moveInput.y * glidePitchSpeed * Time.deltaTime;
            targetRotation *= Quaternion.Euler(pitch, 0f, 0f);
        }
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            float yawFromMove = moveInput.x * glideTurnSpeed * Time.deltaTime;
            targetRotation *= Quaternion.Euler(0f, yawFromMove, 0f);
        }
        if (Mathf.Abs(rotateInput.x) > 0.01f)
        {
            float yaw = rotateInput.x * bankRotateSpeed * Time.deltaTime;
            targetRotation *= Quaternion.Euler(0f, yaw, 0f);
        }

        float rollTarget = -rotateInput.x * 35f;
        Vector3 e = targetRotation.eulerAngles;
        targetRotation = Quaternion.Euler(e.x, e.y, rollTarget);

        verticalVelocity = Mathf.Lerp(verticalVelocity, -glideGravity, Time.deltaTime * rotationSmoothing);
    }

    void FixedUpdate()
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation,
                                               Time.fixedDeltaTime * rotationSmoothing);

        Vector3 forward = transform.forward * cruiseSpeed;
        rb.linearVelocity = new Vector3(forward.x, verticalVelocity, forward.z);
    }
}
