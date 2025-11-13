using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class RightGripperContactPublisher : MonoBehaviour
{
    public string topicName = "/right_contact_detected";
    private ROSConnection ros;
    private BoolMsg contactMsg;
    private bool isInContact = false;

    // Timer for 10 Hz publishing
    private float publishInterval = 0.1f;
    private float timeSinceLastPublish = 0f;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<BoolMsg>(topicName);
        contactMsg = new BoolMsg(false);
    }

    void Update()
    {
        timeSinceLastPublish += Time.deltaTime;

        if (timeSinceLastPublish >= publishInterval)
        {
            PublishContact();
            timeSinceLastPublish = 0f;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Graspable"))
        {
            if (!isInContact)
            {
                isInContact = true;
                PublishContact();  // publish immediately on contact
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Graspable"))
        {
            if (isInContact)
            {
                isInContact = false;
                PublishContact();  // publish immediately on contact loss
            }
        }
    }

    void PublishContact()
    {
        contactMsg.data = isInContact;
        ros.Publish(topicName, contactMsg);
    }
}
