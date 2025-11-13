using UnityEngine;

public class ArmMounter : MonoBehaviour
{
    private Transform parentTransform;
    private Vector3 localPosition;
    private Quaternion localRotation;
    
    void Start()
    {
        parentTransform = transform.parent;
        localPosition = transform.localPosition;
        localRotation = transform.localRotation;
    }
    
    void FixedUpdate()
    {
        if (parentTransform != null)
        {
            ArticulationBody ab = GetComponent<ArticulationBody>();
            if (ab != null && ab.immovable)
            {
                // Teleport the arm to follow parent
                ab.TeleportRoot(
                    parentTransform.TransformPoint(localPosition),
                    parentTransform.rotation * localRotation
                );
            }
        }
    }
}