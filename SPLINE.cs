using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class WireOnTerrain : MonoBehaviour
{
    public Terrain terrain;
    public Vector3[] controlPoints;
    public float wireOffset = 0.02f;

    private LineRenderer lr;

    void OnEnable()
    {
        lr = GetComponent<LineRenderer>();

        if (terrain == null)
            terrain = Terrain.activeTerrain;

        UpdateWire();
    }

    void OnValidate()
    {
        UpdateWire();
    }

    void UpdateWire()
    {
        if (lr == null || terrain == null || controlPoints == null || controlPoints.Length < 2)
            return;

        lr.positionCount = controlPoints.Length;

        for (int i = 0; i < controlPoints.Length; i++)
        {
            Vector3 p = controlPoints[i];
            float y = terrain.SampleHeight(p) + terrain.transform.position.y;
            p.y = y + wireOffset;
            lr.SetPosition(i, p);
        }
    }
}
