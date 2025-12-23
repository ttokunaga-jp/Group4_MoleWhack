using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// QR喪失イベントを単一パイプラインで処理し、ヒット成功を判定して通知する。
/// - OrientationGate: CameraOrientationMonitor で視認数を確認（任意）
/// - TimingGate: 喪失からの経過時間をチェック
/// - 成功時に UnityEvent<string> を発火（UUID付き）
/// </summary>
public class Gameplay_HitPipeline : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraOrientationMonitor orientationMonitor;

    [Header("Settings")]
    [SerializeField] private bool requireOrientation = true;
    [SerializeField] private float maxLossWindow = 0.5f;
    [SerializeField] private bool enableLogging = true;

    [Header("Events")]
    public UnityEvent<string> OnHitSuccess;

    [Header("Statistics")]
    [SerializeField] private int totalHits = 0;
    [SerializeField] private float lastHitTime = 0f;

    public int TotalHits => totalHits;
    public float LastHitTime => lastHitTime;

    private void Start()
    {
        if (orientationMonitor == null)
            orientationMonitor = CameraOrientationMonitor.Instance;

        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRLost += HandleQRLost;
            Log("[START] Registered to QRManager.OnQRLost");
        }
        else
        {
            LogError("[START] QRManager instance not found. Disabling pipeline.");
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRLost -= HandleQRLost;
        }
    }

    private void HandleQRLost(QRInfo info)
    {
        if (info == null)
        {
            LogError("[QRLost] QRInfo is null");
            return;
        }

        float lostDuration = Time.time - info.lastSeenTime;

        if (requireOrientation)
        {
            if (orientationMonitor == null)
                orientationMonitor = CameraOrientationMonitor.Instance;

            bool orientationOK = (orientationMonitor != null && orientationMonitor.IsCameraFacingEnough);
            Log($"[GATE] Orientation OK: {orientationOK} (Visible UUIDs: {orientationMonitor?.VisibleUUIDCount ?? -1})");
            if (!orientationOK)
            {
                Log("[GATE] Orientation failed. Hit rejected.");
                return;
            }
        }

        bool timingOK = (lostDuration <= maxLossWindow);
        Log($"[GATE] Timing OK: {timingOK} (lost {lostDuration:F2}s / window {maxLossWindow:F2}s)");
        if (!timingOK)
        {
            Log("[GATE] Timing failed. Hit rejected.");
            return;
        }

        totalHits++;
        lastHitTime = Time.time;

        Log($"[HIT_SUCCESS] UUID: {info.uuid} | TotalHits: {totalHits} | LostDuration: {lostDuration:F2}s");
        OnHitSuccess?.Invoke(info.uuid);
    }

    private void Log(string message)
    {
        if (enableLogging)
            Debug.Log($"[Gameplay_HitPipeline] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[Gameplay_HitPipeline] {message}");
    }
}
