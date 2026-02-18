using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using RosMessageTypes.Tf2;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;
using System;

public class PlateTFBroadcaster : MonoBehaviour
{
    [Header("ROS Configuration")]
    public string parentFrame = "panda_link0";
    public string childFrame = "target_plate";
    public float publishRate = 10f;

    [Header("Unity References")]
    public Transform referenceFrame; // Arm base (panda_link0)
    public Transform truckTransform; // Truck base
    public Transform currentTargetPlate;

    [Header("Offset Configuration")]
    [Tooltip("Target position relative to TRUCK in Unity local coordinates")]
    public Vector3 targetOffsetFromTruck_Unity = new Vector3(0.29f, -1.49f, -2.81f);

    private Vector3? virtualTargetWorldPosition = null;
    private ROSConnection ros;
    private float publishInterval;
    private float timer;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<TFMessageMsg>("/tf");
        publishInterval = 1f / publishRate;
    }

    void Update()
    {
        if (referenceFrame == null || (currentTargetPlate == null && virtualTargetWorldPosition == null)) return;

        timer += Time.deltaTime;
        if (timer >= publishInterval)
        {
            timer = 0f;
            PublishTF();
        }
    }

    public void SetTargetPlate(Transform plateTransform)
    {
        virtualTargetWorldPosition = null;
        currentTargetPlate = plateTransform;
        Debug.Log($"<color=cyan>[TF] Target plate set to Transform: {plateTransform.name}</color>");
    }

    public void SetVirtualTarget(Vector3 worldPos)
    {
        currentTargetPlate = null;
        
        // Convert truck-local offset to world position
        if (truckTransform != null)
        {
            virtualTargetWorldPosition = truckTransform.TransformPoint(targetOffsetFromTruck_Unity);
            Debug.Log($"<color=cyan>[TF] Virtual target world position: {virtualTargetWorldPosition}</color>");
        }
        else
        {
            Debug.LogError("[TF] No truck transform - cannot compute virtual target!");
        }
    }

    private void PublishTF()
    {
        // Get target world position (either from real plate or virtual target)
        Vector3 targetWorldPos = currentTargetPlate != null ? 
                                 currentTargetPlate.position : 
                                 virtualTargetWorldPosition.Value;

        // Convert to arm-relative position (SAME AS PLATES)
        Vector3 relPos = referenceFrame.InverseTransformPoint(targetWorldPos);
        
        // For rotation: Plates have rotation, virtual targets use identity
        Quaternion targetRot = currentTargetPlate != null ? 
                              currentTargetPlate.rotation : 
                              Quaternion.identity;
        Quaternion relRot = Quaternion.Inverse(referenceFrame.rotation) * targetRot;

        // Unity (LHS, Y-up) → ROS (RHS, Z-up) Conversion (SAME AS PLATES)
        Vector3 rosPos = new Vector3(relPos.z, -relPos.x, relPos.y);
        Quaternion rosRot = new Quaternion(-relRot.z, relRot.x, -relRot.y, relRot.w);

        // Debug.Log($"<color=yellow>[TF] Unity arm-relative: {relPos}, ROS: {rosPos}, Distance: {relPos.magnitude:F3}m</color>");

        TransformStampedMsg tf = new TransformStampedMsg
        {
            header = new HeaderMsg
            {
                frame_id = parentFrame,
                stamp = UnityTimeNow()
            },
            child_frame_id = childFrame,
            transform = new TransformMsg
            {
                translation = new Vector3Msg { x = rosPos.x, y = rosPos.y, z = rosPos.z },
                rotation = new QuaternionMsg { x = rosRot.x, y = rosRot.y, z = rosRot.z, w = rosRot.w }
            }
        };

        ros.Publish("/tf", new TFMessageMsg(new[] { tf }));
    }

    private static TimeMsg UnityTimeNow()
    {
        double time = Time.realtimeSinceStartupAsDouble;
        int sec = (int)Math.Floor(time);
        uint nanosec = (uint)((time - sec) * 1e9);
        return new TimeMsg(sec, nanosec);
    }
}