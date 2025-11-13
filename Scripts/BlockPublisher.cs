using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class BlockPublisher : MonoBehaviour
{
    [Header("ROS Settings")]
    [SerializeField] private string topicName = "/block_pose";
    [SerializeField] private float publishFrequency = 10f; // Hz

    [Header("Transform References")]
    [SerializeField] private Transform referenceFrame; // The frame to compute relative pose

    private ROSConnection ros;
    private float timer;
    private float publishInterval;

    void Start()
    {
        // Get ROS connection
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PoseMsg>(topicName);

        // Calculate publish interval
        publishInterval = 1f / publishFrequency;
        timer = 0f;

        // Validation
        if (referenceFrame == null)
        {
            Debug.LogError("Reference frame not assigned! Please assign a reference frame in the inspector.");
        }
    }

    void Update()
    {
        if (referenceFrame == null) return;

        timer += Time.deltaTime;

        if (timer >= publishInterval)
        {
            timer = 0f;
            PublishPose();
        }
    }

    void PublishPose()
    {
        // Calculate relative position and rotation
        Vector3 relativePosition = referenceFrame.InverseTransformPoint(transform.position);
        Quaternion relativeRotation = Quaternion.Inverse(referenceFrame.rotation) * transform.rotation;

        // Create ROS Pose message
        PoseMsg poseMsg = new PoseMsg
        {
            position = new PointMsg
            {
                // Unity to ROS coordinate conversion: (x, y, z) -> (z, -x, y)
                x = relativePosition.z,
                y = -relativePosition.x,
                z = relativePosition.y
            },
            orientation = new QuaternionMsg
            {
                // Unity to ROS quaternion conversion: (x, y, z, w) -> (-z, x, -y, w)
                x = -relativeRotation.z,
                y = relativeRotation.x,
                z = -relativeRotation.y,
                w = relativeRotation.w
            }
        };

        // Publish to ROS2
        ros.Publish(topicName, poseMsg);
    }

    // Optional: Visualize the relative pose in the editor
    void OnDrawGizmos()
    {
        if (referenceFrame == null) return;

        // Draw reference frame
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(referenceFrame.position, 0.1f);
        Gizmos.DrawLine(referenceFrame.position, referenceFrame.position + referenceFrame.forward * 0.3f);

        // Draw this object
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
        
        // Draw connection
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(referenceFrame.position, transform.position);
    }
}