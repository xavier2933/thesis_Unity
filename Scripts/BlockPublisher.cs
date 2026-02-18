using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using RosMessageTypes.Tf2;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;
using System;

public class BlockTFBroadcaster : MonoBehaviour
{
    [Header("ROS")]
    public string childFrame = "block";
    public float publishRate = 10f;

    [Header("Unity References")]
    public Transform referenceFrame; 

    private ROSConnection ros;
    private float publishInterval;
    private float timer;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<TFMessageMsg>("/tf");
        publishInterval = 1f / publishRate;

        // Auto-find panda_link0 if not assigned manually
        if (referenceFrame == null)
        {
            GameObject link0 = GameObject.Find("panda_link0");
            if (link0 != null) referenceFrame = link0.transform;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= publishInterval && referenceFrame != null)
        {
            timer = 0f;
            PublishTF();
        }
    }

    private static TimeMsg UnityTimeNow()
    {
        double time = Time.realtimeSinceStartupAsDouble;
        int sec = (int)Math.Floor(time);
        uint nanosec = (uint)((time - sec) * 1e9);
        return new TimeMsg(sec, nanosec);
    }

    void PublishTF()
    {
        Vector3 relPos = referenceFrame.InverseTransformPoint(transform.position);
        Quaternion relRot = Quaternion.Inverse(referenceFrame.rotation) * transform.rotation;

        // Unity -> ROS Coordinate Conversion
        Vector3 rosPos = new Vector3(relPos.z, -relPos.x, relPos.y);
        Quaternion rosRot = new Quaternion(-relRot.z, relRot.x, -relRot.y, relRot.w);

        TransformStampedMsg tf = new TransformStampedMsg
        {
            header = new HeaderMsg { frame_id = "panda_link0", stamp = UnityTimeNow() },
            child_frame_id = childFrame,
            transform = new TransformMsg
            {
                translation = new Vector3Msg { x = rosPos.x, y = rosPos.y, z = rosPos.z },
                rotation = new QuaternionMsg { x = rosRot.x, y = rosRot.y, z = rosRot.z, w = rosRot.w }
            }
        };

        ros.Publish("/tf", new TFMessageMsg(new[] { tf }));
    }
}