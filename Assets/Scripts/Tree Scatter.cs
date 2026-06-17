using UnityEngine;

public class TreeScatter : MonoBehaviour
{
    public GameObject treePrefab;
    public int count = 50;
    public Vector2 area = new Vector2(50, 50);
    public Transform parent;

    void Start()
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-area.x * 0.5f, area.x * 0.5f),
                0f,
                Random.Range(-area.y * 0.5f, area.y * 0.5f)
            );

            GameObject tree = Instantiate(treePrefab, pos, Quaternion.identity, parent);

            float s = Random.Range(0.8f, 1.2f);
            tree.transform.localScale = new Vector3(s, s, s);
        }
    }
}