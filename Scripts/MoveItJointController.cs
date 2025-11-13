using UnityEngine;
using RosMessageTypes.Sensor;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

public class MoveItArticulationController : MonoBehaviour
{
    [Header("Joint Configuration")]
    public string[] jointNames;        // Match MoveIt joint names
    public float stiffness = 10000f;
    public float damping = 100f;
    public float forceLimit = 1000f;
    
    [Header("ROS Configuration")]
    public string jointStateTopic = "/joint_states";
    
    private ArticulationBody[] jointArticulationBodies;
    private ROSConnection ros;
    private bool jointsFound = false;

    void Start()
    {
        // Find all joint ArticulationBodies
        jointArticulationBodies = new ArticulationBody[jointNames.Length];
        
        for (int i = 0; i < jointNames.Length; i++)
        {
            Transform jointTransform = FindDeepChild(transform, jointNames[i]);
            if (jointTransform != null)
            {
                jointArticulationBodies[i] = jointTransform.GetComponent<ArticulationBody>();
                if (jointArticulationBodies[i] == null)
                {
                    Debug.LogError($"Joint {jointNames[i]} found but has no ArticulationBody component!");
                }
                else
                {
                    // Configure the joint drive
                    ConfigureJointDrive(jointArticulationBodies[i]);
                    Debug.Log($"Configured joint: {jointNames[i]}");
                }
            }
            else
            {
                Debug.LogWarning($"Joint {jointNames[i]} not found in hierarchy.");
            }
        }
        
        jointsFound = true;

        // Connect to ROS
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<JointStateMsg>(jointStateTopic, JointStateCallback);
        
        Debug.Log($"Subscribed to {jointStateTopic}. Found {System.Array.FindAll(jointArticulationBodies, j => j != null).Length} valid joints.");
    }

    void ConfigureJointDrive(ArticulationBody joint)
    {
        // Configure the joint drive for position control
        var drive = joint.xDrive;
        drive.stiffness = stiffness;
        drive.damping = damping;
        drive.forceLimit = forceLimit;
        joint.xDrive = drive;
        
        // Also configure other drives that might be active
        joint.yDrive = drive;
        joint.zDrive = drive;
    }

    void JointStateCallback(JointStateMsg jointState)
    {
        if (!jointsFound) return;
        
        Debug.Log($"Received joint states: {jointState.position.Length} positions for {jointState.name.Length} joints");
        
        // Create a mapping from joint names to positions
        for (int rosJointIndex = 0; rosJointIndex < jointState.name.Length; rosJointIndex++)
        {
            string rosJointName = jointState.name[rosJointIndex];
            
            // Find matching joint in our array
            for (int unityJointIndex = 0; unityJointIndex < jointNames.Length; unityJointIndex++)
            {
                if (jointNames[unityJointIndex] == rosJointName && jointArticulationBodies[unityJointIndex] != null)
                {
                    float targetPosition = (float)jointState.position[rosJointIndex];
                    SetJointTarget(jointArticulationBodies[unityJointIndex], targetPosition);
                    break;
                }
            }
        }
    }

    void SetJointTarget(ArticulationBody joint, float targetPositionRad)
    {
        // Convert radians to degrees for Unity
        float targetPositionDeg = targetPositionRad * Mathf.Rad2Deg;
        
        // Determine which drive to use based on joint type
        ArticulationDrive drive;
        
        switch (joint.jointType)
        {
            case ArticulationJointType.RevoluteJoint:
                drive = joint.xDrive;
                drive.target = targetPositionDeg;
                joint.xDrive = drive;
                break;
                
            case ArticulationJointType.PrismaticJoint:
                drive = joint.xDrive;
                drive.target = targetPositionRad; // Keep in meters for prismatic
                joint.xDrive = drive;
                break;
                
            case ArticulationJointType.SphericalJoint:
                // For spherical joints, you might need to handle multiple axes
                drive = joint.xDrive;
                drive.target = targetPositionDeg;
                joint.xDrive = drive;
                break;
                
            default:
                Debug.LogWarning($"Unsupported joint type: {joint.jointType}");
                break;
        }
    }

    // Recursively searches hierarchy for a child by name
    Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            var result = FindDeepChild(child, name);
            if (result != null)
                return result;
        }
        return null;
    }
    
    // Debug method to manually test joint movement
    [ContextMenu("Test Joint Movement")]
    void TestJointMovement()
    {
        if (jointArticulationBodies != null)
        {
            for (int i = 0; i < jointArticulationBodies.Length; i++)
            {
                if (jointArticulationBodies[i] != null)
                {
                    SetJointTarget(jointArticulationBodies[i], 0.5f); // Move to 0.5 radians
                }
            }
        }
    }
}