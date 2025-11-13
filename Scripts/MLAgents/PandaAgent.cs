using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class PandaAgent : Agent
{
    public Transform endEffector;
    public Transform target;

    public override void CollectObservations(VectorSensor sensor)
    {
        // End-effector position
        sensor.AddObservation(endEffector.position);
        // Target position
        sensor.AddObservation(target.position);
        // Optional: joint angles or velocities
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Example: apply actions as delta positions
        Vector3 delta = new Vector3(
            actions.ContinuousActions[0],
            actions.ContinuousActions[1],
            actions.ContinuousActions[2]
        );

        // Apply delta to end-effector (via your MoveIt bridge)
        target.position += delta;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // For recording teleop demonstrations: map keyboard or Unity target object
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal") * 0.1f;
        continuousActions[1] = Input.GetAxis("UpDown") * 0.1f;
        continuousActions[2] = Input.GetAxis("Vertical") * 0.1f;
    }
}
