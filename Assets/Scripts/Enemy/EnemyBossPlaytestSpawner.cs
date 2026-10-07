using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>Enemy/Boss-only test harness. Add it to a copy of Dungeon, never to the campaign scene.</summary>
public class EnemyBossPlaytestSpawner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private GameObject[] prefabs = new GameObject[8];
    [SerializeField] private bool spawnSampleEnemiesOnStart = true;

    private readonly string[] names =
    {
        "Warrior", "Archer", "Poison Slime", "Bat", "Dark Mage", "Elite", "Bone Sentinel", "Skeleton King"
    };
    private string status = "Keys 1-8: spawn near Player. Key 0: clear test spawns.";

    private IEnumerator Start()
    {
        if (player == null)
        {
            GameObject found = GameObject.Find("Player");
            if (found != null) player = found.transform;
        }

        // InfiniteWorldGenerator builds the dungeon and NavMesh in Start().
        yield return null;
        if (!spawnSampleEnemiesOnStart) yield break;
        Spawn(1);
        Spawn(2);
        Spawn(3);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.f1Key.wasPressedThisFrame) Spawn(0);
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.f2Key.wasPressedThisFrame) Spawn(1);
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.f3Key.wasPressedThisFrame) Spawn(2);
        if (keyboard.digit4Key.wasPressedThisFrame || keyboard.f4Key.wasPressedThisFrame) Spawn(3);
        if (keyboard.digit5Key.wasPressedThisFrame || keyboard.f5Key.wasPressedThisFrame) Spawn(4);
        if (keyboard.digit6Key.wasPressedThisFrame || keyboard.f6Key.wasPressedThisFrame) Spawn(5);
        if (keyboard.digit7Key.wasPressedThisFrame || keyboard.f7Key.wasPressedThisFrame) Spawn(6);
        if (keyboard.digit8Key.wasPressedThisFrame || keyboard.f8Key.wasPressedThisFrame) Spawn(7);
        if (keyboard.digit0Key.wasPressedThisFrame || keyboard.f9Key.wasPressedThisFrame) ClearTestSpawns();
    }

    private void Spawn(int index)
    {
        if (index < 0 || index >= prefabs.Length || prefabs[index] == null || player == null)
        {
            status = "Missing prefab or Player reference for slot " + index;
            return;
        }

        Vector3 origin = player.position;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(2.2f, 4.2f);
            Vector3 desired = origin + new Vector3(offset.x, offset.y, 0f);
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
            if (Vector2.Distance(origin, hit.position) < 1.5f) continue;

            GameObject instance = Instantiate(prefabs[index], hit.position, Quaternion.identity, transform);
            EnemyHealth health = instance.GetComponent<EnemyHealth>();
            if (health != null) health.SetCountsForLegacyCounter(false);
            instance.name = "TEST " + names[index];
            status = "Spawned " + names[index] + " near Player.";
            return;
        }
        status = "No valid NavMesh point near Player for " + names[index] + ".";
        Debug.LogWarning(status, this);
    }

    private void ClearTestSpawns()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        status = "Cleared test spawns.";
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 260f, 8f, 252f, 212f), GUI.skin.box);
        GUILayout.Label("ENEMY / BOSS PLAYTEST");
        GUILayout.Label("1 Warrior    2 Archer");
        GUILayout.Label("3 Poison Slime    4 Bat");
        GUILayout.Label("5 Dark Mage    6 Elite");
        GUILayout.Label("7 Bone Sentinel    8 Skeleton King");
        GUILayout.Label("0 Clear test spawns");
        GUILayout.Space(6f);
        GUILayout.Label(status);
        GUILayout.EndArea();
    }
}
