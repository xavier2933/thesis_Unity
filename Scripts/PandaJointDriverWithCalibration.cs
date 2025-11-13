using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;

public class PandaJointDriverNoOffsets : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/joint_states";

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

    Dictionary<string, ArticulationBody> jointMap = new Dictionary<string, ArticulationBody>();
    Dictionary<string, float> rosTargets = new Dictionary<string, float>();

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<JointStateMsg>(topicName, JointStateCallback);

        foreach (var ab in GetComponentsInChildren<ArticulationBody>())
        {
            jointMap[ab.name] = ab;
        }
    }

    void JointStateCallback(JointStateMsg msg)
    {
        for (int i = 0; i < msg.name.Length; i++)
        {
            string rosJoint = msg.name[i];
            float target = (float)msg.position[i]; // rad or m

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

                float targetDeg = ab.jointType == ArticulationJointType.RevoluteJoint
                    ? rosTarget * Mathf.Rad2Deg
                    : rosTarget;

                // Smoothly move toward target
                drive.target = Mathf.Lerp(drive.target, targetDeg, 0.2f);  
                ab.xDrive = drive;
            }
        }
    }
}
