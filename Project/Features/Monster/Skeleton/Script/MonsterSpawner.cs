using UnityEngine;
using Mirror;

public class MonsterSpawner : NetworkBehaviour
{
    public GameObject monsterPrefab;
    public float spawnInterval = 5f;
    
    [Header("Vùng sinh quái")]
    public Collider2D spawnZone; // Kéo cái Box Collider của vùng muốn sinh quái vào đây

    public override void OnStartServer()
    {
        InvokeRepeating(nameof(SpawnMonster), spawnInterval, spawnInterval);
    }

    [Server]
    void SpawnMonster()
    {
        if (spawnZone == null)
        {
            Debug.LogWarning("Hùng ơi, chưa kéo Spawn Zone vào Spawner kìa!");
            return;
        }

        // Lấy giới hạn của Collider (khung hình chữ nhật bao quanh)
        Bounds bounds = spawnZone.bounds;

        // Lấy tọa độ ngẫu nhiên trong khung đó
        float randomX = Random.Range(bounds.min.x, bounds.max.x);
        float randomY = Random.Range(bounds.min.y, bounds.max.y);
        Vector2 spawnPos = new Vector2(randomX, randomY);

        // (Tùy chọn) Kiểm tra lại xem điểm đó có thực sự nằm TRONG Collider không 
        // (Trường hợp Hùng dùng Polygon Collider hình thù kỳ dị)
        if (spawnZone.OverlapPoint(spawnPos))
        {
            GameObject monster = Instantiate(monsterPrefab, spawnPos, Quaternion.identity);
            NetworkServer.Spawn(monster);
        }
        else
        {
            // Nếu điểm random rơi vào góc chết của Polygon, thử lại hoặc bỏ qua
            Debug.Log("Điểm random rơi ra ngoài vùng quy định, đang thử lại...");
            SpawnMonster(); 
        }
    }
}