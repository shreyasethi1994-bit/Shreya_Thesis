using UnityEngine;

// ================================================================
//  KiteCameraTopDown.cs
//  Attach this to your Main Camera.
//  Drag your Kite ("Bird Flying 1") into the "target" field.
//
//  Keeps the camera's height and tilt fixed (top-down view) and
//  only smoothly follows the kite's X/Z position underneath it.
//  Does NOT rotate with the kite — top-down cameras usually stay
//  fixed-angle so the player doesn't get disoriented.
// ================================================================

public class KiteCameraTopDown : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Drag your Kite GameObject here")]
    public Transform target;

    [Header("Follow")]
    [Tooltip("How smoothly the camera catches up. Lower = floatier lag, higher = snappier. " +
             "If the kite keeps escaping the frame, raise this value.")]
    public float followSmoothing = 8f;

    [Tooltip("Safety net: camera will never be farther than this from the kite, " +
             "regardless of smoothing — prevents the kite from ever truly leaving frame.")]
    public float maxFollowDistance = 6f;

    [Tooltip("If true, camera keeps its CURRENT height/offset from the kite automatically " +
             "(captured on Start). If false, set offset manually below.")]
    public bool autoCaptureOffset = true;

    [Tooltip("Manual offset used only if autoCaptureOffset is false. " +
             "Y = height above kite, X/Z = horizontal offset (usually 0,0).")]
    public Vector3 manualOffset = new Vector3(0f, 10f, 0f);

    // ── Private ──────────────────────────────────────────────────
    private Vector3 offset;
    private Vector3 currentVelocity;  // used by SmoothDamp

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("KiteCameraTopDown: No target assigned! Drag the Kite into the Target field.");
            return;
        }

        if (autoCaptureOffset)
        {
            // Whatever height/position the camera was placed at in the
            // editor becomes the offset it maintains forever — this way
            // you don't have to guess numbers, just position the camera
            // where it looks good in the Scene view before hitting Play.
            offset = transform.position - target.position;
        }
        else
        {
            offset = manualOffset;
        }

        // Snap immediately, no lerp on the first frame
        transform.position = target.position + offset;
    }

    // LateUpdate so the kite has already moved this frame before the camera follows
    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            1f / followSmoothing   // SmoothDamp takes smoothTime, not a speed value
        );

        // ── Hard safety clamp ───────────────────────────────────────
        // No matter how the smoothing behaves, never let the camera's
        // horizontal distance from the kite exceed maxFollowDistance.
        // This guarantees the kite can never truly leave the frame,
        // even during a sudden burst of speed.
        Vector3 flatDelta = transform.position - target.position;
        flatDelta.y = 0f;   // ignore height offset, only clamp horizontal distance
        if (flatDelta.magnitude > maxFollowDistance)
        {
            Vector3 clamped = flatDelta.normalized * maxFollowDistance;
            Vector3 corrected = target.position + clamped;
            corrected.y = transform.position.y;   // preserve height
            transform.position = corrected;
        }

        // Rotation is intentionally left untouched — top-down cameras
        // typically stay fixed-angle (pointing straight down) at all times.
    }
}
