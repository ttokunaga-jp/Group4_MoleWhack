using UnityEngine;

/// <summary>
/// セットアップシーン用の簡易 UI コントローラー。
/// - SetupStart ボタンで収集を開始（自動開始を抑止）
/// - ロック完了後に「確定 / 再調整」ボタンを表示
/// - OnGUI でシンプルに表示するため追加の UI プレハブは不要
/// </summary>
public class SetupUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QRPoseLocker poseLocker;
    [SerializeField] private QRTrustMonitor trustMonitor;
    [SerializeField] private GameFlowController flow;

    private string status = "準備完了";
    private bool hasLocked = false;
    private bool hasFailed = false;

    private void Awake()
    {
        if (poseLocker == null)
            poseLocker = FindObjectOfType<QRPoseLocker>();
        if (trustMonitor == null)
            trustMonitor = FindObjectOfType<QRTrustMonitor>();
        if (flow == null)
            flow = GameFlowController.Instance ?? FindObjectOfType<GameFlowController>();

        if (poseLocker != null)
        {
            // 自動開始を抑止
            poseLocker.AutoStartOnEnable = false;
        }
    }

    private void OnEnable()
    {
        if (poseLocker == null) return;
        poseLocker.OnCollectingStarted += HandleCollectingStarted;
        poseLocker.OnPoseLocked += HandlePoseLocked;
        poseLocker.OnLockFailed += HandleLockFailed;
    }

    private void OnDisable()
    {
        if (poseLocker == null) return;
        poseLocker.OnCollectingStarted -= HandleCollectingStarted;
        poseLocker.OnPoseLocked -= HandlePoseLocked;
        poseLocker.OnLockFailed -= HandleLockFailed;
    }

    private void HandleCollectingStarted()
    {
        hasLocked = false;
        hasFailed = false;
        status = "スキャン中... (10秒)";
    }

    private void HandlePoseLocked(string uuid, Pose pose)
    {
        hasLocked = true;
        hasFailed = false;
        status = $"ロック完了: {poseLocker?.LockedPoseCount ?? 0}件";
    }

    private void HandleLockFailed()
    {
        hasFailed = true;
        hasLocked = false;
        status = "ロック失敗（サンプル不足など）";
    }

    private void OnGUI()
    {
        const float padding = 16f;
        const float infoWidth = 320f;
        const float infoHeight = 70f;

        // ステータス表示（左上）
        GUILayout.BeginArea(new Rect(padding, padding, infoWidth, infoHeight), GUI.skin.box);
        GUILayout.Label($"状態: {status}");
        if (poseLocker != null && poseLocker.State == QRPoseLocker.LockerState.Collecting)
        {
            float elapsed = poseLocker.GetElapsedSeconds();
            float remaining = Mathf.Max(0f, poseLocker.CollectionDuration - elapsed);
            GUILayout.Label($"残り: {remaining:F1}s");
        }
        GUILayout.EndArea();

        if (poseLocker == null)
        {
            GUI.Label(new Rect(padding, padding + infoHeight + 10f, infoWidth, 30f), "QRPoseLocker がシーンにありません。");
            return;
        }

        // Start ボタン（未収集/失敗/リトライ時のみ）中央配置
        if (poseLocker.State == QRPoseLocker.LockerState.Idle || poseLocker.State == QRPoseLocker.LockerState.Failed || poseLocker.State == QRPoseLocker.LockerState.Retry)
        {
            float startWidth = 200f;
            float startHeight = 40f;
            Rect startRect = new Rect((Screen.width - startWidth) * 0.5f, (Screen.height - startHeight) * 0.5f, startWidth, startHeight);
            if (GUI.Button(startRect, "Setup Start"))
            {
                poseLocker.BeginCollect();
                trustMonitor?.BeginSetup();
            }
        }

        // ロック完了後: 確定 / 再調整
        if (hasLocked && poseLocker.State == QRPoseLocker.LockerState.Locked)
        {
            float confirmWidth = 160f;
            float confirmHeight = 32f;
            Rect confirmRect = new Rect(Screen.width - confirmWidth - padding, padding, confirmWidth, confirmHeight);
            if (GUI.Button(confirmRect, "GameStart"))
            {
                if (trustMonitor != null && trustMonitor.CurrentTrust < trustMonitor.TrustLowThreshold)
                {
                    status = "信頼度が不足しています。再調整してください。";
                }
                else
                {
                    status = "ロック確定済み";
                    trustMonitor?.BeginGameplay();
                    flow?.GoToGameplay();
                }
            }

            float retryWidth = 140f;
            float retryHeight = 32f;
            Rect retryRect = new Rect(padding, padding + infoHeight + 10f, retryWidth, retryHeight);
            if (GUI.Button(retryRect, "Setup Again"))
            {
                poseLocker.Retry();
                trustMonitor?.BeginSetup();
            }
        }

        // 失敗時: 再調整
        if (hasFailed && poseLocker.State == QRPoseLocker.LockerState.Failed)
        {
            float retryWidth = 140f;
            float retryHeight = 32f;
            Rect retryRect = new Rect(padding, padding + infoHeight + 10f, retryWidth, retryHeight);
            if (GUI.Button(retryRect, "Setup Again"))
            {
                poseLocker.Retry();
                trustMonitor?.BeginSetup();
            }
        }
    }
}
