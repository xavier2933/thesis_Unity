using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlateSequenceManager : MonoBehaviour
{
    [Header("References")]
    public Transform platesParent;
    public SimpleTruckNav navScript;
    public RoverROSComms rosComms; 
    public PlateTFBroadcaster plateTfBroadcaster; 

    [Header("Settings")]
    public float waitTimeAtPlate = 1.0f;
    public Vector3 testWaypoint; // For manual testing

    private List<Vector3> platePositions = new List<Vector3>();
    private Coroutine currentNavigationCoroutine = null;

    void Start()
    {
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        
        if (platesParent == null || navScript == null)
        {
            Debug.LogError($"[SEQUENCE] Missing references on {gameObject.name}!");
            return;
        }
        
        GetAllPlatePositions();
        
        if (rosComms != null && platePositions.Count > 0)
        {
            rosComms.SendPlateLocationsToROS(platePositions.ToArray());
        }
    }

    void GetAllPlatePositions()
    {
        platePositions.Clear();
        foreach (Transform t in platesParent)
        {
            Vector3 pos = t.position;
            pos.x += 2.2f; // Your specific offset
            platePositions.Add(pos);
        }
        Debug.Log($"<color=white>[SEQUENCE] Found {platePositions.Count} plates</color>");
    }

    // ==================== PUBLIC API ====================
    
    public void GoToPosition(Vector3 targetWorldPos, float? targetHeading = null)
    {
        if (currentNavigationCoroutine != null) StopCoroutine(currentNavigationCoroutine);
        currentNavigationCoroutine = StartCoroutine(NavigateToPositionCoroutine(targetWorldPos, -1, targetHeading));
    }

    public void GoToPlate(int plateIndex)
    {
        if (plateIndex < 0 || plateIndex >= platePositions.Count) return;
        if (currentNavigationCoroutine != null) StopCoroutine(currentNavigationCoroutine);
        currentNavigationCoroutine = StartCoroutine(NavigateToPositionCoroutine(platePositions[plateIndex], plateIndex));
    }
    
    public void StartFullAutoSequence()
    {
        if (currentNavigationCoroutine != null) StopCoroutine(currentNavigationCoroutine);
        currentNavigationCoroutine = StartCoroutine(FullAutoSequenceCoroutine());
    }
    
    public void StopNavigation()
    {
        if (currentNavigationCoroutine != null)
        {
            StopCoroutine(currentNavigationCoroutine);
            currentNavigationCoroutine = null;
        }
        navScript.hasGoal = false;
        navScript.StopRobot();
    }

    // ==================== MASTER COROUTINE ====================

    private IEnumerator NavigateToPositionCoroutine(Vector3 targetPos, int plateIndex = -1, float? targetHeading = null)
    {
        Vector3 startPos = navScript.transform.position;
        bool isPlate = (plateIndex != -1);

        if (rosComms != null)
        {
            rosComms.isNavigating = true; 
            if (isPlate) rosComms.NotifyNavigationStarted(plateIndex);
        }

        // --- DECISION LOGIC ---
        if (targetHeading.HasValue)
        {
            // Use Bezier Curve if a heading is provided
            navScript.SetCurvedGoal(startPos, targetPos, targetHeading.Value, finalGoal: isPlate);
        }
        else
        {
            // Use Straight Line (original behavior)
            navScript.SetLineGoal(startPos, targetPos, finalGoal: isPlate);
        }

        while (navScript.hasGoal)
        {
            yield return null;
        }

        if (rosComms != null)
        {
            rosComms.isNavigating = false;
            
            if (plateIndex != -1) // It's a PLATE
            {
                rosComms.NotifyPlateReached(plateIndex);
                if (plateTfBroadcaster != null)
                    plateTfBroadcaster.SetTargetPlate(platesParent.GetChild(plateIndex));
            }
            else // It's a WAYPOINT (Antenna)
            {
                if (plateTfBroadcaster != null)
                    plateTfBroadcaster.SetVirtualTarget(targetPos); // ADD THIS LINE
            }
        }
        currentNavigationCoroutine = null;
    }

    private IEnumerator FullAutoSequenceCoroutine()
    {
        for (int i = 0; i < platePositions.Count; i++)
        {
            yield return StartCoroutine(NavigateToPositionCoroutine(platePositions[i], i));
            yield return new WaitForSeconds(waitTimeAtPlate);
        }
    }

    // ==================== TESTING ====================
    
    [ContextMenu("Test: Go To Manual Waypoint")]
    void TestGoToWaypoint() { GoToPosition(testWaypoint); }

    [ContextMenu("Test: Run 10m Rope Mission")]
    void TestRopeMission() { StartCoroutine(SimulatedRopeMission()); }

    private IEnumerator SimulatedRopeMission()
    {
        Vector3 start = transform.position;
        Vector3 mid = start + transform.forward * 5f;
        Vector3 end = start + transform.forward * 10f;

        // Leg 1
        if(rosComms?.ropeDeployer != null) rosComms.ropeDeployer.isDeploying = true;
        GoToPosition(mid);
        while (currentNavigationCoroutine != null) yield return null;

        // Stop & "Place"
        if(rosComms?.ropeDeployer != null) rosComms.ropeDeployer.isDeploying = false;
        Debug.Log("Waiting for arm...");
        yield return new WaitForSeconds(3f);

        // Leg 2
        if(rosComms?.ropeDeployer != null) rosComms.ropeDeployer.isDeploying = true;
        GoToPosition(end);
        while (currentNavigationCoroutine != null) yield return null;

        if(rosComms?.ropeDeployer != null) rosComms.ropeDeployer.isDeploying = false;
        Debug.Log("Mission Complete!");
    }
}