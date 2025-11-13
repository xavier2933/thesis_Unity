using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using System.Collections.Generic;
using UnityEngine;

public class JointStateSubscriberAB : MonoBehaviour
{
    ROSConnection ros;
    public string topicName = "/joint_states";

    // Map joint name → articulation body
    Dictionary<string, ArticulationBody> jointMap = new Dictionary<string, ArticulationBody>();

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<JointStateMsg>(topicName, JointStateCallback);

        // Build dictionary of all articulation bodies
        foreach (var ab in GetComponentsInChildren<ArticulationBody>())
        {
            // URDF Importer names GameObjects after the URDF joint name
            jointMap[ab.name] = ab;
        }
    }

    void JointStateCallback(JointStateMsg msg)
    {
        for (int i = 0; i < msg.name.Length; i++)
        {
            string jointName = msg.name[i];
            double posRad = msg.position[i];

            if (jointMap.TryGetValue(jointName, out ArticulationBody ab))
            {
                SetJointTarget(ab, (float)posRad);
            }
        }
    }

    void SetJointTarget(ArticulationBody ab, float targetRad)
    {
        var drive = ab.xDrive;  // revolute joints use xDrive
        drive.target = targetRad * Mathf.Rad2Deg;  // Unity expects degrees
        ab.xDrive = drive;  // must reassign (struct)
    }
}
