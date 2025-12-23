using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// QR の Pose に基づいて RespawnPlace（旧Cube）と Enemy（旧Sphere）を生成・更新する。
/// IQR 平滑化を Common_QRPoseSmoother に委譲し、生成は Setup_QRAnchorFactory で行う。
/// </summary>
public class QRObjectPositioner : MonoBehaviour
{
    public enum EnemyVariant
    {
        Default,
        Enemy1
    }

    [Header("Prefabs")]
    [SerializeField] private GameObject respawnPrefab;
    [SerializeField] private GameObject enemyDefaultPrefab;
    [SerializeField] private GameObject enemyDefaultDefeatedPrefab;
    [SerializeField] private GameObject enemy1Prefab;
    [SerializeField] private GameObject enemy1DefeatedPrefab;
    [SerializeField] private QRPoseLocker poseLocker;

    [Header("Settings")]
    [SerializeField] private Vector3 positionOffset = Vector3.zero;
    [SerializeField] private bool rotateWithQR = true;
    [SerializeField] private bool spawnDefeatedOnLoss = true;
    [SerializeField] private bool enablePositioningLogging = true;
    [SerializeField] private bool useLockedPoseOnly = true;
    [SerializeField] private EnemyVariant enemyVariant = EnemyVariant.Default;

    [Header("Transform Settings")]
    [SerializeField] private Setup_QRAnchorFactory.SpawnSettings respawnSettings = new Setup_QRAnchorFactory.SpawnSettings
    {
        scale = 0.1f,
        heightOffset = -0.1f,
        rotationEuler = new Vector3(90f, 0f, 0f)
    };
    [SerializeField] private Setup_QRAnchorFactory.SpawnSettings enemyDefaultSettings = new Setup_QRAnchorFactory.SpawnSettings
    {
        scale = 0.5f,
        heightOffset = 0.0f,
        rotationEuler = new Vector3(90f, 0f, 0f)
    };
    [SerializeField] private Setup_QRAnchorFactory.SpawnSettings enemyDefaultDefeatedSettings = new Setup_QRAnchorFactory.SpawnSettings
    {
        scale = 0.5f,
        heightOffset = 0.0f,
        rotationEuler = new Vector3(90f, 0f, 0f)
    };
    [SerializeField] private Setup_QRAnchorFactory.SpawnSettings enemy1Settings = new Setup_QRAnchorFactory.SpawnSettings
    {
        scale = 0.5f,
        heightOffset = 0.0f,
        rotationEuler = new Vector3(90f, 0f, 0f)
    };
    [SerializeField] private Setup_QRAnchorFactory.SpawnSettings enemy1DefeatedSettings = new Setup_QRAnchorFactory.SpawnSettings
    {
        scale = 0.5f,
        heightOffset = 0.0f,
        rotationEuler = new Vector3(90f, 0f, 0f)
    };

    private readonly Dictionary<string, GameObject> qrMarkerObjects = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> qrEnemyObjects = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> qrDefeatedObjects = new Dictionary<string, GameObject>();

    private Common_QRPoseSmoother poseSmoother;
    private Setup_QRAnchorFactory anchorFactory;
    private Setup_QRPrefabResolver prefabResolver;
    private readonly HashSet<string> placedLocked = new HashSet<string>();

    private void Awake()
    {
        poseSmoother = new Common_QRPoseSmoother();
        anchorFactory = new Setup_QRAnchorFactory();
        prefabResolver = new Setup_QRPrefabResolver();
    }

    private void Start()
    {
        LogPos("[START] QRObjectPositioner initializing...");

        respawnPrefab = prefabResolver.ResolveRespawn(respawnPrefab);
        enemyDefaultPrefab = prefabResolver.ResolveEnemyDefault(enemyDefaultPrefab);
        enemyDefaultDefeatedPrefab = prefabResolver.ResolveEnemyDefaultDefeated(enemyDefaultDefeatedPrefab);
        enemy1Prefab = prefabResolver.ResolveEnemy1(enemy1Prefab);
        enemy1DefeatedPrefab = prefabResolver.ResolveEnemy1Defeated(enemy1DefeatedPrefab);

        if (respawnPrefab == null || GetActiveEnemyPrefab() == null)
        {
            LogErrorPos("[START] Required prefabs are missing. Assign Respawn/Enemy prefabs or place them under Resources.");
            enabled = false;
            return;
        }

        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRAdded += OnQRAdded;
            QRManager.Instance.OnQRUpdated += OnQRUpdated;
            QRManager.Instance.OnQRLost += OnQRLost;
            LogPos("[START] ✓ Registered to QRManager events");
        }
        else
        {
            LogErrorPos("[START] QRManager instance not found!");
            enabled = false;
            return;
        }

        LogPos("[START] ✓ QRObjectPositioner ready");
    }

    private void OnDestroy()
    {
        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRAdded -= OnQRAdded;
            QRManager.Instance.OnQRUpdated -= OnQRUpdated;
            QRManager.Instance.OnQRLost -= OnQRLost;
        }
    }

    private void OnQRAdded(QRInfo info)
    {
        if (info == null) return;
        Pose poseToUse = info.lastPose;
        if (useLockedPoseOnly && poseLocker != null && poseLocker.State == QRPoseLocker.LockerState.Locked && poseLocker.GetLockedPose(info.uuid, out var locked))
        {
            poseToUse = locked;
        }
        OnQRDetected(info.uuid, poseToUse.position, poseToUse.rotation);
    }

    private void OnQRUpdated(QRInfo info)
    {
        if (info == null) return;
        if (!qrMarkerObjects.ContainsKey(info.uuid)) return;

        if (useLockedPoseOnly && poseLocker != null && poseLocker.State == QRPoseLocker.LockerState.Locked)
        {
            // ロック済みなら更新しない
            return;
        }

        Vector3 finalPosition = info.lastPose.position + positionOffset;
        Quaternion finalRotation = rotateWithQR ? info.lastPose.rotation : Quaternion.identity;

        poseSmoother.AddSample(info.uuid, info.lastPose.position, info.lastPose.rotation);
        (Vector3 smoothedPos, Quaternion smoothedRot) = poseSmoother.GetSmoothedPose(info.uuid, finalPosition, finalRotation);

        if (qrMarkerObjects.TryGetValue(info.uuid, out var parentObject) && parentObject != null)
        {
            parentObject.transform.position = smoothedPos;
            if (rotateWithQR)
            {
                parentObject.transform.rotation = smoothedRot;
            }
        }

        if (qrEnemyObjects.TryGetValue(info.uuid, out var enemy) && enemy != null)
        {
            var activeSettings = GetActiveEnemySettings();
            enemy.transform.position = smoothedPos + Vector3.up * activeSettings.heightOffset;
        }

        LogPos($"[QR_UPDATED] Pose updated for QR: {info.uuid}");
    }

    private void OnQRDetected(string uuid, Vector3 position, Quaternion rotation)
    {
        LogPos($"[QR_DETECTED] UUID: {uuid}");

        Vector3 finalPosition = position + positionOffset;
        Quaternion finalRotation = rotateWithQR ? rotation : Quaternion.identity;

        if (!qrMarkerObjects.ContainsKey(uuid))
        {
            GameObject parentObject = anchorFactory.CreateParent(uuid, finalPosition, finalRotation, transform);
            anchorFactory.CreateRespawn(parentObject, respawnPrefab, respawnSettings, uuid);

            Setup_QRAnchorFactory.SpawnSettings activeSettings = GetActiveEnemySettings();
            GameObject enemy = anchorFactory.CreateEnemy(parentObject, GetActiveEnemyPrefab(), activeSettings, finalPosition, GetActiveEnemyName());

            qrMarkerObjects[uuid] = parentObject;
            qrEnemyObjects[uuid] = enemy;
            if (useLockedPoseOnly && poseLocker != null && poseLocker.State == QRPoseLocker.LockerState.Locked)
            {
                placedLocked.Add(uuid);
            }

            LogPos($"[QR_POSITIONED] ✓ Marker + Enemy created for QR: {uuid}");
        }
        else
        {
            // Locked 配置を繰り返さない
            if (useLockedPoseOnly && placedLocked.Contains(uuid))
            {
                return;
            }

            GameObject existingObject = qrMarkerObjects[uuid];
            existingObject.transform.position = finalPosition;
            if (rotateWithQR)
            {
                existingObject.transform.rotation = finalRotation;
            }

            if (!qrEnemyObjects.ContainsKey(uuid) || qrEnemyObjects[uuid] == null)
            {
                Setup_QRAnchorFactory.SpawnSettings activeSettings = GetActiveEnemySettings();
                GameObject enemy = anchorFactory.CreateEnemy(existingObject, GetActiveEnemyPrefab(), activeSettings, existingObject.transform.position, GetActiveEnemyName());
                qrEnemyObjects[uuid] = enemy;
                LogPos($"[QR_RESPAWN] ✓ Enemy respawned for QR: {uuid}");
            }

            LogPos($"[QR_POSITIONED] ✓ Marker updated for QR: {uuid}");
        }
    }

    private void OnQRLost(QRInfo info)
    {
        if (info == null)
        {
            LogErrorPos("[QR_LOST] QRInfo is null");
            return;
        }

        string uuid = info.uuid;
        LogPos($"[QR_LOST] UUID: {uuid}");

        if (qrEnemyObjects.ContainsKey(uuid))
        {
            var enemy = qrEnemyObjects[uuid];
            if (enemy != null)
            {
                Destroy(enemy);
                LogPos($"[QR_REMOVED] ✓ Enemy destroyed: {enemy.name}");
            }
            qrEnemyObjects.Remove(uuid);

            if (spawnDefeatedOnLoss && GetDefeatedEnemyPrefab() != null && qrMarkerObjects.TryGetValue(uuid, out var parent))
            {
                Setup_QRAnchorFactory.SpawnSettings defeatedSettings = GetDefeatedEnemySettings();
                Vector3 basePos = parent.transform.position;
                var defeated = anchorFactory.CreateDefeatedEnemy(parent, GetDefeatedEnemyPrefab(), defeatedSettings, basePos, GetDefeatedEnemyName());
                if (defeated != null)
                {
                    qrDefeatedObjects[uuid] = defeated;
                    LogPos($"[QR_REMOVED] ✓ Defeated enemy spawned: {defeated.name}");
                }
            }
        }
        else
        {
            LogWarningPos($"[QR_LOST] ⚠ No Enemy found for UUID: {uuid}");
        }
    }

    public int GetPositionedObjectCount() => qrMarkerObjects.Count;
    public int GetActiveEnemyCount() => qrEnemyObjects.Count;

    public void ClearAllEnemies()
    {
        foreach (var enemy in qrEnemyObjects.Values)
        {
            if (enemy != null) Destroy(enemy);
        }
        qrEnemyObjects.Clear();
        LogPos("[CLEAR] ✓ All enemies cleared");
    }

    public void ClearAllObjects()
    {
        foreach (var obj in qrMarkerObjects.Values)
        {
            if (obj != null) Destroy(obj);
        }
        qrMarkerObjects.Clear();
        qrEnemyObjects.Clear();
        qrDefeatedObjects.Clear();
        LogPos("[CLEAR] ✓ All QR objects cleared");
    }

    private void LogPos(string message)
    {
        if (enablePositioningLogging)
        {
            Debug.Log($"[QRObjectPositioner] {message}");
        }
    }

    private void LogWarningPos(string message)
    {
        Debug.LogWarning($"[QRObjectPositioner] {message}");
    }

    private void LogErrorPos(string message)
    {
        Debug.LogError($"[QRObjectPositioner] {message}");
    }

    private GameObject GetActiveEnemyPrefab()
    {
        return enemyVariant == EnemyVariant.Default ? enemyDefaultPrefab : enemy1Prefab;
    }

    private GameObject GetDefeatedEnemyPrefab()
    {
        return enemyVariant == EnemyVariant.Default ? enemyDefaultDefeatedPrefab : enemy1DefeatedPrefab;
    }

    private string GetActiveEnemyName()
    {
        return enemyVariant == EnemyVariant.Default ? "EnemyDefault" : "Enemy1";
    }

    private string GetDefeatedEnemyName()
    {
        return enemyVariant == EnemyVariant.Default ? "EnemyDefaultDefeated" : "Enemy1Defeated";
    }

    private Setup_QRAnchorFactory.SpawnSettings GetActiveEnemySettings()
    {
        return enemyVariant == EnemyVariant.Default ? enemyDefaultSettings : enemy1Settings;
    }

    private Setup_QRAnchorFactory.SpawnSettings GetDefeatedEnemySettings()
    {
        return enemyVariant == EnemyVariant.Default ? enemyDefaultDefeatedSettings : enemy1DefeatedSettings;
    }
}
