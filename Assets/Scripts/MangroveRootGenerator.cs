using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedurally generates mangrove-style root lines beneath a tree sprite.
/// Attach this to your TreeCluster prefab. Roots auto-generate on Start.
/// Uses LineRenderers set to a lower sorting order so they appear under the tree sprite.
/// </summary>
public class MangroveRootGenerator : MonoBehaviour
{
    [Header("Root Appearance")]
    [Tooltip("Color of the roots. A dark brown works well for mangroves.")]
    public Color rootColor = new Color(0.35f, 0.22f, 0.10f, 1f);

    [Tooltip("Width of the main root arms.")]
    public float mainRootWidth = 0.4f;

    [Tooltip("Width of the smaller branch tips.")]
    public float branchTipWidth = 0.1f;

    [Tooltip("Sorting layer name that sits below your tree sprite (must match your project's sorting layers).")]
    public string sortingLayerName = "Default";

    [Tooltip("Order in sorting layer. Set this lower than your tree sprite's Order in Layer.")]
    public int sortingOrder = -1;

    [Header("Root Shape")]
    [Tooltip("How many main root arms spread out from the trunk.")]
    [Range(3, 12)]
    public int numberOfRoots = 6;

    [Tooltip("How far the roots spread outward from the center (in local units).")]
    public float rootLength = 1.5f;

    [Tooltip("How much randomness in root length per arm (0 = all same length).")]
    [Range(0f, 1f)]
    public float lengthVariance = 0.4f;

    [Tooltip("How much the root arms curve sideways (0 = perfectly straight).")]
    [Range(0f, 1f)]
    public float curviness = 0.35f;

    [Tooltip("Number of segments per root arm (higher = smoother curve).")]
    [Range(2, 12)]
    public int segments = 6;

    [Tooltip("How many sub-branches fork off each main root.")]
    [Range(0, 3)]
    public int branchesPerRoot = 2;

    [Tooltip("Sub-branches are this fraction of main root length.")]
    [Range(0.1f, 0.8f)]
    public float branchLengthRatio = 0.4f;

    [Tooltip("Random seed — change this per prefab variant for different patterns.")]
    public int seed = 0;

    private List<GameObject> rootObjects = new List<GameObject>();

    void Start()
    {
        GenerateRoots();
    }

    [ContextMenu("Regenerate Roots")]
    public void GenerateRoots()
    {
        ClearRoots();

        Random.InitState(seed == 0 ? GetInstanceID() : seed);

        float angleStep = 360f / numberOfRoots;
        // Slight random rotation of the whole root cluster so no two trees look identical
        float baseAngle = Random.Range(0f, angleStep);

        for (int i = 0; i < numberOfRoots; i++)
        {
            float angle = (baseAngle + i * angleStep) * Mathf.Deg2Rad;
            float length = rootLength * (1f + Random.Range(-lengthVariance, lengthVariance));

            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 end = direction * length;

            // Perpendicular vector for curving
            Vector3 perp = new Vector3(-direction.z, 0f, direction.x);
            float curveAmount = curviness * length * Random.Range(-1f, 1f);
            Vector3 midOffset = perp * curveAmount;
            Vector3 mid = (end * 0.5f) + midOffset;

            // Draw the main root arm as a curved line
            LineRenderer lr = CreateLineRenderer($"Root_{i}", mainRootWidth, branchTipWidth);
            DrawCurvedLine(lr, Vector3.zero, mid, end, segments);

            // Add sub-branches forking off near the end of each main root
            for (int b = 0; b < branchesPerRoot; b++)
            {
                float branchT = Random.Range(0.5f, 0.85f);
                Vector3 branchStart = QuadraticBezier(Vector3.zero, mid, end, branchT);

                float branchAngleOffset = Random.Range(20f, 50f) * (Random.value > 0.5f ? 1f : -1f);
                float branchAngle = (angle * Mathf.Rad2Deg + branchAngleOffset) * Mathf.Deg2Rad;
                Vector3 branchDir = new Vector3(Mathf.Cos(branchAngle), 0f, Mathf.Sin(branchAngle));
                float branchLength = length * branchLengthRatio * Random.Range(0.7f, 1.3f);
                Vector3 branchEnd = branchStart + branchDir * branchLength;

                LineRenderer branchLr = CreateLineRenderer($"Root_{i}_Branch_{b}", mainRootWidth * 0.6f, branchTipWidth);
                DrawStraightLine(branchLr, branchStart, branchEnd);
            }
        }
    }

    private LineRenderer CreateLineRenderer(string objName, float startWidth, float endWidth)
    {
        GameObject obj = new GameObject(objName);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.numCapVertices = 3;
        lr.numCornerVertices = 3;

        // Unlit material so it's not affected by 3D lighting (matches your sprite look)
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = rootColor;
        lr.endColor = rootColor;

        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;

        rootObjects.Add(obj);
        return lr;
    }

    private void DrawCurvedLine(LineRenderer lr, Vector3 start, Vector3 mid, Vector3 end, int steps)
    {
        lr.positionCount = steps + 1;
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            lr.SetPosition(i, QuadraticBezier(start, mid, end, t));
        }
    }

    private void DrawStraightLine(LineRenderer lr, Vector3 start, Vector3 end)
    {
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
    }

    private Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    [ContextMenu("Clear Roots")]
    public void ClearRoots()
    {
        foreach (var obj in rootObjects)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(obj);
                else
                    Destroy(obj);
#else
                Destroy(obj);
#endif
            }
        }
        rootObjects.Clear();

        // Also clean up any leftover root children from a previous run
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("Root_"))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child.gameObject);
                else
                    Destroy(child.gameObject);
#else
                Destroy(child.gameObject);
#endif
            }
        }
    }
}
