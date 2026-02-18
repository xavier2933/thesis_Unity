using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class BlockDispenser : MonoBehaviour
{
    public GameObject bloccPrefab;
    // Set these to the values from your inspector image
    public Vector3 spawnPos = new Vector3(1.781433f, -1.335353f, -0.5479906f);
    public Vector3 spawnRot = new Vector3(90f, -90f, 0f);
    
    private GameObject activeBlock;

    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<EmptyMsg>("/spawn_blocc", HandleSpawn);
    }

    void HandleSpawn(EmptyMsg msg)
    {
        Debug.Log("📢 [Dispenser] HandleSpawn TRIGGERED!");

        if (bloccPrefab == null)
        {
            Debug.LogError("❌ [Dispenser] bloccPrefab is NULL!");
            return;
        }

        // 1. Calculate World Space coordinates based on the Rover's current position
        // This ensures it spawns at that specific spot on the rover, but isn't "attached"
        Vector3 worldPos = transform.TransformPoint(spawnPos);
        Quaternion worldRot = transform.rotation * Quaternion.Euler(spawnRot);

        // 2. Clean up old TF
        if (activeBlock != null)
        {
            var tf = activeBlock.GetComponent<BlockTFBroadcaster>();
            if (tf != null) tf.enabled = false;
            activeBlock.name = "Deployed_Blocc";
        }

        // 3. Spawn WITHOUT a parent (passing 'null' or just omitting the second argument)
        activeBlock = Instantiate(bloccPrefab, worldPos, worldRot); 
        activeBlock.name = "Active_Blocc";
        FindObjectOfType<DeploymentValidator>().NotifyBlockReleased(activeBlock);

        Debug.Log($"✅ [Dispenser] Spawned at World Pos: {worldPos}. It is now independent of the rover.");
    }
}