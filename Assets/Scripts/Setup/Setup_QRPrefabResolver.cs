using UnityEngine;

/// <summary>
/// Setup 用の Prefab 解決ヘルパー。Inspector 未設定時に Resources から補完する。
/// </summary>
public class Setup_QRPrefabResolver
{
    public GameObject ResolveRespawn(GameObject current)
    {
        if (current != null) return current;
        // 優先: hole (指定プレハブ)
        var go = Resources.Load<GameObject>("Prefabs/hole");
        if (go == null) go = Resources.Load<GameObject>("Prefabs/RespawnPlace");
        if (go == null) go = Resources.Load<GameObject>("Prefabs/RespawnPrefab");
        if (go == null) go = Resources.Load<GameObject>("Prefabs/Cube");
        return go;
    }

    public GameObject ResolveEnemyDefault(GameObject current)
    {
        if (current != null) return current;
        // 優先: mole
        var go = Resources.Load<GameObject>("Prefabs/mole");
        if (go == null) go = Resources.Load<GameObject>("Prefabs/EnemyDefault");
        if (go == null) go = Resources.Load<GameObject>("Prefabs/EnemyPrefab");
        if (go == null) go = Resources.Load<GameObject>("Prefabs/Sphere");
        return go;
    }

    public GameObject ResolveEnemyDefaultDefeated(GameObject current)
    {
        if (current != null) return current;
        // 優先: mole_defeated
        var go = Resources.Load<GameObject>("Prefabs/mole_defeated");
        if (go == null) go = Resources.Load<GameObject>("Prefabs/EnemyDefaultDefeated");
        return go;
    }

    public GameObject ResolveEnemy1(GameObject current)
    {
        if (current != null) return current;
        // 優先: Golden_mole
        var go = Resources.Load<GameObject>("Prefabs/Golden_mole");
        if (go == null) go = Resources.Load<GameObject>("Prefabs/Enemy1");
        return go;
    }

    public GameObject ResolveEnemy1Defeated(GameObject current)
    {
        if (current != null) return current;
        // 優先: Golden_mole_defeated
        var go = Resources.Load<GameObject>("Prefabs/Golden_mole_defeated");
        if (go == null) go = Resources.Load<GameObject>("Prefabs/Enemy1Defeated");
        return go;
    }
}
