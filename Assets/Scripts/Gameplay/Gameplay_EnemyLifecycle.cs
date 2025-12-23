using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 単一の敵の出現・被撃破演出を制御する。
/// - Spawn: 下から上昇して指定高さへ
/// - Hit: Defeated プレハブへ差し替え後、沈む演出
/// - 効果音: Spawn/Hit 用の AudioSource を呼び出せるようフックを用意
/// </summary>
public class Gameplay_EnemyLifecycle : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject defeatedPrefab;

    [Header("Animation")]
    [SerializeField] private float riseHeight = 0.3f;
    [SerializeField] private float riseDuration = 0.4f;
    [SerializeField] private float sinkDistance = 0.3f;
    [SerializeField] private float sinkDuration = 0.3f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource spawnAudio;
    [SerializeField] private AudioSource hitAudio;

    [Header("Events")]
    public UnityEvent OnSpawned;
    public UnityEvent OnHit;

    private GameObject currentInstance;
    private Transform targetParent;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isDefeated = false;

    public void Initialize(Transform parent, Vector3 position, Quaternion rotation)
    {
        targetParent = parent;
        targetPosition = position;
        targetRotation = rotation;
    }

    /// <summary>
    /// 敵を spawnPrefab から rise 演出付きで生成する
    /// </summary>
    public void Spawn(GameObject spawnPrefab, float scaleMultiplier = 1f)
    {
        if (spawnPrefab == null || targetParent == null) return;
        CleanupCurrent();

        currentInstance = Instantiate(spawnPrefab, targetParent);
        currentInstance.transform.position = targetPosition - Vector3.up * riseHeight;
        currentInstance.transform.rotation = targetRotation;
        currentInstance.transform.localScale = spawnPrefab.transform.localScale * scaleMultiplier;

        spawnAudio?.Play();
        OnSpawned?.Invoke();
        StartCoroutine(RiseRoutine());
    }

    /// <summary>
    /// ヒット演出: Defeated プレハブに差し替えて沈める
    /// </summary>
    public void HandleHit()
    {
        if (isDefeated) return;
        isDefeated = true;
        hitAudio?.Play();
        OnHit?.Invoke();
        StartCoroutine(SinkRoutine());
    }

    private IEnumerator RiseRoutine()
    {
        float t = 0f;
        Vector3 start = targetPosition - Vector3.up * riseHeight;
        Vector3 end = targetPosition;
        while (t < riseDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / riseDuration);
            if (currentInstance != null)
            {
                currentInstance.transform.position = Vector3.Lerp(start, end, lerp);
            }
            yield return null;
        }
        if (currentInstance != null)
        {
            currentInstance.transform.position = end;
        }
    }

    private IEnumerator SinkRoutine()
    {
        // defeated プレハブに差し替え
        if (defeatedPrefab != null)
        {
            Vector3 pos = currentInstance != null ? currentInstance.transform.position : targetPosition;
            Quaternion rot = currentInstance != null ? currentInstance.transform.rotation : targetRotation;
            float scale = currentInstance != null ? currentInstance.transform.localScale.x : defeatedPrefab.transform.localScale.x;
            CleanupCurrent();
            currentInstance = Instantiate(defeatedPrefab, targetParent);
            currentInstance.transform.position = pos;
            currentInstance.transform.rotation = rot;
            currentInstance.transform.localScale = defeatedPrefab.transform.localScale * scale;
        }

        float t = 0f;
        Vector3 start = currentInstance != null ? currentInstance.transform.position : targetPosition;
        Vector3 end = start - Vector3.up * sinkDistance;
        while (t < sinkDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / sinkDuration);
            if (currentInstance != null)
            {
                currentInstance.transform.position = Vector3.Lerp(start, end, lerp);
            }
            yield return null;
        }
        CleanupCurrent();
    }

    private void CleanupCurrent()
    {
        if (currentInstance != null)
        {
            Destroy(currentInstance);
            currentInstance = null;
        }
    }
}
