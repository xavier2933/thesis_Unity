using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class RockDetector : MonoBehaviour
{
    public string topicName = "rock_detection";
    public float detectionRadius = 3.0f;
    
    [HideInInspector] 
    public Transform roverTransform; 
    
    private ROSConnection ros;
    private bool hasPublished = false;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<StringMsg>(topicName);
    }

    void Update()
    {
        // Only run if the spawner successfully passed the rover reference
        if (roverTransform != null && !hasPublished)
        {
            float distance = Vector3.Distance(transform.position, roverTransform.position);

            if (distance <= detectionRadius)
            {
                StringMsg msg = new StringMsg($"Rock at {transform.position} detected!");
                ros.Publish(topicName, msg);
                hasPublished = true;
                
                // Optional: Visual feedback in Unity console
                Debug.Log($"Published detection for {gameObject.name}");
            }
        }
    }
}