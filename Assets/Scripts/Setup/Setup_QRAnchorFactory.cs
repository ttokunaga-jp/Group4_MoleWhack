using UnityEngine;

/// <summary>
/// QR 由来のオブジェクト生成を担当するシンプルなファクトリ。
/// 親の生成、Respawn（旧Cube）、Enemy（旧Sphere）、Defeated 用の生成を行う。
/// </summary>
public class Setup_QRAnchorFactory
{
    public class SpawnSettings
    {
        public float scale;
        public float heightOffset;
        public Vector3 rotationEuler;
    }

    public GameObject CreateParent(string uuid, Vector3 position, Quaternion rotation, Transform parent)
    {
        GameObject parentObject = new GameObject($"QR_Marker_{GetShortUuid(uuid)}");
        parentObject.transform.position = position;
        parentObject.transform.rotation = rotation;
        parentObject.transform.SetParent(parent);
        return parentObject;
    }

    public GameObject CreateRespawn(GameObject parentObject, GameObject respawnPrefab, SpawnSettings settings, string uuid)
    {
        GameObject respawn = Object.Instantiate(respawnPrefab, parentObject.transform);
        respawn.transform.localPosition = new Vector3(0f, settings.heightOffset, 0f);
        respawn.transform.localRotation = Quaternion.Euler(settings.rotationEuler);
        respawn.transform.localScale = respawnPrefab.transform.localScale * settings.scale;
        respawn.name = "RespawnPlace";
        return respawn;
    }

    public GameObject CreateEnemy(GameObject parentObject, GameObject enemyPrefab, SpawnSettings settings, Vector3 basePosition, string displayName)
    {
        GameObject enemy = Object.Instantiate(enemyPrefab);
        enemy.transform.position = basePosition + Vector3.up * settings.heightOffset;
        enemy.transform.SetParent(parentObject.transform, true);
        enemy.transform.localRotation = Quaternion.Euler(settings.rotationEuler);
        enemy.transform.localScale = enemyPrefab.transform.localScale * settings.scale;
        enemy.name = displayName;
        return enemy;
    }

    public GameObject CreateDefeatedEnemy(GameObject parentObject, GameObject killedPrefab, SpawnSettings settings, Vector3 basePosition, string displayName)
    {
        if (killedPrefab == null) return null;
        GameObject defeated = Object.Instantiate(killedPrefab);
        defeated.transform.position = basePosition + Vector3.up * settings.heightOffset;
        defeated.transform.SetParent(parentObject.transform, true);
        defeated.transform.localRotation = Quaternion.Euler(settings.rotationEuler);
        defeated.transform.localScale = killedPrefab.transform.localScale * settings.scale;
        defeated.name = displayName;
        return defeated;
    }

    private string GetShortUuid(string uuid)
    {
        if (string.IsNullOrEmpty(uuid)) return "Unknown";
        return uuid.Length <= 8 ? uuid : uuid.Substring(0, 8);
    }
}
