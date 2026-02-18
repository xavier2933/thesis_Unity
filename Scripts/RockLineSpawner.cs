using UnityEngine;

public class RockLineSpawner : MonoBehaviour
{
    public GameObject rockPrefab;
    public Transform startPoint;
    public Transform endPoint;
    public Transform rover; // Assign your Rover object here in the Inspector
    public int numberOfRocks = 5; 

    void Start()
    {
        if (rockPrefab != null && startPoint != null && endPoint != null && rover != null)
        {
            SpawnRocks();
        }
        else
        {
            Debug.LogError("RockLineSpawner is missing references! Check the Inspector.");
        }
    }

    void SpawnRocks()
    {
        for (int i = 0; i < numberOfRocks; i++)
        {
            float t = Random.value;
            Vector3 spawnPos = Vector3.Lerp(startPoint.position, endPoint.position, t);
            
            // Jitter for natural placement
            spawnPos += new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));

            // 1. Spawn the rock
            GameObject newRock = Instantiate(rockPrefab, spawnPos, Quaternion.Euler(0, Random.Range(0, 360), 0));

            // 2. Pass the Rover's transform to the RockDetector script on the prefab
            RockDetector detector = newRock.GetComponent<RockDetector>();
            if (detector != null)
            {
                detector.roverTransform = rover;
            }
        }
    }
}