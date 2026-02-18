using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Drop-in replacement for SimpleTruckNav with optional Bezier curve support.
/// Maintains exact same API and behavior as SimpleTruckNav.
/// Use SetLineGoal() for straight lines (original behavior).
/// Use SetCurvedGoal() for smooth Bezier curves with custom end orientation.
/// </summary>
public class SimpleTruckNav : MonoBehaviour
{
    [Header("Target")]
    public Vector3 targetPosition;
    public float targetHeading; 
    public bool hasGoal = false;
    public bool isFinalGoal = false;

    [Header("Line Following")]
    public bool useLineFollowing = false;
    public Vector3 lineStart;
    public Vector3 lineEnd;
    public float lookAheadDistance = 1.0f;

    [Header("Curve Following")]
    public bool useCurveFollowing = false;
    
    [Header("Settings")]
    public float moveSpeed = 0.2f;
    public float turnSpeed = 1.0f;
    public float stopDistance = 0.4f;
    public float angleTolerance = 8f;

    [Header("Friction Compensation")]
    public float minForwardCmd = 0.25f;
    public float minTurnCmd = 0.5f;

    [Header("References")]
    public TruckAndArmController controller;
    
    [Header("Visualization")]
    public bool showPath = true;
    public Color pathColor = Color.cyan;
    
    [Header("Inspector Testing")]
    public Vector3 testWaypoint = new Vector3(5, 0, 5);
    public float testHeading = 0f;
    public bool testUseCurve = false;
    
    // Bezier curve data
    private Vector3 p0, p1, p2, p3;
    private List<Vector3> curvePoints = new List<Vector3>();
    private List<float> curveDistances = new List<float>();
    private float totalCurveLength = 0f;
    private int curveResolution = 50;
    
    // Line renderer for path visualization
    private LineRenderer lineRenderer;

    void Start()
    {
        SetupLineRenderer();
    }

    void Update()
    {
        if (!hasGoal || controller == null) 
        {
            StopRobot();
            return;
        }

        float forwardCmd = 0;
        float turnCmd = 0;

        if (useCurveFollowing)
        {
            // BEZIER CURVE FOLLOWING (NEW)
            FollowBezierCurve(ref forwardCmd, ref turnCmd);
        }
        else if (useLineFollowing)
        {
            // STRAIGHT LINE FOLLOWING (ORIGINAL)
            FollowStraightLine(ref forwardCmd, ref turnCmd);
        }
        else
        {
            // WAYPOINT NAVIGATION (ORIGINAL)
            NavigateToWaypoint(ref forwardCmd, ref turnCmd);
        }

        controller.rosForward = forwardCmd;
        controller.rosTurn = turnCmd;
    }

    void FollowStraightLine(ref float forwardCmd, ref float turnCmd)
    {
        // EXACT ORIGINAL IMPLEMENTATION
        Vector3 lineDir = (lineEnd - lineStart).normalized;
        Vector3 toRover = transform.position - lineStart;
        toRover.y = 0;
        float progressAlongLine = Vector3.Dot(toRover, lineDir);
        
        float pursuitProgress = progressAlongLine + lookAheadDistance;
        
        float lineLength = Vector3.Distance(lineStart, lineEnd);
        pursuitProgress = Mathf.Clamp(pursuitProgress, 0, lineLength);
        Vector3 pursuitPoint = lineStart + lineDir * pursuitProgress;
        pursuitPoint.y = transform.position.y;
        
        float distanceToEnd = lineLength - progressAlongLine;
        
        Vector3 toPursuit = pursuitPoint - transform.position;
        toPursuit.y = 0;
        float desiredHeading = Mathf.Atan2(toPursuit.x, toPursuit.z) * Mathf.Rad2Deg;
        float headingError = Mathf.DeltaAngle(transform.eulerAngles.y, desiredHeading);
        
        if (Mathf.Abs(headingError) > angleTolerance)
        {
            turnCmd = ApplyDeadband(Mathf.Clamp(headingError / 30f, -1f, 1f) * turnSpeed, minTurnCmd);
        }
        
        if (distanceToEnd > stopDistance)
        {
            if (Mathf.Abs(headingError) < 30f)
            {
                float speedMult = Mathf.Clamp01(distanceToEnd / (stopDistance * 2));
                float finalSpeedMult = isFinalGoal ? 0.5f : 1.0f;
                forwardCmd = ApplyDeadband(moveSpeed * Mathf.Max(speedMult, 0.5f) * finalSpeedMult, minForwardCmd);
            }
        }
        
        if (distanceToEnd <= stopDistance)
        {
            float finalHeadingError = Mathf.DeltaAngle(transform.eulerAngles.y, targetHeading);
            if (Mathf.Abs(finalHeadingError) <= angleTolerance)
            {
                Debug.Log($"<color=white>Reached end of line with correct heading!</color>");
                StopRobot();
                hasGoal = false;
            }
            else
            {
                turnCmd = ApplyDeadband(Mathf.Clamp(finalHeadingError / 30f, -1f, 1f) * turnSpeed, minTurnCmd);
            }
        }
        
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[LINE] Progress: {progressAlongLine:F2}/{lineLength:F2}m, DistToEnd: {distanceToEnd:F2}m, Heading: {headingError:F1}°");
        }
    }

    void NavigateToWaypoint(ref float forwardCmd, ref float turnCmd)
    {
        // EXACT ORIGINAL IMPLEMENTATION
        Vector3 diff = targetPosition - transform.position;
        diff.y = 0; 
        float distance = diff.magnitude;
        
        float headingError = Mathf.DeltaAngle(transform.eulerAngles.y, targetHeading);
        
        if (Mathf.Abs(headingError) > angleTolerance)
        {
            turnCmd = ApplyDeadband(Mathf.Clamp(headingError / 30f, -1f, 1f) * turnSpeed, minTurnCmd);
        }

        if (distance > stopDistance)
        {
            if (Mathf.Abs(headingError) < 30f)
            {
                float speedMult = Mathf.Clamp01(distance / (stopDistance * 2));
                float finalSpeedMult = isFinalGoal ? 0.5f : 1.0f;
                forwardCmd = ApplyDeadband(moveSpeed * Mathf.Max(speedMult, 0.5f) * finalSpeedMult, minForwardCmd);
            }
        }

        if (distance <= stopDistance && Mathf.Abs(headingError) <= angleTolerance)
        {
            Debug.Log($"<color=white>Reached Point at {targetPosition} with correct heading!</color>");
            StopRobot();
            hasGoal = false; 
        }

        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[NAV] Dist: {distance:F2}m (need <{stopDistance}), Heading: {headingError:F1}° (need <{angleTolerance}°)");
        }
    }

    void FollowBezierCurve(ref float forwardCmd, ref float turnCmd)
    {
        // NEW BEZIER CURVE FOLLOWING
        if (curvePoints.Count == 0) return;
        
        Vector3 currentPos = transform.position;
        currentPos.y = 0;
        
        // Find closest point on curve
        float closestDist = float.MaxValue;
        int closestIndex = 0;
        
        for (int i = 0; i < curvePoints.Count; i++)
        {
            Vector3 curvePoint = curvePoints[i];
            curvePoint.y = 0;
            float dist = Vector3.Distance(currentPos, curvePoint);
            
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIndex = i;
            }
        }
        
        // Find pursuit point ahead along curve
        float currentDistance = curveDistances[closestIndex];
        float targetDistance = currentDistance + lookAheadDistance;
        
        int pursuitIndex = closestIndex;
        for (int i = closestIndex; i < curvePoints.Count; i++)
        {
            if (curveDistances[i] >= targetDistance)
            {
                pursuitIndex = i;
                break;
            }
            pursuitIndex = i;
        }
        
        Vector3 pursuitPoint = curvePoints[pursuitIndex];
        pursuitPoint.y = currentPos.y;
        
        float distanceToEnd = totalCurveLength - currentDistance;
        
        // Navigate toward pursuit point
        Vector3 toPursuit = pursuitPoint - currentPos;
        toPursuit.y = 0;
        float desiredHeading = Mathf.Atan2(toPursuit.x, toPursuit.z) * Mathf.Rad2Deg;
        float headingError = Mathf.DeltaAngle(transform.eulerAngles.y, desiredHeading);
        
        if (Mathf.Abs(headingError) > angleTolerance)
        {
            turnCmd = ApplyDeadband(Mathf.Clamp(headingError / 30f, -1f, 1f) * turnSpeed, minTurnCmd);
        }
        
        if (distanceToEnd > stopDistance)
        {
            if (Mathf.Abs(headingError) < 30f)
            {
                float speedMult = Mathf.Clamp01(distanceToEnd / (stopDistance * 2));
                float finalSpeedMult = isFinalGoal ? 0.5f : 1.0f;
                forwardCmd = ApplyDeadband(moveSpeed * Mathf.Max(speedMult, 0.5f) * finalSpeedMult, minForwardCmd);
            }
        }
        else
        {
            // Near end - check final heading alignment
            float finalHeadingError = Mathf.DeltaAngle(transform.eulerAngles.y, targetHeading);
            
            if (Mathf.Abs(finalHeadingError) <= angleTolerance)
            {
                Debug.Log($"<color=white>Reached end of curve with correct heading!</color>");
                StopRobot();
                hasGoal = false;
            }
            else
            {
                turnCmd = ApplyDeadband(Mathf.Clamp(finalHeadingError / 30f, -1f, 1f) * turnSpeed, minTurnCmd);
            }
        }
        
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log($"[CURVE] Progress: {currentDistance:F2}/{totalCurveLength:F2}m, DistToEnd: {distanceToEnd:F2}m, Heading: {headingError:F1}°");
        }
    }

    float ApplyDeadband(float input, float minPower)
    {
        if (Mathf.Abs(input) < 0.01f) return 0;
        return Mathf.Sign(input) * Mathf.Max(Mathf.Abs(input), minPower);
    }

    public void StopRobot() 
    { 
        if (controller != null)
        {
            controller.rosForward = 0; 
            controller.rosTurn = 0;
        }
    }
    
    // ORIGINAL API - Exact same as SimpleTruckNav
    public void SetGoal(Vector3 pos, float heading, bool finalGoal = false) 
    { 
        targetPosition = pos; 
        targetHeading = heading; 
        hasGoal = true;
        isFinalGoal = finalGoal;
        useLineFollowing = false;
        useCurveFollowing = false;
        ClearPathVisualization();
    }

    public void SetLineGoal(Vector3 start, Vector3 end, bool finalGoal = false)
    {
        // EXACT ORIGINAL IMPLEMENTATION
        lineStart = start;
        lineEnd = end;
        lineStart.y = 0;
        lineEnd.y = 0;
        targetHeading = Mathf.Atan2((end - start).x, (end - start).z) * Mathf.Rad2Deg;
        useLineFollowing = true;
        useCurveFollowing = false;
        isFinalGoal = finalGoal;
        hasGoal = true;
        
        // Visualize straight line
        VisualizeStraightLine();
        
        Debug.Log($"<color=cyan>[LINE GOAL] Start: {start}, End: {end}, Heading: {targetHeading:F1}°</color>");
    }
    
    // NEW API - For curved paths with custom end orientation
    public void SetCurvedGoal(Vector3 start, Vector3 end, float endHeading, bool finalGoal = false)
    {
        targetPosition = end;
        targetHeading = endHeading;
        isFinalGoal = finalGoal;
        useLineFollowing = false;
        useCurveFollowing = true;
        
        // Generate Bezier curve
        GenerateBezierCurve(start, end, endHeading);
        hasGoal = true;
        
        Debug.Log($"<color=magenta>[CURVE GOAL] Start: {start}, End: {end}, EndHeading: {endHeading:F1}°</color>");
    }

    void GenerateBezierCurve(Vector3 start, Vector3 end, float endHeading)
    {
        p0 = start;
        p3 = end;
        
        // Calculate start heading from current orientation
        float startHeading = transform.eulerAngles.y;
        
        // Control points based on headings
        float d = Vector3.Distance(p0, p3) / 2f;
        
        float phi = startHeading * Mathf.Deg2Rad;
        float theta = endHeading * Mathf.Deg2Rad;
        
        p1 = p0 + d * new Vector3(Mathf.Sin(phi), 0, Mathf.Cos(phi));
        p2 = p3 - d * new Vector3(Mathf.Sin(theta), 0, Mathf.Cos(theta));
        
        // Generate curve points
        curvePoints.Clear();
        curveDistances.Clear();
        totalCurveLength = 0f;
        
        Vector3 previousPoint = p0;
        
        for (int i = 0; i <= curveResolution; i++)
        {
            float t = (float)i / curveResolution;
            Vector3 point = GetBezierPoint(t);
            
            curvePoints.Add(point);
            
            if (i > 0)
            {
                totalCurveLength += Vector3.Distance(previousPoint, point);
            }
            curveDistances.Add(totalCurveLength);
            
            previousPoint = point;
        }
        
        // Visualize curve
        VisualizeCurve();
    }

    Vector3 GetBezierPoint(float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        
        Vector3 point = uuu * p0;
        point += 3f * uu * t * p1;
        point += 3f * u * tt * p2;
        point += ttt * p3;
        
        return point;
    }

    void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = pathColor;
        lineRenderer.endColor = pathColor;
        lineRenderer.positionCount = 0;
    }

    void VisualizeStraightLine()
    {
        if (!showPath || lineRenderer == null) return;
        
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, lineStart);
        lineRenderer.SetPosition(1, lineEnd);
    }

    void VisualizeCurve()
    {
        if (!showPath || lineRenderer == null || curvePoints.Count == 0) return;
        
        lineRenderer.positionCount = curvePoints.Count;
        for (int i = 0; i < curvePoints.Count; i++)
        {
            lineRenderer.SetPosition(i, curvePoints[i]);
        }
    }

    void ClearPathVisualization()
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
    }
    
    // ==================== INSPECTOR TESTING ====================
    
    [ContextMenu("Test: Go To Waypoint (Straight)")]
    void TestStraightWaypoint()
    {
        Vector3 start = transform.position;
        SetLineGoal(start, testWaypoint);
        Debug.Log($"<color=yellow>TEST: Straight line to {testWaypoint}</color>");
    }
    
    [ContextMenu("Test: Go To Waypoint (Curved)")]
    void TestCurvedWaypoint()
    {
        Vector3 start = transform.position;
        SetCurvedGoal(start, testWaypoint, testHeading);
        Debug.Log($"<color=yellow>TEST: Curved path to {testWaypoint}, ending at {testHeading}°</color>");
    }
    
    [ContextMenu("Test: Go To Waypoint (Auto)")]
    void TestAutoWaypoint()
    {
        Vector3 start = transform.position;
        if (testUseCurve)
        {
            SetCurvedGoal(start, testWaypoint, testHeading);
            Debug.Log($"<color=yellow>TEST: Auto (Curved) to {testWaypoint}, ending at {testHeading}°</color>");
        }
        else
        {
            SetLineGoal(start, testWaypoint);
            Debug.Log($"<color=yellow>TEST: Auto (Straight) to {testWaypoint}</color>");
        }
    }
    
    [ContextMenu("Stop Navigation")]
    void TestStop()
    {
        hasGoal = false;
        StopRobot();
        Debug.Log("<color=red>TEST: Navigation stopped</color>");
    }
    
    void OnDrawGizmos()
    {
        // Draw test waypoint in Scene view
        Gizmos.color = testUseCurve ? Color.magenta : Color.cyan;
        Gizmos.DrawWireSphere(testWaypoint, 0.3f);
        
        // Draw heading arrow at test waypoint
        Vector3 headingDir = Quaternion.Euler(0, testHeading, 0) * Vector3.forward;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(testWaypoint, headingDir * 1.0f);
        
        // Draw label
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(testWaypoint + Vector3.up * 0.5f, 
            testUseCurve ? $"Test (Curve)\n{testHeading:F0}°" : "Test (Straight)");
        #endif
        
        // Draw current rover position and heading
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            Gizmos.DrawRay(transform.position, transform.forward * 0.8f);
        }
    }
}