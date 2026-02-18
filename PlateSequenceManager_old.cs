// using UnityEngine;
// using System.Collections;
// using System.Collections.Generic;

// public class PlateSequenceManager : MonoBehaviour
// {
//     [Header("References")]
//     public Transform platesParent;
//     public SimpleTruckNav navScript;

//     [Header("Settings")]
//     public float waitTimeAtPlate = 1.0f;
//     public int segmentsBetweenPlates = 5;
//     public float waypointThreshold = 0.4f; 
//     public float defaultPlateHeading = 90f;

//     private List<Vector3> platePositions = new List<Vector3>();


//     void Start()
//     {
//         Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);

//         // Safety: Check if references are missing before doing anything
//         if (platesParent == null || navScript == null)
//         {
//             Debug.LogError($"[SEQUENCE] Missing references on {gameObject.name}!");
//             return;
//         }
//     }

//     [ContextMenu("Start Plate Sequence")]
//     public void StartSequence()
//     {
//         StopAllCoroutines(); 
//         GetAllPlatePositions();
        
//         if (platePositions.Count > 0) 
//         {
//             StartCoroutine(RunPathSequence());
//         }
//         else
//         {
//             Debug.LogWarning("[SEQUENCE] No plates found to visit!");
//         }
//     }

//     void GetAllPlatePositions()
//     {
//         platePositions.Clear();
//         // This gathers every child of the 'platesParent'
//         foreach (Transform t in platesParent)
//         {
//             platePositions.Add(t.position);
//             // This prints the discovery order immediately
//             Debug.Log($"[INIT] Found Plate: {t.name} at {t.position}");
//         }
//         Debug.Log($"<color=white><b>[INIT] Total Plates Found: {platePositions.Count}</b></color>");
//     }

//     IEnumerator RunPathSequence()
//     {
//         Vector3 lastPos = transform.position;
//         int plateIndex = 0;

//         foreach (Vector3 platePos in platePositions)
//         {
//             Debug.Log($"<color=yellow><b>[PLATE {plateIndex}]</b> Target: {platePos}</color>");

//             // Use line following - one direct line from current position to plate
//             bool isLastPlate = (plateIndex == platePositions.Count - 1);
//             navScript.SetLineGoal(lastPos, platePos, finalGoal: isLastPlate);
            
//             while (navScript.hasGoal)
//             {
//                 yield return null;
//             }

//             Debug.Log("<color=green>PLATE COMPLETE. Waiting...</color>");
//             yield return new WaitForSeconds(0.3f);
//             yield return new WaitForSeconds(waitTimeAtPlate);
            
//             lastPos = platePos;
//             plateIndex++;
//         }
        
//         Debug.Log("<color=cyan><b>ALL PLATES COMPLETE!</b></color>");
//     }
// }