using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Trajectory;

public class TrajectoryExecutor : MonoBehaviour
{
    [Tooltip("Assign the joints in the same order as MoveIt publishes them")]
    public ArticulationBody[] joints;

    // Store the active trajectory
    private List<JointTrajectoryPointMsg> trajectoryPoints = new List<JointTrajectoryPointMsg>();
    private int currentPointIndex = 0;
    private float pointStartTime = 0f;

    void Start()
    {
        // Subscribe to MoveIt joint trajectory topic
        ROSConnection.instance.Subscribe<JointTrajectoryMsg>(
            "/panda_arm_controller/joint_trajectory", TrajectoryCallback);
    }

    void Update()
    {
        if (trajectoryPoints.Count == 0 || currentPointIndex >= trajectoryPoints.Count)
            return;

        // Time since we started moving toward this point
        float elapsed = Time.time - pointStartTime;
        float targetTime = (float)trajectoryPoints[currentPointIndex].time_from_start.sec +
                           trajectoryPoints[currentPointIndex].time_from_start.nanosec * 1e-9f;

        // If we’ve reached this waypoint, move to the next
        if (elapsed >= targetTime)
        {
            ApplyPoint(trajectoryPoints[currentPointIndex]); // snap to final values
            currentPointIndex++;
            pointStartTime = Time.time;
        }
        else
        {
            // Interpolate between last and current point
            int prevIndex = Mathf.Max(0, currentPointIndex - 1);
            float prevTime = (float)trajectoryPoints[prevIndex].time_from_start.sec +
                             trajectoryPoints[prevIndex].time_from_start.nanosec * 1e-9f;

            float t = Mathf.InverseLerp(prevTime, targetTime, elapsed + prevTime);
            InterpolatePoints(trajectoryPoints[prevIndex], trajectoryPoints[currentPointIndex], t);
        }
    }

    void TrajectoryCallback(JointTrajectoryMsg msg)
    {
        // Replace stored trajectory with new one
        Debug.Log($"GOt trajectory {msg}");

        trajectoryPoints.Clear();
        foreach (var point in msg.points)
            trajectoryPoints.Add(point);

        currentPointIndex = 0;
        pointStartTime = Time.time;
    }

    void ApplyPoint(JointTrajectoryPointMsg point)
    {
        for (int i = 0; i < joints.Length && i < point.positions.Length; i++)
        {
            var drive = joints[i].xDrive;
            drive.target = Mathf.Rad2Deg * (float)point.positions[i]; // ROS uses radians
            joints[i].xDrive = drive;
        }
    }

    void InterpolatePoints(JointTrajectoryPointMsg p0, JointTrajectoryPointMsg p1, float t)
    {
        for (int i = 0; i < joints.Length && i < p0.positions.Length; i++)
        {
            float start = (float)p0.positions[i];
            float end = (float)p1.positions[i];
            float interp = Mathf.Lerp(start, end, t);

            var drive = joints[i].xDrive;
            drive.target = Mathf.Rad2Deg * interp;
            joints[i].xDrive = drive;
        }
    }
}
