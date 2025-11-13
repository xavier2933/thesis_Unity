using UnityEngine;
using System.IO;
using System.Globalization;

public class TerrainDataExporter : MonoBehaviour
{
    public Terrain terrain;
    public int resolution = 512; // sampling resolution
    public string slopeFileName = "terrain_slope.csv";
    public string heightFileName = "terrain_height.csv";

    void Start()
    {
        ExportHeightAndSlope();
    }

    void ExportHeightAndSlope()
    {
        TerrainData data = terrain.terrainData;
        int w = data.heightmapResolution;
        int h = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, w, h);

        float[,] slope = new float[h, w];

        float terrainWidth = data.size.x;
        float terrainHeight = data.size.y;
        float terrainLength = data.size.z;

        float dx = terrainWidth / (w - 1);
        float dz = terrainLength / (h - 1);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int xm = Mathf.Max(x - 1, 0);
                int xp = Mathf.Min(x + 1, w - 1);
                int ym = Mathf.Max(y - 1, 0);
                int yp = Mathf.Min(y + 1, h - 1);

                // Convert normalized heights to world units
                float hL = heights[y, xm] * terrainHeight;
                float hR = heights[y, xp] * terrainHeight;
                float hD = heights[ym, x] * terrainHeight;
                float hU = heights[yp, x] * terrainHeight;

                float dhdx = (hR - hL) / ((xp - xm) * dx);
                float dhdz = (hU - hD) / ((yp - ym) * dz);

                float grad = Mathf.Sqrt(dhdx * dhdx + dhdz * dhdz);
                slope[y, x] = Mathf.Atan(grad) * Mathf.Rad2Deg;
            }
        }

        // Save raw heightmap (in meters)
        string heightPath = Path.Combine(Application.dataPath, heightFileName);
        using (StreamWriter writer = new StreamWriter(heightPath))
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float heightMeters = heights[y, x] * terrainHeight;
                    writer.Write(heightMeters.ToString(CultureInfo.InvariantCulture));
                    if (x < w - 1) writer.Write(",");
                }
                writer.WriteLine();
            }
        }

        // Save slope map (in degrees)
        string slopePath = Path.Combine(Application.dataPath, slopeFileName);
        using (StreamWriter writer = new StreamWriter(slopePath))
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    writer.Write(slope[y, x].ToString(CultureInfo.InvariantCulture));
                    if (x < w - 1) writer.Write(",");
                }
                writer.WriteLine();
            }
        }

        Debug.Log($"Exported heightmap to {heightPath}");
        Debug.Log($"Exported slope map to {slopePath}");
    }
}
