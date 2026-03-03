using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using RosMessageTypes.Geometry;
using RosMessageTypes.BuiltinInterfaces;

public class RoverROSComms : MonoBehaviour
{
    [Header("References")]
    public PlateSequenceManager sequenceManager;
    public Transform roverTransform; // The actual rover for position tracking
    public MultiRopeDeployer ropeDeployer; 
    public SimpleTruckNav truckNav;  // ADD THIS


    
    [Header("ROS Topics")]
    public string plateLocationsTopicName = "rover/plate_locations";
    public string roverStatusTopicName = "rover/status";
    public string moveCommandTopicName = "rover/move_command";
    public string ropeDeployTopicName = "rover/deploy_rope";
    public string waypointTopicName = "rover/waypoint";
    public string curvedGoalTopicName = "rover/curved_goal";
    public string obstacleAvoidTopicName = "rover/avoid_obstacle";
    public string teleportTopicName = "rover/teleport";
    public string rotateToHeadingTopicName = "rover/rotate_to_heading";




    [Header("Manual Test")]
    public Vector3 testWaypoint;

    [ContextMenu("Test: Go To Manual Waypoint")]
    void TestGoToWaypoint() 
    {
        if (sequenceManager != null)
        {
            sequenceManager.GoToPosition(testWaypoint);
        }
    }
    
    [Header("Status (Read Only)")]
    public int currentPlateIndex = -1;
    public bool isAtPlate = false;
    public bool isNavigating = false;
    
    // Internal
    private ROSConnection ros;
    private bool plateLocationsSent = false;
    private float lastStatusPublishTime = 0f;
    private float statusPublishInterval = 0.1f; // 10Hz
    void Awake()  // Changed from Start()
    {
        // Get ROS connection
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PoseArrayMsg>(plateLocationsTopicName);
        ros.RegisterPublisher<StringMsg>(roverStatusTopicName);
        ros.Subscribe<BoolMsg>(ropeDeployTopicName, OnRopeCommandReceived);
        ros.Subscribe<PoseMsg>(waypointTopicName, OnWaypointReceived);
        ros.Subscribe<PoseMsg>(curvedGoalTopicName, OnCurvedGoalReceived);
        ros.Subscribe<PoseMsg>(teleportTopicName, OnTeleportReceived);
        ros.Subscribe<Float32Msg>(rotateToHeadingTopicName, OnRotateToHeadingReceived);


        
        // Subscribe to move commands
        ros.Subscribe<Int32Msg>(moveCommandTopicName, OnMoveCommandReceived);
        ros.Subscribe<PoseMsg>(obstacleAvoidTopicName, OnObstacleAvoidReceived);

        
        Debug.Log("<color=cyan>[ROS] RoverROSComms initialized</color>");
    }


    private void OnRotateToHeadingReceived(Float32Msg msg)
    {
        if (truckNav == null) { Debug.LogError("[ROS] SimpleTruckNav is null!"); return; }
        truckNav.RotateInPlace((float)msg.data);
        isNavigating = true;
        Debug.Log($"<color=cyan>[ROS] 🧭 Rotate-in-place to {(float)msg.data:F0}°</color>");
    }


    private void OnTeleportReceived(PoseMsg msg)
    {
        ArticulationBody ab = roverTransform.GetComponent<ArticulationBody>();

        // Mapping ROS -> Unity
        // ROS (x, y, z) -> Unity (x, z, y) based on your SendPlateLocations mapping
        Vector3 targetPos = new Vector3(
            (float)msg.position.x,
            (float)msg.position.z, 
            (float)msg.position.y
        );

        // Convert ROS Quaternion to Unity Quaternion
        // We swap y/z and negate w to handle the coordinate system hand-change
        Quaternion targetRot = new Quaternion(
            (float)msg.orientation.x,
            (float)msg.orientation.z,
            (float)msg.orientation.y,
            -(float)msg.orientation.w
        );

        if (ab != null)
        {
            // TeleportRoot is essential for ArticulationBodies
            ab.TeleportRoot(targetPos, targetRot);
            
            // Kill any existing momentum so it doesn't "drift" after teleporting
            ab.linearVelocity = Vector3.zero;
            ab.angularVelocity = Vector3.zero;
        }
        else
        {
            roverTransform.SetPositionAndRotation(targetPos, targetRot);
        }

        // Stop navigation so ROS isn't waiting for an arrival that won't happen
        if (truckNav != null) { truckNav.hasGoal = false; truckNav.StopRobot(); }
        isNavigating = false;

        Debug.Log($"<color=cyan>[ROS] 🚀 Articulation Teleported to {targetPos} with valid rotation.</color>");
    }


    private void OnCurvedGoalReceived(PoseMsg msg)
    {
        if (truckNav == null)
        {
            Debug.LogError("[ROS] SimpleTruckNav reference is null!");
            return;
        }
        Vector3 endPoint = new Vector3(
            (float)msg.position.x,
            (float)msg.position.y,
            (float)msg.position.z
        );
        float endHeading = (float)msg.orientation.z;  // heading packed in degrees
        bool isFinal = msg.orientation.w > 0.5f;
        Vector3 startPoint = truckNav.transform.position;
        truckNav.SetCurvedGoal(startPoint, endPoint, endHeading, isFinal);
        // Flag navigating so ROS wait_for_unity_arrival() works
        isNavigating = true;
        Debug.Log($"<color=magenta>[ROS] Curved goal received: {endPoint}, heading={endHeading}°, final={isFinal}</color>");
    }

    private void OnObstacleAvoidReceived(PoseMsg msg)
    {
        if (truckNav == null)
        {
            Debug.LogError("[ROS] SimpleTruckNav reference is null!");
            return;
        }

        // position = obstacle world position
        // orientation.z = end heading (degrees)
        // orientation.x = avoidLeft (1 = left, 0 = right)
        // orientation.w = isFinal

        Vector3 obstaclePos = new Vector3(
            (float)msg.position.x,
            (float)msg.position.y,
            (float)msg.position.z
        );

        // End point is packed separately — we need a destination
        // Convention: send a second waypoint via curved_goal first, or pack end into header
        // Here we derive end point as X meters past the obstacle along current heading
        float passDistance = 4f; // how far past the rock to target
        Vector3 forward = truckNav.transform.forward;
        Vector3 startPoint = truckNav.transform.position;
        Vector3 endPoint = obstaclePos + forward * passDistance;

        float endHeading = (float)msg.orientation.z;
        bool avoidLeft = msg.orientation.x > 0.5f;
        bool isFinal = msg.orientation.w > 0.5f;

        truckNav.SetObstacleAvoidanceGoal(startPoint, endPoint, obstaclePos, endHeading, avoidLeft, isFinal);
        isNavigating = true;

        Debug.Log($"<color=green>[ROS] Obstacle avoidance received: rock at {obstaclePos}, avoid {(avoidLeft ? "left" : "right")}, heading={endHeading}°</color>");
    }

    private void OnWaypointReceived(RosMessageTypes.Geometry.PoseMsg msg)
    {
        Vector3 targetPos = new Vector3((float)msg.position.x, (float)msg.position.y, (float)msg.position.z);
        
        // Convert ROS orientation to Unity
        Quaternion rosRotation = new Quaternion(
            (float)msg.orientation.x,
            (float)msg.orientation.y,
            (float)msg.orientation.z,
            (float)msg.orientation.w
        );

        // Check if orientation is provided (not just a default/empty quaternion)
        // 0.1 degree tolerance is safe for floating point errors
        float? targetHeading = null;
        if (Quaternion.Angle(rosRotation, Quaternion.identity) > 0.1f)
        {
            targetHeading = rosRotation.eulerAngles.y;
        }

        if (sequenceManager != null)
        {
            // Pass the optional heading to the manager
            sequenceManager.GoToPosition(targetPos, targetHeading);
            
            string mode = targetHeading.HasValue ? $"Curve to {targetHeading}°" : "Straight Line";
            Debug.Log($"<color=cyan>[ROS] Waypoint received ({mode}): {targetPos}</color>");
        }
    }
    void Update()
    {
        // Detect when curved navigation completes
        if (isNavigating && truckNav != null && !truckNav.hasGoal)
        {
            isNavigating = false;
            Debug.Log("<color=green>[ROS] ✓ Curved navigation complete</color>");
        }
        // Publish status at fixed interval (existing code)
        if (Time.time - lastStatusPublishTime >= statusPublishInterval)
        {
            PublishRoverStatus();
            lastStatusPublishTime = Time.time;
        }
    }
    
    // ==================== PUBLIC API ====================

    private void OnRopeCommandReceived(BoolMsg msg)
    {
        if (ropeDeployer != null)
        {
            ropeDeployer.isDeploying = msg.data;
            Debug.Log($"<color=magenta>[ROS] Rope Deployment: {msg.data}</color>");
        }
    }
    
    /// <summary>
    /// Send all plate positions to ROS (called once at startup)
    /// </summary>
    public void SendPlateLocationsToROS(Vector3[] platePositions)
    {
        if (plateLocationsSent)
        {
            Debug.LogWarning("[ROS] Plate locations already sent");
            return;
        }
        
        Debug.Log($"<color=cyan>[ROS] Sending {platePositions.Length} plate positions to ROS</color>");
        
        // Create PoseArray message
        PoseArrayMsg poseArray = new PoseArrayMsg();
        poseArray.header = new HeaderMsg
        {
            stamp = new TimeMsg
            {
                sec = (int)Time.time,
                nanosec = (uint)((Time.time - (int)Time.time) * 1e9)
            },
            frame_id = "world"
        };
        
        // Convert Unity positions to ROS poses
        poseArray.poses = new PoseMsg[platePositions.Length];
        for (int i = 0; i < platePositions.Length; i++)
        {
            Vector3 pos = platePositions[i];
            poseArray.poses[i] = new PoseMsg
            {
                position = new PointMsg
                {
                    x = pos.x,
                    y = pos.z,
                    z = pos.y
                },
                orientation = new QuaternionMsg { x = 0, y = 0, z = 0, w = 1 }
            };
        }
        Debug.Log($"{poseArray}");

        ros.Publish(plateLocationsTopicName, poseArray);
        plateLocationsSent = true;
        
        Debug.Log("<color=green>[ROS] ✓ Plate locations sent</color>");
    }
    
    /// <summary>
    /// Notify ROS that rover has arrived at a plate
    /// </summary>
    public void NotifyPlateReached(int plateIndex)
    {
        currentPlateIndex = plateIndex;
        isAtPlate = true;
        isNavigating = false;
        
        Debug.Log($"<color=green>[ROS] ✓ Arrived at plate {plateIndex}</color>");
        PublishRoverStatus(); // Send immediate update
    }
    
    /// <summary>
    /// Notify ROS that navigation has started
    /// </summary>
    public void NotifyNavigationStarted(int plateIndex)
    {
        currentPlateIndex = plateIndex;
        isAtPlate = false;
        isNavigating = true;
        
        Debug.Log($"<color=yellow>[ROS] → Navigating to plate {plateIndex}</color>");
        PublishRoverStatus();
    }
    
    // ==================== PRIVATE ====================
    
    private void PublishRoverStatus()
    {
        if (roverTransform == null) return;
        
        Vector3 pos = roverTransform.position;
        
        // Format: "plateIndex,isAtPlate,isNavigating,x,y,z"
        string statusStr = $"{currentPlateIndex},{isAtPlate},{isNavigating},{pos.x:F3},{pos.y:F3},{pos.z:F3}";
        
        StringMsg statusMsg = new StringMsg { data = statusStr };
        ros.Publish(roverStatusTopicName, statusMsg);
    }
    
    private void OnMoveCommandReceived(Int32Msg msg)
    {
        int targetPlate = msg.data;
        
        Debug.Log($"<color=yellow>[ROS] ◄ Received command: Go to plate {targetPlate}</color>");
        
        if (sequenceManager != null)
        {
            sequenceManager.GoToPlate(targetPlate);
        }
        else
        {
            Debug.LogError("[ROS] SequenceManager reference is null!");
        }
    }
    
    // ==================== TESTING ====================
    
    [ContextMenu("Test: Send to Plate 0")]
    void TestGoToPlate0() { OnMoveCommandReceived(new Int32Msg { data = 0 }); }
    
    [ContextMenu("Test: Send to Plate 1")]
    void TestGoToPlate1() { OnMoveCommandReceived(new Int32Msg { data = 1 }); }
    
    [ContextMenu("Test: Send to Plate 2")]
    void TestGoToPlate2() { OnMoveCommandReceived(new Int32Msg { data = 2 }); }
    
    [ContextMenu("Test: Send to Plate 3")]
    void TestGoToPlate3() { OnMoveCommandReceived(new Int32Msg { data = 3 }); }
    
    [ContextMenu("Test: Run Full Auto Sequence")]
    void TestFullAuto() 
    { 
        if (sequenceManager != null)
            sequenceManager.StartFullAutoSequence();
    }
}