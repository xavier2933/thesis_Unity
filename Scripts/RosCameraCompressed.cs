using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

public class RosCameraCompressed : MonoBehaviour
{
    [Header("Settings")]
    public string topicName = "/camera/rgb/image_raw/compressed"; // Standard ROS compressed topic
    public float publishFrequency = 10f; // Limit to 10-15Hz for performance
    public string frameId = "camera_link";
    public int qualityLevel = 50; // 0-100 JPEG quality (50 is a good balance)

    [Header("Components")]
    public RenderTexture targetTexture;
    
    private ROSConnection ros;
    private float timeElapsed;
    private Texture2D texture2D;
    private Rect rect;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        
        // Register as a CompressedImage publisher (much lighter than ImageMsg)
        ros.RegisterPublisher<CompressedImageMsg>(topicName);

        // Initialize the helper texture
        // TextureFormat.RGB24 is standard for ROS
        texture2D = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGB24, false);
        rect = new Rect(0, 0, targetTexture.width, targetTexture.height);
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;

        if (timeElapsed > (1.0f / publishFrequency))
        {
            PublishCameraFrame();
            timeElapsed = 0;
        }
    }

    void PublishCameraFrame()
    {
        // 1. Activate the Render Texture
        RenderTexture.active = targetTexture;

        // 2. Read pixels from GPU to CPU
        texture2D.ReadPixels(rect, 0, 0);
        texture2D.Apply();

        // 3. Manually Encode to JPG (The Fix)
        // This replaces the missing 'ToCompressedImageMsg' method
        byte[] imageBytes = texture2D.EncodeToJPG(qualityLevel);

        // 4. Create the ROS Message manually
        CompressedImageMsg imgMsg = new CompressedImageMsg();
        imgMsg.header = new RosMessageTypes.Std.HeaderMsg { frame_id = frameId };
        imgMsg.format = "jpeg";
        imgMsg.data = imageBytes;

        // 5. Publish
        ros.Publish(topicName, imgMsg);

        // Reset
        RenderTexture.active = null;
    }
}