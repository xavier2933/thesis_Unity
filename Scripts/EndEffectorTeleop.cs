using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class EndEffectorTeleop : MonoBehaviour
{
    public string topicName = "/unity_target_pose";
    ROSConnection ros;

    // Reference to the arm base transform
    public Transform armBaseTransform; // Drag panda_link0 here in Inspector

    // Scale factor - Unity is 1.5x larger than MoveIt
    private float scaleFactorUnityToROS = 1.0f / 1.0f; // 0.6667
    
    // Last published position to avoid flooding
    private Vector3 lastPublishedPosition;
    
    // Reset position (relative to arm base, in Unity coordinates)
    private Vector3 resetPositionLocal = new Vector3(0.672f, 0.669f, 0.0f);
    private Quaternion resetRotationLocal = Quaternion.identity;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        lastPublishedPosition = transform.position;
        ros.RegisterPublisher<PoseMsg>(topicName);
        
        if (armBaseTransform == null)
        {
            Debug.LogError("Arm Base Transform not assigned! Drag panda_link0 to this field.");
        }
    }

    void Update()
    {
        // Reset to specific position when R is pressed
        if (Input.GetKeyDown(KeyCode.R))
        {
            // Convert local reset position to world space
            transform.position = armBaseTransform.TransformPoint(resetPositionLocal);
            transform.rotation = armBaseTransform.rotation * resetRotationLocal;
            PublishCurrentPose();
            Debug.Log("Reset to position relative to arm base");
            return;
        }

        // Move the object with keyboard
        float moveSpeed = 0.005f;
        Vector3 delta = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) delta.z += moveSpeed;
        if (Input.GetKey(KeyCode.S)) delta.z -= moveSpeed;
        if (Input.GetKey(KeyCode.A)) delta.x -= moveSpeed;
        if (Input.GetKey(KeyCode.D)) delta.x += moveSpeed;
        if (Input.GetKey(KeyCode.Q)) delta.y += moveSpeed;
        if (Input.GetKey(KeyCode.E)) delta.y -= moveSpeed;

        transform.position += delta;

        // Only publish if moved more than 1 cm
        if ((transform.position - lastPublishedPosition).magnitude > 0.005f)
        {
            PublishCurrentPose();
        }
    }

    void PublishCurrentPose()
    {
        if (armBaseTransform == null) return;

        // Convert world position to arm base local space
        Vector3 localPosition = armBaseTransform.InverseTransformPoint(transform.position);
        Quaternion localRotation = Quaternion.Inverse(armBaseTransform.rotation) * transform.rotation;
        
        // Scale the local position from Unity coordinates to ROS coordinates
        Vector3 rosPosition = localPosition * scaleFactorUnityToROS;
        
        PoseMsg pose = new PoseMsg
        {
            position = new PointMsg(rosPosition.x, rosPosition.y, rosPosition.z),
            orientation = new QuaternionMsg(localRotation.x, localRotation.y, localRotation.z, localRotation.w)
        };
        ros.Publish(topicName, pose);
        lastPublishedPosition = transform.position;
        
        Debug.Log($"World pos: {transform.position}, Local pos: {localPosition}, ROS pos: {rosPosition}");
    }
}