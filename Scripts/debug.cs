using UnityEngine;

public class ArticulationDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool logHierarchy = true;
    public bool logJointStates = true;
    
    void Start()
    {
        if (logHierarchy)
        {
            Debug.Log("=== ARTICULATION HIERARCHY DEBUG ===");
            DebugArticulationHierarchy(transform, 0);
        }
    }
    
    void Update()
    {
        if (logJointStates && Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("=== CURRENT JOINT STATES ===");
            LogCurrentJointStates();
        }
    }
    
    void DebugArticulationHierarchy(Transform current, int depth)
    {
        string indent = new string('-', depth * 2);
        ArticulationBody ab = current.GetComponent<ArticulationBody>();
        
        if (ab != null)
        {
            Debug.Log($"{indent} {current.name}:");
            Debug.Log($"{indent}   Joint Type: {ab.jointType}");
            Debug.Log($"{indent}   Is Root: {ab.isRoot}");
            Debug.Log($"{indent}   Mass: {ab.mass}");
            
            // Check if it's immovable (Unity 6 compatible way)
            try
            {
                bool immovable = ab.immovable;
                Debug.Log($"{indent}   Immovable: {immovable}");
            }
            catch (System.Exception)
            {
                Debug.Log($"{indent}   Immovable: Unable to read (might be controlled by isRoot)");
            }
            
            // Check drives for moveable joints
            if (!ab.isRoot && (ab.jointType == ArticulationJointType.RevoluteJoint || 
                               ab.jointType == ArticulationJointType.PrismaticJoint))
            {
                var drive = ab.xDrive;
                Debug.Log($"{indent}   X-Drive Target: {drive.target}");
                Debug.Log($"{indent}   X-Drive Stiffness: {drive.stiffness}");
                Debug.Log($"{indent}   X-Drive Damping: {drive.damping}");
                Debug.Log($"{indent}   X-Drive Force Limit: {drive.forceLimit}");
                Debug.Log($"{indent}   X-Drive Lower Limit: {drive.lowerLimit}");
                Debug.Log($"{indent}   X-Drive Upper Limit: {drive.upperLimit}");
            }
            
            // Check for conflicting components
            Rigidbody rb = current.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.LogError($"{indent} ERROR: {current.name} has both ArticulationBody AND Rigidbody!");
            }
            
            Joint[] joints = current.GetComponents<Joint>();
            if (joints.Length > 0)
            {
                Debug.LogWarning($"{indent} WARNING: {current.name} has {joints.Length} Joint components that may conflict with ArticulationBody");
            }
        }
        else
        {
            Debug.Log($"{indent} {current.name}: No ArticulationBody");
        }
        
        foreach (Transform child in current)
        {
            DebugArticulationHierarchy(child, depth + 1);
        }
    }
    
    void LogCurrentJointStates()
    {
        ArticulationBody[] bodies = GetComponentsInChildren<ArticulationBody>();
        
        foreach (var body in bodies)
        {
            if (!body.isRoot)
            {
                try
                {
                    // Unity 6 compatible way to read joint positions
                    int dofCount = body.dofCount;
                    if (dofCount > 0)
                    {
                        Debug.Log($"{body.name}: DoF Count={dofCount}, Joint Type={body.jointType}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Could not read joint state for {body.name}: {e.Message}");
                }
            }
        }
    }
    
    [ContextMenu("Force Reset All Joints")]
    void ForceResetAllJoints()
    {
        Debug.Log("Force resetting all joints to zero...");
        ArticulationBody[] bodies = GetComponentsInChildren<ArticulationBody>();
        
        foreach (var body in bodies)
        {
            if (!body.isRoot && body.jointType == ArticulationJointType.RevoluteJoint)
            {
                var drive = body.xDrive;
                drive.target = 0;
                drive.stiffness = 1000; // More conservative value
                drive.damping = 100;
                drive.forceLimit = 1000;
                body.xDrive = drive;
            }
        }
    }
    
    [ContextMenu("Check for Common Issues")]
    void CheckCommonIssues()
    {
        Debug.Log("=== CHECKING FOR COMMON ISSUES ===");
        
        ArticulationBody[] bodies = GetComponentsInChildren<ArticulationBody>();
        ArticulationBody root = null;
        
        // Find root
        foreach (var body in bodies)
        {
            if (body.isRoot)
            {
                root = body;
                break;
            }
        }
        
        if (root == null)
        {
            Debug.LogError("No root ArticulationBody found!");
        }
        else
        {
            Debug.Log($"Root found: {root.name}, Joint Type: {root.jointType}");
            if (root.jointType != ArticulationJointType.FixedJoint)
            {
                Debug.LogError($"Root ArticulationBody '{root.name}' should have Joint Type = Fixed!");
            }
        }
        
        // Check for proper parent-child relationships
        int validJoints = 0;
        foreach (var body in bodies)
        {
            if (!body.isRoot)
            {
                ArticulationBody parent = body.transform.parent?.GetComponent<ArticulationBody>();
                if (parent == null)
                {
                    Debug.LogError($"ArticulationBody '{body.name}' has no ArticulationBody parent!");
                }
                else
                {
                    validJoints++;
                }
            }
        }
        
        Debug.Log($"Found {validJoints} valid moveable joints");
        
        // Check for reasonable masses
        foreach (var body in bodies)
        {
            if (body.mass < 0.1f)
            {
                Debug.LogWarning($"ArticulationBody '{body.name}' has very low mass: {body.mass}");
            }
            if (body.mass > 100f)
            {
                Debug.LogWarning($"ArticulationBody '{body.name}' has very high mass: {body.mass}");
            }
        }
        
        Debug.Log("Issue check complete.");
    }
    
    [ContextMenu("Simple Joint Test")]
    void SimpleJointTest()
    {
        Debug.Log("Testing joints with random targets...");
        ArticulationBody[] bodies = GetComponentsInChildren<ArticulationBody>();
        
        foreach (var body in bodies)
        {
            if (!body.isRoot && body.jointType == ArticulationJointType.RevoluteJoint)
            {
                var drive = body.xDrive;
                drive.target = Random.Range(-45f, 45f);
                drive.stiffness = 1000;
                drive.damping = 100;
                body.xDrive = drive;
                Debug.Log($"Set {body.name} target to {drive.target:F1} degrees");
            }
        }
    }
}