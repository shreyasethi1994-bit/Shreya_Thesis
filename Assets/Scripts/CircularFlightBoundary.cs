using UnityEngine;

/// <summary>
/// Keeps the bird within a circular boundary around a center point.
/// Instead of a hard physical stop, it smoothly steers the bird back inward
/// as it approaches/crosses the edge, which feels more natural for flight.
/// Attach this to your bird GameObject (alongside its existing flight/movement script).
/// </summary>
public class CircularFlightBoundary : MonoBehaviour
{
    [Header("Boundary Settings")]
    [Tooltip("Center of the circular play area (usually your map's center, XZ matters most).")]
    public Vector3 center = Vector3.zero;

    [Tooltip("Radius of the circular boundary, in world units.")]
    public float radius = 500f;

    [Tooltip("Distance from the edge where steering-back starts to kick in (soft buffer zone).")]
    public float softBufferDistance = 50f;

    [Tooltip("How strongly the bird gets pushed back toward center once past the buffer (higher = snappier correction).")]
    public float steerStrength = 2f;

    [Header("Hard Limit (safety net)")]
    [Tooltip("Absolute distance the bird is never allowed to exceed, regardless of steering. Should be >= radius.")]
    public float hardLimitRadius = 550f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>(); // optional, only used if present
    }

    void Update()
    {
        Vector3 flatPos = new Vector3(transform.position.x, center.y, transform.position.z);
        Vector3 flatCenter = new Vector3(center.x, center.y, center.z);

        float distFromCenter = Vector3.Distance(flatPos, flatCenter);
        float softEdge = radius - softBufferDistance;

        if (distFromCenter > softEdge)
        {
            // How far past the soft edge are we, normalized 0-1 within the buffer zone
            float overshoot = Mathf.Clamp01((distFromCenter - softEdge) / softBufferDistance);

            Vector3 directionToCenter = (flatCenter - flatPos).normalized;
            float pushStrength = overshoot * steerStrength;

            if (rb != null)
            {
                // Physics-based bird: apply a gentle inward force
                rb.AddForce(directionToCenter * pushStrength, ForceMode.Acceleration);
            }
            else
            {
                // Transform-based bird: nudge position directly
                transform.position += directionToCenter * pushStrength * Time.deltaTime;
            }
        }

        // Hard safety clamp so the bird truly never escapes, even at high speed
        float currentDist = Vector3.Distance(
            new Vector3(transform.position.x, center.y, transform.position.z),
            flatCenter);

        if (currentDist > hardLimitRadius)
        {
            Vector3 clampedFlat = flatCenter + (flatPos - flatCenter).normalized * hardLimitRadius;
            transform.position = new Vector3(clampedFlat.x, transform.position.y, clampedFlat.z);
        }
    }

    // Visualize the boundary in the Scene view for easy tuning
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        DrawCircle(center, radius);

        Gizmos.color = Color.yellow;
        DrawCircle(center, radius - softBufferDistance);

        Gizmos.color = Color.red;
        DrawCircle(center, hardLimitRadius);
    }

    private void DrawCircle(Vector3 c, float r)
    {
        int segments = 64;
        Vector3 prevPoint = c + new Vector3(r, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = c + new Vector3(Mathf.Cos(angle) * r, 0, Mathf.Sin(angle) * r);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}
