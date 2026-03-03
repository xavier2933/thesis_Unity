using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;

public class TruckAndArmController : MonoBehaviour
{
    // ================================
    // === ARM TARGET TELEOP ==========
    // ================================
    [Header("Arm Teleop Settings")]
    public string targetPoseTopic = "/unity_target_pose";
    public string gripperTopic = "/gripper_command";
    public string wristTopic = "/wrist_angle";
    public string targetPoseSubTopic = "/target_pose";

    private Vector3 lastReceivedPosition;
    private Quaternion lastReceivedRotation;
    private bool receivedPose = false;
    public Transform armBaseTransform;
    public float scaleFactorUnityToROS = 1.0f;
    public float armMoveSpeed = 0.005f;
    public float armRotateSpeed = 1.0f; // degrees per frame

    private Vector3 lastPublishedPosition;
    private Quaternion lastPublishedRotation;
    // private Vector3 resetPositionLocal = new Vector3(0.672f, 0.669f, 0.0f);
    private Quaternion resetRotationLocal = Quaternion.identity;
    private bool gripperClosed = false;
    private bool lastFrameGripperClosed = false;


    // ================================
    // === TRUCK DRIVE ===============
    // ================================
    [System.Serializable]
    public class WheelGroup
    {
        public ArticulationBody[] wheels;
    }

    [Header("Drive Settings")]
    public WheelGroup leftTrack;
    public WheelGroup rightTrack;
    public float driveForce = 1000f;
    public float maxSpeed = 500f;

    [Header("ROS Input Settings")]
    public string cmdVelTopic = "/cmd_vel";
    public string autonomousModeTopic = "/autonomous_mode";
    public bool useROSInput = true;

    public float rosForward = 0f;
    public float rosTurn = 0f;

    private ROSConnection ros;
    private float wristTwistAngle = 0f;
    private float wristTwistSpeed = 1.0f;

    [Header("Reset")]
    // these are world coords relative to base???
    
    public float armRotationVariance = 10f; // Range of random rotation for the arm
    public Vector3 resetPositionLocal = new Vector3(0.3f, 0.3f, 0.3f);
    public float armPosOffset = 0.05f;
    public Vector3 resetRotationEuler = new Vector3(0f, 0f, 0f); // <-- define in degrees
    public Vector3 bloccResetPosition = new Vector3(1.781433f, -1.335353f, -0.5479906f);
    public Vector3 bloccResetRotationEuler = new Vector3(90f, -90f, 0f); // <-- define in degrees
    public GameObject blocc;
    public GameObject bloccParent;
    public float bloccPosVariance = 0.2f; // How much X and Z can shift
    public float bloccRotVariance = 360f; // Randomize Y rotation (0-360)
    public string resetTopic = "/reset_env";


    private float posePublishInterval = 0.1f;  // 10 Hz
    private float posePublishTimer = 0f;


    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        lastPublishedPosition = transform.position;
        lastPublishedRotation = transform.rotation;

        ros.RegisterPublisher<PoseMsg>(targetPoseTopic);
        ros.RegisterPublisher<BoolMsg>(gripperTopic);
        ros.RegisterPublisher<Float32Msg>(wristTopic);

        ros.Subscribe<PoseMsg>(targetPoseSubTopic, TargetPoseCallback);
        ros.Subscribe<BoolMsg>(resetTopic, ResetCallback);


        if (useROSInput)
            ros.Subscribe<TwistMsg>(cmdVelTopic, CmdVelCallback);

        ros.Subscribe<BoolMsg>(autonomousModeTopic, OnAutonomousModeReceived);

        if (armBaseTransform == null)
            Debug.LogError("Arm Base Transform not assigned! Drag your arm base link here.");

        InitWheelGroup(leftTrack);
        InitWheelGroup(rightTrack);
    }

    void Update()
    {
        HandleTruckControls();
        HandleTeleopControls();
        posePublishTimer += Time.deltaTime;
        if (posePublishTimer >= posePublishInterval)
        {
            PublishCurrentPose();
            ros.Publish(wristTopic, new Float32Msg(wristTwistAngle));

            posePublishTimer = 0f;
        }

        if (gripperClosed != lastFrameGripperClosed && 
            !Input.GetKeyDown(KeyCode.LeftBracket) && 
            !Input.GetKeyDown(KeyCode.RightBracket))
        {
            Debug.LogError($"[BUG] Gripper state changed unexpectedly! Was: {lastFrameGripperClosed}, Now: {gripperClosed}");
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            DoReset();
        }
        lastFrameGripperClosed = gripperClosed;
    }

    private void ResetCallback(BoolMsg msg)
    {
        if (msg.data == true)
        {
            DoReset();
        }
    }

    private void DoReset()
    {
        // --- 1. Reset and Randomize Arm ---
        // Create a random rotation offset for the arm
        
        Quaternion armResetRotation = Quaternion.Euler(resetRotationEuler);        
        // Arm Position Randomization (Existing logic)
        Vector3 armRandomOffset = new Vector3(
            Random.Range(-armPosOffset, armPosOffset),
            Random.Range(-armPosOffset, armPosOffset),
            Random.Range(-armPosOffset, armPosOffset)
        );
        
        Vector3 basePosition = armBaseTransform.TransformPoint(resetPositionLocal);
        transform.position = basePosition + armRandomOffset;
        
        // Apply base rotation PLUS the random variance
        transform.rotation = armBaseTransform.rotation * armResetRotation;        
        PublishCurrentPose();

        // --- 2. Reset and Randomize Blocc ---
        // if (blocc != null)
        // {
        //     Rigidbody rb = blocc.GetComponent<Rigidbody>();
        //     if (rb != null)
        //     {
        //         rb.linearVelocity = Vector3.zero;
        //         rb.angularVelocity = Vector3.zero;

        //         // Transform parent = blocc.transform.parent;
        //         Transform parent = bloccParent.transform;

        //         // Randomize X and Z locally
        //         float randX = Random.Range(-bloccPosVariance, bloccPosVariance);
        //         float randZ = Random.Range(-bloccPosVariance, bloccPosVariance);
        //         Vector3 randomizedLocalPos = bloccResetPosition + new Vector3(0, randZ, randX);

        //         // Randomize Rotation (primarily around the vertical axis for a block)
        //         float randRotY = Random.Range(-bloccRotVariance, bloccRotVariance);
        //         Quaternion randomizedLocalRot = Quaternion.Euler(bloccResetRotationEuler) * Quaternion.Euler(randRotY, 0, 0);

        //         // Convert to World Space
        //         Vector3 worldResetPos = parent.TransformPoint(randomizedLocalPos);
        //         Quaternion worldResetRot = parent.rotation * randomizedLocalRot;

        //         rb.MovePosition(worldResetPos);
        //         rb.MoveRotation(worldResetRot);

        //         Debug.Log($"[Teleop] Blocc reset with Pos Var: ({randX}, {randZ}) and Rot Var: {randRotY}");
        //     }
        // }
    }    
    

    
    void TargetPoseCallback(PoseMsg msg)
    {
        Vector3 newPosition = new Vector3(
            (float)msg.position.x,
            (float)msg.position.y,
            (float)msg.position.z
        ) / scaleFactorUnityToROS;

        Quaternion newRotation = new Quaternion(
            (float)msg.orientation.x,
            (float)msg.orientation.y,
            (float)msg.orientation.z,
            (float)msg.orientation.w
        );

        // Only teleport if pose actually changed
        if (!receivedPose || 
            Vector3.Distance(newPosition, lastReceivedPosition) > 0.001f ||
            Quaternion.Angle(newRotation, lastReceivedRotation) > 0.1f)
        {
            // Transform from arm base frame to Unity world frame if needed
            transform.position = armBaseTransform.TransformPoint(newPosition);
            transform.rotation = armBaseTransform.rotation * newRotation;

            lastReceivedPosition = newPosition;
            lastReceivedRotation = newRotation;
            receivedPose = true;
        }
    }

    // ================================
    // === TRUCK DRIVE ===============
    // ================================
    void HandleTruckControls()
    {
        float forward = Input.GetAxis("Vertical");
        float turn = Input.GetAxis("Horizontal");

        if (useROSInput)
        {
            forward = Mathf.Abs(rosForward) > 0.01f ? rosForward : forward;
            turn = Mathf.Abs(rosTurn) > 0.01f ? rosTurn : turn;
        }

        float leftInput = forward + turn;
        float rightInput = forward - turn;
        // Debug.Log($"[TruckControl] Forward: {forward:F3}, Turn: {turn:F3}, Left: {leftInput:F3}, Right: {rightInput:F3}");

        SetTrackMotor(leftTrack, leftInput);
        SetTrackMotor(rightTrack, rightInput);
    }

    void InitWheelGroup(WheelGroup track)
    {
        foreach (var wheel in track.wheels)
        {
            if (wheel == null) continue;
            wheel.jointType = ArticulationJointType.RevoluteJoint;
            wheel.twistLock = ArticulationDofLock.FreeMotion;
            wheel.swingYLock = ArticulationDofLock.LockedMotion;
            wheel.swingZLock = ArticulationDofLock.LockedMotion;

            var drive = wheel.xDrive;
            drive.forceLimit = driveForce;
            drive.targetVelocity = 0f;
            wheel.xDrive = drive;
        }
    }

    void SetTrackMotor(WheelGroup track, float input)
    {
        foreach (var wheel in track.wheels)
        {
            if (wheel == null) continue;
            var drive = wheel.xDrive;
            drive.targetVelocity = input * maxSpeed;
            drive.forceLimit = driveForce;
            wheel.xDrive = drive;
        }
    }

    void CmdVelCallback(TwistMsg msg)
    {
        // rosForward = (float)msg.linear.x;
        // rosTurn = (float)msg.angular.z;
    }

    void OnAutonomousModeReceived(BoolMsg msg)
    {
        useROSInput = msg.data;
        Debug.Log($"<color=cyan>[TruckCtrl] Autonomous mode: {msg.data} → useROSInput={useROSInput}</color>");
    }

    // ================================
    // === ARM TELEOP ================
    // ================================
    void HandleTeleopControls()
    {
        // === WRIST TWIST INPUT ( ; , ' ) ===
        bool wristMoved = false;
        if (Input.GetKey(KeyCode.Semicolon))
        {
            wristTwistAngle += wristTwistSpeed;
            wristMoved = true;
        }
        if (Input.GetKey(KeyCode.Quote))
        {
            wristTwistAngle -= wristTwistSpeed;
            wristMoved = true;
        }


        wristTwistAngle %= 360f;

        // === POSITION CONTROLS (I, J, K, L, U, O) ===
        Vector3 delta = Vector3.zero;
        if (Input.GetKey(KeyCode.I)) delta.z += armMoveSpeed;
        if (Input.GetKey(KeyCode.K)) delta.z -= armMoveSpeed;
        if (Input.GetKey(KeyCode.J)) delta.x -= armMoveSpeed;
        if (Input.GetKey(KeyCode.L)) delta.x += armMoveSpeed;
        if (Input.GetKey(KeyCode.U)) delta.y += armMoveSpeed;
        if (Input.GetKey(KeyCode.O)) delta.y -= armMoveSpeed;
        transform.position += delta;

        // === GRIPPER CONTROL ([ and ]) ===
        bool gripperPublished = false;
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            gripperClosed = false;
            gripperPublished = true;
            PublishGripperState();
            Debug.Log("[Gripper] Opened");
        }
        if (Input.GetKeyDown(KeyCode.RightBracket))
        {
            gripperClosed = true;
            gripperPublished = true;
            PublishGripperState();
            Debug.Log("[Gripper] Closed");
        }

        // === PUBLISH IF MOVED OR ROTATED ===
        if ((transform.position - lastPublishedPosition).magnitude > 0.01f ||
            wristMoved == true)
        {
            PublishCurrentPose();
            ros.Publish(wristTopic, new Float32Msg(wristTwistAngle));
        }
        if (gripperPublished == false)
        {
            PublishGripperState();
        }
    }


    void PublishCurrentPose()
    {
        if (armBaseTransform == null) return;

        Vector3 localPosition = armBaseTransform.InverseTransformPoint(transform.position);
        Quaternion localRotation = Quaternion.Inverse(armBaseTransform.rotation) * transform.rotation;
        Vector3 rosPosition = localPosition * scaleFactorUnityToROS;

        PoseMsg pose = new PoseMsg
        {
            position = new PointMsg(rosPosition.x, rosPosition.y, rosPosition.z),
            orientation = new QuaternionMsg(localRotation.x, localRotation.y, localRotation.z, localRotation.w)
        };

        ros.Publish(targetPoseTopic, pose);
        lastPublishedPosition = transform.position;
        lastPublishedRotation = transform.rotation;
    }

    void PublishGripperState()
    {
        ros.Publish(gripperTopic, new BoolMsg(gripperClosed));
    }
}
