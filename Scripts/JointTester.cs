using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class PandaJointDriver : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/joint_states";

    // Mapping: ROS joint name → Unity link name
    Dictionary<string, string> rosJointToUnityLink = new Dictionary<string, string>()
    {
        { "panda_joint1", "panda_link1" },
        { "panda_joint2", "panda_link2" },
        { "panda_joint3", "panda_link3" },
        { "panda_joint4", "panda_link4" },
        { "panda_joint5", "panda_link5" },
        { "panda_joint6", "panda_link6" },
        { "panda_joint7", "panda_link7" },
        { "panda_finger_joint1", "panda_leftfinger" },
        { "panda_finger_joint2", "panda_rightfinger" }
    };

    // Unity link name → articulation body
    Dictionary<string, ArticulationBody> jointMap = new Dictionary<string, ArticulationBody>();

    // ROS target values per Unity link
    Dictionary<string, float> rosTargets = new Dictionary<string, float>();

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<JointStateMsg>(topicName, JointStateCallback);

        // Cache articulation bodies by link name
        foreach (var ab in GetComponentsInChildren<ArticulationBody>())
        {
            jointMap[ab.name] = ab;
        }

        Debug.Log($"Cached {jointMap.Count} articulation bodies from robot model.");
    }

    void JointStateCallback(JointStateMsg msg)
    {
        for (int i = 0; i < msg.name.Length; i++)
        {
            string rosJoint = msg.name[i];
            float target = (float)msg.position[i]; // radians for revolute, meters for prismatic

            if (rosJointToUnityLink.TryGetValue(rosJoint, out string unityLink))
            {
                rosTargets[unityLink] = target;
            }
        }
    }

    void FixedUpdate()
    {
        foreach (var kvp in rosTargets)
        {
            string unityLink = kvp.Key;
            float rosTarget = kvp.Value;

            if (jointMap.TryGetValue(unityLink, out ArticulationBody ab))
            {
                var drive = ab.xDrive;

                if (ab.jointType == ArticulationJointType.RevoluteJoint)
                {
                    drive.target = rosTarget * Mathf.Rad2Deg; // convert rad → deg
                }
                else if (ab.jointType == ArticulationJointType.PrismaticJoint)
                {
                    drive.target = rosTarget; // already in meters
                }

                ab.xDrive = drive;
            }
        }
    }

    void Update()
    {
        // Debug log every frame
        foreach (var kvp in rosTargets)
        {
            string unityLink = kvp.Key;
            float rosTarget = kvp.Value;

            if (jointMap.TryGetValue(unityLink, out ArticulationBody ab))
            {
                float unityActual = float.NaN;
                if (ab.jointPosition.dofCount > 0)
                    unityActual = ab.jointPosition[0] * (ab.jointType == ArticulationJointType.RevoluteJoint ? Mathf.Rad2Deg : 1f);

                Debug.Log($"{unityLink} | ROS target: {rosTarget:F3} {(ab.jointType == ArticulationJointType.RevoluteJoint ? "rad" : "m")} | Unity actual: {unityActual:F2}");
            }
        }
    }
}
