using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Scatters tree cluster prefabs on a Terrain, following the actual heightmap,
/// and restricts placement to areas where a chosen terrain texture layer
/// (e.g. "marsh"/brown) is dominant.
/// </summary>
public class TreeClusterPlacer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the parent object that contains all your Terrain tiles (e.g. 'Terrain Marsh').")]
    public Transform terrainTilesParent;
    public GameObject[] treeClusterPrefabs; // e.g. Cluster 1, Cluster 2

    [Header("Texture Filtering (single background texture)")]
    [Tooltip("The single big texture you painted sand/marsh colors onto, applied to the terrain material.")]
    public Texture2D groundTexture;
    [Tooltip("Approximate color of the marsh/brown area you want to allow trees on.")]
    public Color marshColor = new Color(0.45f, 0.33f, 0.18f); // adjust to match your brown
    [Tooltip("How many times the ground texture repeats across the terrain (matches your material's Tiling X/Y values).")]
    public Vector2 textureTiling = new Vector2(1f, 1f);
    [Tooltip("How close a sampled pixel must be to marshColor to count as marsh (0 = exact match required, higher = looser).")]
    [Range(0f, 1f)] public float colorTolerance = 0.25f;

    [Header("Scatter Settings")]
    [Tooltip("Number of trees to attempt per terrain tile (not total across all tiles).")]
    public int treesPerTile = 30;
    public float minScale = 0.8f;
    public float maxScale = 1.4f;
    public bool randomYRotation = true;
    [Tooltip("Base rotation (in degrees) to lay the sprite flat for top-down view. Default -90 on X works for most vertical sprite quads. Flip to 90 if it ends up upside down.")]
    public Vector3 flatRotationOffset = new Vector3(-90f, 0f, 0f);
    [Tooltip("Minimum distance between placed trees, to avoid overlap.")]
    public float minSpacing = 3f;

    [Header("Parent")]
    public Transform treeParent;

    private TerrainData terrainData;
    private Vector3 terrainPos;
    private List<Vector3> placedPositions = new List<Vector3>();

    [ContextMenu("Place Trees")]
    public void PlaceTrees()
    {
        if (terrainTilesParent == null)
        {
            Debug.LogError("Assign the parent object containing your terrain tiles first.");
            return;
        }
        if (treeClusterPrefabs == null || treeClusterPrefabs.Length == 0)
        {
            Debug.LogError("Assign at least one tree cluster prefab.");
            return;
        }

        ClearExistingTrees();

        Terrain[] tiles = terrainTilesParent.GetComponentsInChildren<Terrain>();
        if (tiles.Length == 0)
        {
            Debug.LogError("No Terrain components found under the assigned parent.");
            return;
        }

        int totalPlaced = 0;

        foreach (Terrain tile in tiles)
        {
            totalPlaced += PlaceTreesOnTile(tile);
        }

        Debug.Log($"Placed {totalPlaced} tree clusters across {tiles.Length} terrain tiles.");
    }

    private int PlaceTreesOnTile(Terrain tile)
    {
        terrainData = tile.terrainData;
        terrainPos = tile.transform.position;

        Vector2 minXZ = new Vector2(terrainPos.x, terrainPos.z);
        Vector2 maxXZ = new Vector2(terrainPos.x + terrainData.size.x, terrainPos.z + terrainData.size.z);

        // Reset spacing check per tile (trees on different tiles rarely overlap anyway)
        placedPositions.Clear();

        int placed = 0;
        int attempts = 0;
        int maxAttempts = treesPerTile * 30;

        while (placed < treesPerTile && attempts < maxAttempts)
        {
            attempts++;

            float x = Random.Range(minXZ.x, maxXZ.x);
            float z = Random.Range(minXZ.y, maxXZ.y);

            if (!IsMarshAt(x, z))
                continue;

            Vector3 candidate = new Vector3(x, 0f, z);
            if (TooClose(candidate))
                continue;

            float y = tile.SampleHeight(candidate) + terrainPos.y;
            Vector3 worldPos = new Vector3(x, y, z);

            GameObject prefab = treeClusterPrefabs[Random.Range(0, treeClusterPrefabs.Length)];
            GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity);

            Quaternion flatten = Quaternion.Euler(flatRotationOffset);
            if (randomYRotation)
            {
                float randomY = Random.Range(0f, 360f);
                instance.transform.rotation = Quaternion.AngleAxis(randomY, Vector3.up) * flatten;
            }
            else
            {
                instance.transform.rotation = flatten;
            }

            float scale = Random.Range(minScale, maxScale);
            instance.transform.localScale *= scale;

            if (treeParent != null)
                instance.transform.SetParent(treeParent);

            placedPositions.Add(worldPos);
            placed++;
        }

        return placed;
    }

    [ContextMenu("Clear Placed Trees")]
    public void ClearExistingTrees()
    {
        if (treeParent == null) return;

        for (int i = treeParent.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(treeParent.GetChild(i).gameObject);
            else
                Destroy(treeParent.GetChild(i).gameObject);
#else
            Destroy(treeParent.GetChild(i).gameObject);
#endif
        }
    }

    private bool TooClose(Vector3 candidate)
    {
        foreach (var p in placedPositions)
        {
            float dx = p.x - candidate.x;
            float dz = p.z - candidate.z;
            if (dx * dx + dz * dz < minSpacing * minSpacing)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Samples the ground texture's pixel color at the world position (via UV mapping
    /// across the terrain bounds) and checks whether it's close enough to marshColor.
    /// </summary>
    private bool IsMarshAt(float worldX, float worldZ)
    {
        if (groundTexture == null)
        {
            Debug.LogWarning("Assign groundTexture to enable marsh-only filtering.");
            return true; // fallback: allow placement everywhere
        }

        Vector3 terrainLocalPos = new Vector3(worldX, 0, worldZ) - terrainPos;

        float u = terrainLocalPos.x / terrainData.size.x;
        float v = terrainLocalPos.z / terrainData.size.z;

        // Apply tiling: texture repeats N times across the terrain,
        // so multiply by tiling factor then wrap into 0-1 range.
        u = Mathf.Repeat(u * textureTiling.x, 1f);
        v = Mathf.Repeat(v * textureTiling.y, 1f);

        int pixelX = Mathf.Clamp(Mathf.RoundToInt(u * (groundTexture.width - 1)), 0, groundTexture.width - 1);
        int pixelY = Mathf.Clamp(Mathf.RoundToInt(v * (groundTexture.height - 1)), 0, groundTexture.height - 1);

        Color sampled = groundTexture.GetPixel(pixelX, pixelY);

        float dist = ColorDistance(sampled, marshColor);
        return dist <= colorTolerance;
    }

    private float ColorDistance(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db) / 1.732f; // normalize to 0-1 range
    }
}
