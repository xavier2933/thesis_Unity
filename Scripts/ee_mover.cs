using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

public class EEMover : MonoBehaviour
{
    public Transform endEffector;
    private ROSConnection ros;
    public string topicName = "/goal_pose";

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<PoseStampedMsg>(topicName);
    }

    void Update()
    {
        Vector3 delta = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")) * Time.deltaTime;
        endEffector.position += delta;

        // Get current time in ROS format
        var currentTime = System.DateTimeOffset.UtcNow;
        var rosTime = new TimeMsg
        {
            sec = (int)currentTime.ToUnixTimeSeconds(),
            nanosec = (uint)((currentTime.ToUnixTimeMilliseconds() % 1000) * 1000000)
        };

        // Create message
        PoseStampedMsg goalMsg = new PoseStampedMsg
        {
            header = new HeaderMsg
            {
                frame_id = "panda_link0",
                stamp = rosTime
            },
            pose = new PoseMsg
            {
                position = new PointMsg
                {
                    x = endEffector.position.x,
                    y = endEffector.position.y,
                    z = endEffector.position.z
                },
                orientation = new QuaternionMsg
                {
                    x = endEffector.rotation.x,
                    y = endEffector.rotation.y,
                    z = endEffector.rotation.z,
                    w = endEffector.rotation.w
                }
            }
        };

        ros.Publish(topicName, goalMsg);
    }
}