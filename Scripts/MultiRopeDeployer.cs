using UnityEngine;
using System.Collections.Generic;

public class MultiRopeDeployer : MonoBehaviour
{
    [Header("References")]
    public Terrain terrain;
    public Transform roverTransform;
    public GameObject ropePrefab;

    [Header("Settings")]
    public float wireOffset = 0.05f;
    public float segmentLength = 0.5f;

    [Header("Controls")]
    public bool isDeploying = false;
    private bool _wasDeploying = false; // Internal tracker for the toggle

    private LineRenderer currentLR;
    private List<Vector3> currentPoints = new List<Vector3>();
    private Vector3 lastDroppedPosition;

    void Start()
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        Debug.Log("Rope Deployer Initialized.");
    }

    // This ensures the toggle works when clicked in the Inspector
    void OnValidate()
    {
        if (Application.isPlaying && isDeploying && !_wasDeploying)
        {
            StartNewSegment();
        }
        _wasDeploying = isDeploying;
    }

    void StartNewSegment()
    {
        if (ropePrefab == null || roverTransform == null)
        {
            Debug.LogError("RopePrefab or RoverTransform is missing!");
            return;
        }

        Debug.Log("<color=green>Starting New Rope Segment</color>");
        
        GameObject newRope = Instantiate(ropePrefab, Vector3.zero, Quaternion.identity);
        newRope.name = "Rope_Segment_" + Time.time;
        currentLR = newRope.GetComponent<LineRenderer>();
        
        currentPoints.Clear();
        AddPoint(roverTransform.position);
        lastDroppedPosition = roverTransform.position;
    }

    void Update()
    {
        // Manual toggle check for code-based changes
        if (isDeploying && !_wasDeploying) StartNewSegment();
        _wasDeploying = isDeploying;

        if (!isDeploying || currentLR == null) return;

        float distanceMoved = Vector3.Distance(roverTransform.position, lastDroppedPosition);

        if (distanceMoved >= segmentLength)
        {
            Debug.Log($"Distance reached ({distanceMoved:F2}m). Adding point #{currentPoints.Count + 1}");
            AddPoint(roverTransform.position);
            lastDroppedPosition = roverTransform.position;
        }

        UpdateLineRenderer();
    }

    void AddPoint(Vector3 worldPos)
    {
        float y = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
        Vector3 snappedPos = new Vector3(worldPos.x, y + wireOffset, worldPos.z);
        currentPoints.Add(snappedPos);
    }

    void UpdateLineRenderer()
    {
        currentLR.positionCount = currentPoints.Count + 1;

        for (int i = 0; i < currentPoints.Count; i++)
        {
            currentLR.SetPosition(i, currentPoints[i]);
        }

        float roverY = terrain.SampleHeight(roverTransform.position) + terrain.transform.position.y;
        Vector3 currentRoverPos = new Vector3(roverTransform.position.x, roverY + wireOffset, roverTransform.position.z);
        
        currentLR.SetPosition(currentPoints.Count, currentRoverPos);
    }
}