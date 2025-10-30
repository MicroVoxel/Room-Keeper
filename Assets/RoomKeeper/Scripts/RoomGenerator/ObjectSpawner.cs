// ObjectSpawner.cs
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    [Header("Object Prefabs")]
    public GameObject[] decorationPrefabs;
    public GameObject[] itemPrefabs;
    public GameObject[] monsterPrefabs;

    [Header("Spawn Settings")]
    public Transform[] points;
    [Range(0f, 1f)] public float monsterSpawnChance = 0.25f; // โอกาสเกิดมอนสเตอร์ 25%

    private void Start()
    {
        SpawnObjects();
    }

    public void SpawnObjects()
    {
        if (points.Length == 0) return;

        foreach (Transform point in points)
        {
            // สุ่มว่าจะวางอะไรดี
            int choice = Random.Range(0, 3); // 0=ว่าง, 1=ของตกแต่ง, 2=ไอเท็ม

            // ลองสุ่มมอนสเตอร์ก่อน
            if (Random.value < monsterSpawnChance)
            {
                if (monsterPrefabs.Length > 0)
                {
                    GameObject monster = GetRandomPrefab(monsterPrefabs);
                    Instantiate(monster, point.position, point.rotation, point);
                }
            }
            else // ถ้าไม่เกิดมอนสเตอร์ ก็สุ่มของอย่างอื่น
            {
                if (choice == 1 && decorationPrefabs.Length > 0)
                {
                    GameObject decor = GetRandomPrefab(decorationPrefabs);
                    Instantiate(decor, point.position, point.rotation, point);
                }
                else if (choice == 2 && itemPrefabs.Length > 0)
                {
                    GameObject item = GetRandomPrefab(itemPrefabs);
                    Instantiate(item, point.position, point.rotation, point);
                }
            }
        }
    }

    private GameObject GetRandomPrefab(GameObject[] prefabs)
    {
        return prefabs[Random.Range(0, prefabs.Length)];
    }
}