// using UnityEngine;
// using Unity.Robotics.ROSTCPConnector;
// using RosMessageTypes.Geometry;

// public class PandaArmReset : MonoBehaviour
// {
//     ROSConnection ros;
//     public string topicName = "/unity_target_pose";

//     void Start()
//     {
//         ros = ROSConnection.GetOrCreateInstance();
//         ros.RegisterPublisher<PoseMsg>(topicName);
//     }

//     void Update()
//     {
//         if (Input.GetKeyDown(KeyCode.R))
//         {
//             PublishResetPose();
//         }
//     }

//     void PublishResetPose()
//     {
//         PoseMsg resetPose = new PoseMsg
//         {
//             position = new PointMsg
//             {
//                 x = 0.672,
//                 y = 0.669,
//                 z = 0.0
//             },
//             orientation = new QuaternionMsg
//             {
//                 x = 0.0,
//                 y = 0.0,
//                 z = 0.0,
//                 w = 1.0
//             }
//         };

//         ros.Publish(topicName, resetPose);
//         Debug.Log("Published reset pose: (0.672, 0.669, 0) with identity rotation");
//     }
// }