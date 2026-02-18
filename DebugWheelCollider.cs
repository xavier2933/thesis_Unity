using UnityEngine;

public class DebugWheelCollider : MonoBehaviour
{
    public WheelCollider[] wheelColliders;

    void OnDrawGizmos()
    {
        if (wheelColliders == null) return;

        foreach (var wc in wheelColliders)
        {
            if (wc == null) continue;

            if (wc.GetGroundHit(out WheelHit hit))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(hit.point, 0.05f);
                Gizmos.DrawLine(wc.transform.position, hit.point);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(
                    wc.transform.position,
                    wc.transform.position - transform.up * wc.suspensionDistance
                );
            }
        }
    }

}
