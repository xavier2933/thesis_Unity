using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class DeploymentValidator : MonoBehaviour
{
    [Header("Validation Settings")]
    public float maxTiltAngle = 10f;
    public float validationDelay = 2.0f; // Give physics time to settle
    
    [Header("References")]
    public BlockDispenser blockDispenser;
    
    private ROSConnection ros;
    private int pendingSiteId = -1;
    private bool validationPending = false;
    private float validationTimer = 0f;
    private GameObject lastSpawnedBlock;
    
    private const string DEPLOYMENT_RESULT_TOPIC = "/deployment_result";
    private const string DEPLOYMENT_SITE_TOPIC = "/deployment_site_id";

    private const string PLACEMENT_COMPLETE_TOPIC = "/placement_complete";

    
    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<StringMsg>(DEPLOYMENT_RESULT_TOPIC);
        ros.Subscribe<Int32Msg>(DEPLOYMENT_SITE_TOPIC, OnDeploymentSiteReceived);
        ros.Subscribe<Int32Msg>(PLACEMENT_COMPLETE_TOPIC, OnPlacementComplete);

        
        Debug.Log("✅ [DeploymentValidator] Initialized and listening for Site IDs.");
    }
    
    void OnDeploymentSiteReceived(Int32Msg msg)
    {
        pendingSiteId = msg.data;
        Debug.Log($"📋 [DeploymentValidator] Next deployment target set to Site ID: {pendingSiteId}");
    }

    public void OnBlockReleased()
    {
        if (pendingSiteId < 0)
        {
            Debug.LogWarning("⚠️ [DeploymentValidator] Block released but no site ID pending!");
            return;
        }
        
        validationPending = true;
        validationTimer = validationDelay;
        Debug.Log($"⏱️ [DeploymentValidator] Starting validation in {validationDelay}s for site {pendingSiteId}");
    }

    void OnPlacementComplete(Int32Msg msg)
    {
        // Sync the site ID from the message
        pendingSiteId = msg.data;

        if (lastSpawnedBlock == null)
        {
            Debug.LogWarning($"⚠️ [DeploymentValidator] Site {pendingSiteId} complete, but no block reference found!");
            return;
        }
        
        validationPending = true;
        validationTimer = validationDelay;
        Debug.Log($"📥 [DeploymentValidator] Signal for Site {pendingSiteId} received. Validating in {validationDelay}s...");
    }
    
    // This is called by BlockDispenser after it instantiates a block
    public void NotifyBlockReleased(GameObject newBlock)
    {
        lastSpawnedBlock = newBlock;
        Debug.Log($"📦 [DeploymentValidator] Block reference captured: {newBlock.name}");
    }
    
    void Update()
    {
        if (!validationPending) return;
        
        validationTimer -= Time.deltaTime;
        if (validationTimer <= 0)
        {
            validationPending = false;
            ValidateDeployment();
        }
    }
    
    void ValidateDeployment()
    {
        if (lastSpawnedBlock == null)
        {
            PublishResult(pendingSiteId, false, "Block reference lost or destroyed");
            return;
        }

        // 1. Define the target rotation (-90, 0, -90)
        Quaternion targetRotation = Quaternion.Euler(-90f, 0f, -90f);
        
        // 2. Get current rotation
        Quaternion currentRotation = lastSpawnedBlock.transform.rotation;

        // 3. Calculate the difference in degrees for each individual axis
        // We use Mathf.DeltaAngle to handle the 360-degree wrap-around correctly
        Vector3 currentEuler = currentRotation.eulerAngles;
        float diffX = Mathf.Abs(Mathf.DeltaAngle(currentEuler.x, -90f));
        float diffY = Mathf.Abs(Mathf.DeltaAngle(currentEuler.y, 0f));
        float diffZ = Mathf.Abs(Mathf.DeltaAngle(currentEuler.z, -90f));

        // 4. Check if all axes are within the 10-degree tolerance
        bool isSuccess = (diffX <= maxTiltAngle) && 
                        (diffY <= maxTiltAngle) && 
                        (diffZ <= maxTiltAngle);

        // 5. Build a detailed reason string for the ROS log
        string reason = isSuccess 
            ? $"Success: Orientation within tolerance. (ΔX:{diffX:F1} ΔY:{diffY:F1} ΔZ:{diffZ:F1})" 
            : $"Failed: Out of tolerance. (ΔX:{diffX:F1} ΔY:{diffY:F1} ΔZ:{diffZ:F1}) Threshold: {maxTiltAngle}°";

        PublishResult(pendingSiteId, isSuccess, reason);
        pendingSiteId = -1; 
    }
    
    void PublishResult(int siteId, bool success, string reason)
    {
        // Format: "site_id,success,reason"
        string resultString = $"{siteId},{success.ToString().ToLower()},{reason}";
        ros.Publish(DEPLOYMENT_RESULT_TOPIC, new StringMsg(resultString));
        Debug.Log($"📤 [DeploymentValidator] Published to ROS: {resultString}");
    }
}