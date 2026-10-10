using UnityEngine;

/// Scatters N rocks (default 260) from the 6 moon_rock_XX variants.
/// Drag the imported moon_rock_01..06 GLBs into 'variants', put this on an empty GameObject, press Play
/// (or right-click the component > Scatter now). Deterministic via 'seed'.
public class MoonRockScatterer : MonoBehaviour
{
    public GameObject[] variants;
    public int count = 260;
    public int seed = 1969;
    public float radius = 120f;
    public float keepClearRadius = 22f;           // empty zone around the base (this object's position)
    public Vector2 scaleRange = new Vector2(0.6f, 1.8f);
    public LayerMask groundMask = ~0;             // rocks snap to terrain if a collider is below
    public float groundRayHeight = 50f;

    void Start() { Scatter(); }

    [ContextMenu("Scatter now")]
    public void Scatter()
    {
        for (int i = transform.childCount - 1; i >= 0; i--) { var c = transform.GetChild(i).gameObject; if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
        if (variants == null || variants.Length == 0) { Debug.LogWarning("MoonRockScatterer: assign variants"); return; }
        var rng = new System.Random(seed);
        for (int n = 0; n < count; n++)
        {
            float a = (float)(rng.NextDouble() * Mathf.PI * 2);
            float r = Mathf.Lerp(keepClearRadius, radius, Mathf.Sqrt((float)rng.NextDouble()));
            Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            if (Physics.Raycast(p + Vector3.up * groundRayHeight, Vector3.down, out var hit, groundRayHeight * 2, groundMask)) p = hit.point;
            // small rocks are common, big ones rare
            var v = variants[Mathf.Min(variants.Length - 1, (int)(Mathf.Pow((float)rng.NextDouble(), 1.6f) * variants.Length))];
            var go = Instantiate(v, p, Quaternion.Euler((float)(rng.NextDouble() * 10 - 5), (float)(rng.NextDouble() * 360), (float)(rng.NextDouble() * 10 - 5)), transform);
            go.transform.localScale = Vector3.one * Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
            go.isStatic = true;
        }
    }
}
