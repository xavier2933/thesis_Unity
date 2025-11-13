using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class drive : MonoBehaviour
{
    [System.Serializable]
    public class WheelGroup
    {
        public ArticulationBody[] wheels;
    }

    public WheelGroup leftTrack;
    public WheelGroup rightTrack;

    [Header("Drive Parameters")]
    [Tooltip("Maximum drive strength (torque). Higher = more acceleration.")]
    public float driveForce = 1000f;
    [Tooltip("Maximum rotational velocity (deg/sec) each wheel can reach.")]
    public float maxSpeed = 500f; // degrees/sec, not radians

    [Header("ROS Settings")]
    public string cmdVelTopic = "/cmd_vel"; // standard topic for robot velocity commands
    public bool useROSInput = true;

    // Internal state
    private float rosForward = 0f;
    private float rosTurn = 0f;
    private ROSConnection ros;

    void Start()
    {
        if (useROSInput)
        {
            ros = ROSConnection.GetOrCreateInstance();
            ros.Subscribe<TwistMsg>(cmdVelTopic, CmdVelCallback);
        }

        // Initialize wheel drives
        InitWheelGroup(leftTrack);
        InitWheelGroup(rightTrack);
    }

    void Update()
    {
        // W/S for forward/backward, A/D for turning
        float forward = Input.GetAxis("Vertical");
        float turn = Input.GetAxis("Horizontal");

        // If ROS input is enabled and active, use that instead of manual input
        if (useROSInput)
        {
            forward = Mathf.Abs(rosForward) > 0.01f ? rosForward : forward;
            turn = Mathf.Abs(rosTurn) > 0.01f ? rosTurn : turn;
        }

        // Tank drive: left and right track speeds
        float leftInput = forward - turn;
        float rightInput = forward + turn;

        if (Mathf.Abs(leftInput) > 0.01f || Mathf.Abs(rightInput) > 0.01f)
        {
            Debug.Log($"left: {leftInput:F2}  right: {rightInput:F2}");
        }

        SetTrackMotor(leftTrack, leftInput);
        SetTrackMotor(rightTrack, rightInput);
    }

    // --- Helper Functions ---

    void InitWheelGroup(WheelGroup track)
    {
        foreach (var wheel in track.wheels)
        {
            if (wheel == null) continue;

            // Make sure articulation body is configured for rotational drive
            wheel.jointType = ArticulationJointType.RevoluteJoint;
            wheel.twistLock = ArticulationDofLock.LimitedMotion;
            wheel.swingYLock = ArticulationDofLock.LockedMotion;
            wheel.swingZLock = ArticulationDofLock.LockedMotion;

            var drive = wheel.xDrive;
            // drive.stiffness = driveForce;
            // drive.damping = 0f;
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

            // Input is in [-1, 1], maxSpeed is degrees/sec
            drive.targetVelocity = input * maxSpeed;
            // drive.stiffness = driveForce;
            // drive.damping = 0f;
            drive.forceLimit = driveForce;
            wheel.xDrive = drive;
        }
    }

    void CmdVelCallback(TwistMsg msg)
    {
        // Typical ROS /cmd_vel: linear.x = forward/backward, angular.z = turn rate
        rosForward = (float)msg.linear.x;
        rosTurn = (float)msg.angular.z;
    }
}
