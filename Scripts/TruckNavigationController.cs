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
    // Add this field at the top with the other curve data
    private bool curveAligning = false;
    private float curveAlignStartTime = 0f;
    public float curveAlignmentTimeout = 5.0f; // Max seconds to spend aligning heading
    
    [Header("Settings")]
    public float moveSpeed = 0.2f;
    public float turnSpeed = 1.0f;
    public float stopDistance = 0.4f;
    public float angleTolerance = 8f;

    [Header("Friction Compensation")]
    public float minForwardCmd = 0.25f;
    public float minTurnCmd = 0.65f;

    [Header("References")]
    public TruckAndArmController controller;
    
    [Header("Visualization")]
    public bool showPath = true;
    public Color pathColor = Color.cyan;
    [Header("Inspector Testing - Obstacle Avoidance")]
    public Vector3 testObstaclePos = new Vector3(2, 0, 3);
    public Vector3 testAvoidEndPoint = new Vector3(0, 0, 6);
    public float testAvoidEndHeading = 0f;
    public bool testAvoidLeft = true;
    
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
    private bool  rotatingInPlace    = false;
    private float rotateTargetHeading = 0f;


    void Start()
    {
        SetupLineRenderer();
    }

    void Update()
    {
        if (rotatingInPlace)
        {
            float err = Mathf.DeltaAngle(transform.eulerAngles.y, rotateTargetHeading);
            if (Mathf.Abs(err) <= angleTolerance)
            {
                Debug.Log($"<color=white>[NAV] RotateInPlace done — {transform.eulerAngles.y:F1}°</color>");
                rotatingInPlace = false;
                StopRobot();
                hasGoal = false;   // ← signals ROS that "arrival" happened
            }
            else
            {
                float rawTurn = Mathf.Clamp(err / 20f, -1f, 1f) * turnSpeed;
                controller.rosForward = 0f;
                controller.rosTurn    = ApplyDeadband(rawTurn, minTurnCmd);
            }
            return;   // skip all other nav logic while rotating
        }
        
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
        // Fully 2D — ignore Y for all distance/progress calculations
        Vector3 lineDirFull = lineEnd - lineStart;
        lineDirFull.y = 0;
        float lineLength = lineDirFull.magnitude;
        Vector3 lineDir = lineDirFull.normalized;

        Vector3 toRover = transform.position - lineStart;
        toRover.y = 0;
        float progressAlongLine = Vector3.Dot(toRover, lineDir);
        
        float pursuitProgress = Mathf.Clamp(progressAlongLine + lookAheadDistance, 0, lineLength);
        Vector3 pursuitPoint = lineStart + lineDir * pursuitProgress;
        pursuitPoint.y = 0; // keep flat
        
        float distanceToEnd = lineLength - progressAlongLine;

        // ── Overshoot recovery: rover has passed the endpoint — reverse back ─
        if (distanceToEnd < 0f)
        {
            // Keep heading aligned with the line; drive in reverse to return
            float lineHeading = Mathf.Atan2(lineDir.x, lineDir.z) * Mathf.Rad2Deg;
            float reverseHeadingError = Mathf.DeltaAngle(transform.eulerAngles.y, lineHeading);

            if (Time.frameCount % 30 == 0)
                Debug.Log($"<color=orange>[LINE] OVERSHOOT — {-distanceToEnd:F2}m past end. Reversing... alignErr={reverseHeadingError:F1}°</color>");

            // Steer to keep aligned with line direction while going in reverse
            turnCmd = ApplyDeadband(Mathf.Clamp(reverseHeadingError / 30f, -1f, 1f) * turnSpeed, minTurnCmd);
            forwardCmd = -ApplyDeadband(moveSpeed * 0.9f, minForwardCmd); // always reverse
            return;
        }
        // ──────────────────────────────────────────────────────────────────

        // ── Phase 2: near endpoint — switch to pure targetHeading alignment ──
        // Don't use pursuit-point direction here: the vector is tiny (~0.05m)
        // so any lateral offset causes wildly noisy heading errors.
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

            // Creep forward while still meaningfully far from endpoint so the rover 
            // doesn't stall on inertia during heading alignment. Stop creeping at 0.1m.
            if (distanceToEnd > 0.1f)
                forwardCmd = ApplyDeadband(moveSpeed * 0.3f, minForwardCmd);

            if (Time.frameCount % 30 == 0)
                Debug.Log($"[LINE P2] DistToEnd: {distanceToEnd:F2}m, FinalHeadingErr: {finalHeadingError:F1}°");
            return;
        }

        // ── Phase 1: following the line via pursuit point ─────────────────
        Vector3 toPursuit = pursuitPoint - transform.position;
        toPursuit.y = 0;
        float desiredHeading = Mathf.Atan2(toPursuit.x, toPursuit.z) * Mathf.Rad2Deg;
        float headingError = Mathf.DeltaAngle(transform.eulerAngles.y, desiredHeading);

        if (Mathf.Abs(headingError) > angleTolerance)
            turnCmd = ApplyDeadband(Mathf.Clamp(headingError / 30f, -1f, 1f) * turnSpeed, minTurnCmd);

        if (Mathf.Abs(headingError) < 30f)
        {
            float speedMult = Mathf.Clamp01(distanceToEnd / (stopDistance * 2));
            float finalSpeedMult = isFinalGoal ? 0.5f : 1.0f;
            forwardCmd = ApplyDeadband(moveSpeed * Mathf.Max(speedMult, 0.5f) * finalSpeedMult, minForwardCmd);
        }

        if (Time.frameCount % 30 == 0)
            Debug.Log($"[LINE P1] Progress: {progressAlongLine:F2}/{lineLength:F2}m, DistToEnd: {distanceToEnd:F2}m, HeadingErr: {headingError:F1}°");
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
        if (curvePoints.Count == 0) return;

        Vector3 currentPos = transform.position;
        currentPos.y = 0;

        // ── Phase 2: rotate in place to final heading ──────────────────
        if (curveAligning)
        {
            float finalHeadingError = Mathf.DeltaAngle(transform.eulerAngles.y, targetHeading);
            float alignElapsed = Time.time - curveAlignStartTime;

            // Success: heading is within tolerance
            if (Mathf.Abs(finalHeadingError) <= angleTolerance)
            {
                Debug.Log($"<color=white>Curve complete — heading aligned! (took {alignElapsed:F1}s)</color>");
                curveAligning = false;
                StopRobot();
                hasGoal = false;
            }
            // Timeout: accept current heading and move on
            else if (alignElapsed > curveAlignmentTimeout)
            {
                Debug.LogWarning($"[CURVE P2] Alignment timeout after {curveAlignmentTimeout}s (error: {finalHeadingError:F1}°). Accepting current heading.");
                curveAligning = false;
                StopRobot();
                hasGoal = false;
            }
            else
            {
                // Apply deadband so turn never drops below the physical rotation threshold
                float rawTurn = Mathf.Clamp(finalHeadingError / 20f, -1f, 1f) * turnSpeed;
                turnCmd = ApplyDeadband(rawTurn, minTurnCmd);
                // forwardCmd stays 0
            }

            if (Time.frameCount % 30 == 0)
                Debug.Log($"[CURVE P2] Heading err: {finalHeadingError:F1}°, elapsed: {alignElapsed:F1}s");
            return;
        }

        // ── Phase 1: follow the curve ──────────────────────────────────
        float closestDist = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < curvePoints.Count; i++)
        {
            Vector3 cp = curvePoints[i];
            cp.y = 0;
            float dist = Vector3.Distance(currentPos, cp);
            if (dist < closestDist) { closestDist = dist; closestIndex = i; }
        }

        float currentDistance = curveDistances[closestIndex];
        float distanceToEnd   = totalCurveLength - currentDistance;

        // Also check straight-line distance to the actual endpoint
        Vector3 endFlat = p3; endFlat.y = 0;
        float spatialDistToEnd = Vector3.Distance(currentPos, endFlat);

        // Transition to Phase 2: align heading at end of ALL curves
        if (distanceToEnd <= stopDistance && spatialDistToEnd <= stopDistance * 2f)
        {
            Debug.Log($"<color=white>Curve reached end — aligning heading (final={isFinalGoal})</color>");
            curveAligning = true;
            curveAlignStartTime = Time.time;
            // Zero motors WITHOUT calling StopRobot() — that would immediately clear
            // curveAligning and hasGoal, killing Phase 2 before it can run even one frame.
            if (controller != null) { controller.rosForward = 0f; controller.rosTurn = 0f; }
            return;
        }

        // Pursuit point
        float targetDistance = currentDistance + lookAheadDistance;
        int pursuitIndex = closestIndex;
        for (int i = closestIndex; i < curvePoints.Count; i++)
        {
            pursuitIndex = i;
            if (curveDistances[i] >= targetDistance) break;
        }

        Vector3 pursuitPoint = curvePoints[pursuitIndex];
        pursuitPoint.y = currentPos.y;

        Vector3 toPursuit = pursuitPoint - currentPos;
        toPursuit.y = 0;

        float desiredHeading = Mathf.Atan2(toPursuit.x, toPursuit.z) * Mathf.Rad2Deg;
        float headingError   = Mathf.DeltaAngle(transform.eulerAngles.y, desiredHeading);

        if (Mathf.Abs(headingError) > angleTolerance)
            turnCmd = ApplyDeadband(Mathf.Clamp(headingError / 20f, -1f, 1f) * turnSpeed, minTurnCmd);

        if (Mathf.Abs(headingError) < 20f)
        {
            float speedMult      = Mathf.Clamp01(distanceToEnd / (stopDistance * 2));
            float finalSpeedMult = isFinalGoal ? 0.5f : 1.0f;
            forwardCmd = ApplyDeadband(0.2f * Mathf.Max(speedMult, 0.5f) * finalSpeedMult, minForwardCmd);
        }

        if (Time.frameCount % 30 == 0)
            Debug.Log($"[CURVE P1] Dist: {currentDistance:F2}/{totalCurveLength:F2}m, ToEnd: {distanceToEnd:F2}m, Spatial: {spatialDistToEnd:F2}m, Heading err: {headingError:F1}°");
    }

    float ApplyDeadband(float input, float minPower)
    {
        if (Mathf.Abs(input) < 0.01f) return 0;
        return Mathf.Sign(input) * Mathf.Max(Mathf.Abs(input), minPower);
    }

    public void StopRobot() 
    { 
        curveAligning = false;
        hasGoal = false;
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
        curveAligning = false; 
        ClearPathVisualization();
    }

    public void RotateInPlace(float targetHeading)
    {
        rotatingInPlace     = true;
        rotateTargetHeading = targetHeading;
        hasGoal             = true;
        useLineFollowing    = false;
        useCurveFollowing   = false;
        curveAligning       = false;
        Debug.Log($"<color=cyan>[NAV] RotateInPlace → {targetHeading:F0}°</color>");
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
        curveAligning = false;
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
        curveAligning = false;
        
        // Generate Bezier curve
        GenerateBezierCurve(start, end, endHeading);
        hasGoal = true;
        
        Debug.Log($"<color=magenta>[CURVE GOAL] Start: {start}, End: {end}, EndHeading: {endHeading:F1}°</color>");
    }

    public void SetObstacleAvoidanceGoal(Vector3 start, Vector3 end, Vector3 obstaclePos, float endHeading, bool avoidLeft = true, bool finalGoal = false)
    {
        targetPosition = end;
        targetHeading = endHeading;
        isFinalGoal = finalGoal;
        useLineFollowing = false;
        useCurveFollowing = true;
        curveAligning = false;

        p0 = start; p0.y = 0;
        p3 = end;   p3.y = 0;

        Vector3 pathDir = (p3 - p0).normalized;
        Vector3 lateral = Vector3.Cross(Vector3.up, pathDir) * (avoidLeft ? 1f : -1f);

        float startHeading = transform.eulerAngles.y;
        float d = Vector3.Distance(p0, p3) / 2f;

        float phi   = startHeading * Mathf.Deg2Rad;
        float theta = endHeading   * Mathf.Deg2Rad;

        // Base control points from headings
        p1 = p0 + d * new Vector3(Mathf.Sin(phi),  0, Mathf.Cos(phi));
        p2 = p3 - d * new Vector3(Mathf.Sin(theta), 0, Mathf.Cos(theta));

        // Project obstacle onto the path line
        Vector3 toObstacle = (obstaclePos - p0);
        toObstacle.y = 0;
        float projectedDist = Vector3.Dot(toObstacle, pathDir);
        Vector3 closestPointOnPath = p0 + pathDir * projectedDist;

        // How far is the rock from the straight line path
        float lateralOffset = Vector3.Distance(closestPointOnPath, new Vector3(obstaclePos.x, 0, obstaclePos.z));

        // We want the curve to pass at least rockClearance meters to the side
        float rockClearance = 3.5f; // tune this — meters of clearance from rock edge
        float requiredPush = Mathf.Max(0f, rockClearance - lateralOffset) + rockClearance;

        Debug.Log($"[AVOID] lateralOffset={lateralOffset:F2}, requiredPush={requiredPush:F2}");

        // Push both control points sideways — this is what actually bends the arc
        p1 += lateral * requiredPush;
        p2 += lateral * requiredPush;

        // Rebuild curve
        curvePoints.Clear();
        curveDistances.Clear();
        totalCurveLength = 0f;
        Vector3 prev = p0;

        for (int i = 0; i <= curveResolution; i++)
        {
            float t = (float)i / curveResolution;
            Vector3 point = GetBezierPoint(t);
            curvePoints.Add(point);
            if (i > 0) totalCurveLength += Vector3.Distance(prev, point);
            curveDistances.Add(totalCurveLength);
            prev = point;
        }

        hasGoal = true;
        VisualizeCurve();

        Debug.Log($"<color=green>[AVOID] Rock at {obstaclePos}, push={requiredPush:F2}m {(avoidLeft ? "left" : "right")}</color>");
    }

    void GenerateBezierCurve(Vector3 start, Vector3 end, float endHeading)
    {
        p0 = start;
        p3 = end;
        
        // Calculate start heading from current orientation
        float startHeading = transform.eulerAngles.y;
        
        float phi   = startHeading * Mathf.Deg2Rad;
        float theta = endHeading   * Mathf.Deg2Rad;

        Vector3 startFwd = new Vector3(Mathf.Sin(phi),   0, Mathf.Cos(phi));
        Vector3 endFwd   = new Vector3(Mathf.Sin(theta), 0, Mathf.Cos(theta));

        Vector3 chord    = p3 - p0; chord.y = 0;
        float   chordLen = chord.magnitude;
        Vector3 chordDir = chordLen > 0.01f ? chord.normalized : startFwd;

        // alignFwd = 1 → heading matches chord direction (straight-ahead, no S risk)
        // alignFwd ≈ 0 → mostly lateral move (high S risk with heading-only control points)
        float alignFwd = Vector3.Dot(startFwd, chordDir);

        // Shorten control arms when lateral — long arms amplify the S inflection.
        // Full alignment → 40% of chord length; pure lateral → 25%.
        float d = chordLen * Mathf.Lerp(0.25f, 0.40f, Mathf.Clamp01(alignFwd));

        // Blend control-point directions toward the chord as the move becomes more lateral.
        // This converts the S into a smooth C-arc.
        float lateralBlend = Mathf.Clamp01(1f - Mathf.Abs(alignFwd));
        float blendAmount  = lateralBlend * 0.65f;

        Vector3 p1Dir = Vector3.Normalize(Vector3.Lerp(startFwd, chordDir, blendAmount));
        Vector3 p2Dir = Vector3.Normalize(Vector3.Lerp(endFwd,   chordDir, blendAmount));

        p1 = p0 + d * p1Dir;
        p2 = p3 - d * p2Dir;

        Debug.Log($"[CURVE GEN] chordLen={chordLen:F1}m, alignFwd={alignFwd:F2}, blend={blendAmount:F2}, d={d:F2}m");
        
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

    [ContextMenu("Test: Avoid Obstacle")]
    void TestObstacleAvoidance()
    {
        Vector3 start = transform.position;
        SetObstacleAvoidanceGoal(start, testAvoidEndPoint, testObstaclePos, testAvoidEndHeading, testAvoidLeft);
        Debug.Log($"<color=green>TEST: Avoiding obstacle at {testObstaclePos}, going to {testAvoidEndPoint}</color>");
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

        // Add inside OnDrawGizmos()
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(testObstaclePos, 0.3f);
        Gizmos.DrawWireSphere(testAvoidEndPoint, 0.3f);

        Vector3 avoidHeadingDir = Quaternion.Euler(0, testAvoidEndHeading, 0) * Vector3.forward;
        Gizmos.color = Color.green;
        Gizmos.DrawRay(testAvoidEndPoint, avoidHeadingDir * 1.0f);

        #if UNITY_EDITOR
        UnityEditor.Handles.Label(testObstaclePos + Vector3.up * 0.5f, "Obstacle");
        UnityEditor.Handles.Label(testAvoidEndPoint + Vector3.up * 0.5f, 
            $"Avoid End\n{testAvoidEndHeading:F0}° {(testAvoidLeft ? "←" : "→")}");
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