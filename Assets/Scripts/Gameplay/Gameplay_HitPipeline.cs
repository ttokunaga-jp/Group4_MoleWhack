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
    [SerializeField] private bool requireTrustStable = true;
    [SerializeField] private float minTrustForHit = 0.6f;
    [SerializeField] private int minVisibleDuringHit = 1;
    [SerializeField] private bool enableDistanceGate = true;
    [SerializeField] private float maxHitDistanceMeters = 1.5f;
    [SerializeField] private bool enableAngleGate = true;
    [SerializeField, Range(0f, 180f)] private float maxAngleDegrees = 60f;
    [SerializeField] private bool enableLogging = true;

    [Header("Events")]
    public UnityEvent<string> OnHitSuccess;

    [Header("Statistics")]
    [SerializeField] private int totalHits = 0;
    [SerializeField] private float lastHitTime = 0f;

    public int TotalHits => totalHits;
    public float LastHitTime => lastHitTime;

    private QRTrustMonitor trustMonitor;

    private void Start()
    {
        if (orientationMonitor == null)
            orientationMonitor = CameraOrientationMonitor.Instance;
        trustMonitor = trustMonitor ?? FindObjectOfType<QRTrustMonitor>();

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

        if (!PassDistanceAndAngleGates(info))
        {
            Log("[GATE] Distance/Angle gate failed. Hit rejected.");
            return;
        }

        // 信頼度と可視UUID数チェック（カメラ向きによる誤判定抑止）
        if (requireTrustStable)
        {
            int visibleCount = QRManager.Instance?.CurrentTrackedUUIDs.Count ?? 0;
            float trust = trustMonitor != null ? trustMonitor.CurrentTrust : 1f;
            Log($"[GATE] Trust {trust:F2} (min {minTrustForHit:F2}), Visible {visibleCount} (min {minVisibleDuringHit})");
            if (trust < minTrustForHit || visibleCount < minVisibleDuringHit)
            {
                Log("[GATE] Trust/visibility gate failed. Hit rejected.");
                return;
            }
        }

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

    private bool PassDistanceAndAngleGates(QRInfo info)
    {
        var cam = Camera.main;
        if (cam == null) return true; // 参照なしならゲート無効として通す

        Vector3 camPos = cam.transform.position;
        Vector3 camFwd = cam.transform.forward;
        Vector3 toQR = info.lastPose.position - camPos;
        float dist = toQR.magnitude;

        if (enableDistanceGate && dist > maxHitDistanceMeters)
        {
            Log($"[GATE] Distance fail: {dist:F2}m > {maxHitDistanceMeters:F2}m");
            return false;
        }

        if (enableAngleGate)
        {
            if (toQR.sqrMagnitude < 1e-6f) return false;
            Vector3 dir = toQR.normalized;
            float dot = Vector3.Dot(camFwd.normalized, dir);
            dot = Mathf.Clamp(dot, -1f, 1f);
            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            if (angle > maxAngleDegrees)
            {
                Log($"[GATE] Angle fail: {angle:F1}deg > {maxAngleDegrees:F1}deg");
                return false;
            }
        }
        return true;
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
