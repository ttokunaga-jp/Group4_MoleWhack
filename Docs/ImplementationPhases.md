# 実装フェーズ詳細指示書

**対象期間**: 今週中（フェーズ1-3）  
**作業者**: デベロッパー  
**動作確認**: Quest 3S + MRUK環境

---

## 🎯 フェーズ全体スケジュール

```
フェーズ1: QRManager 中央管理化 ← 【今週 1-2日目】 最優先
  ↓
フェーズ2: CameraOrientationMonitor 追加 ← 【2日目午後】 誤判定防止
  ↓
フェーズ3: HitValidator 複合判定実装 ← 【3日目】 ヒット精度向上
  ↓
フェーズ4: UI/デバッグ（オプション）← 【4日目以降】 体験向上
```

---

# PHASE 1: QRManager 中央管理化（1-2時間）

## 目標
- MRUK トラッキング処理を単一の Singleton に統合
- 既存の `QRCodeTracker_MRUK` をリプレース
- `OnQRAdded`, `OnQRUpdated`, `OnQRLost` 中央イベント確立

## 実装ステップ

### S1-1: `QRInfo.cs` データクラス作成

```csharp
// Assets/Scripts/QRInfo.cs
using UnityEngine;

/// <summary>
/// QR コード情報データクラス
/// UUID ごとに追跡情報を保持する（Singleton で管理）
/// </summary>
public class QRInfo
{
    public string uuid;                    // QR の一意識別子
    public Pose firstPose;                 // 初回検出時の World Pose
    public Pose lastPose;                  // 最終観測時の World Pose
    public bool isTracked;                 // 現在追跡中か
    public System.DateTime firstSeenAt;    // 初回検出時刻
    public float lastSeenTime;             // Time.time での最後の目撃時刻

    public QRInfo(string uuid, Pose firstPose)
    {
        this.uuid = uuid;
        this.firstPose = firstPose;
        this.lastPose = firstPose;
        this.isTracked = true;
        this.firstSeenAt = System.DateTime.UtcNow;
        this.lastSeenTime = Time.time;
    }

    public void UpdatePose(Pose newPose)
    {
        this.lastPose = newPose;
        this.lastSeenTime = Time.time;
    }
}
```

**チェックリスト**
- [ ] ファイル作成
- [ ] コンパイル確認

---

### S1-2: `QRManager.cs` Singleton 実装

```csharp
// Assets/Scripts/QRManager.cs
using Meta.XR.MRUtilityKit;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// QRコード トラッキング マネージャー（Singleton）
/// 
/// 責務：
/// - MRUK から Trackable イベントを受け取る
/// - UUID ベースで QR 情報を管理（Dictionary）
/// - 他スクリプト向けにイベント発行
/// - CurrentTrackedUUIDs を公開（カメラ向きチェック用）
/// </summary>
public class QRManager : MonoBehaviour
{
    public static QRManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform targetCubeTransform;
    [SerializeField] private bool enableDetailedLogging = true;

    [Header("Detection Settings")]
    [SerializeField] private float detectionCooldown = 0.5f;
    [SerializeField] private float lostTimeout = 1.0f;

    // ===== イベント定義 =====
    public delegate void OnQRAddedHandler(QRInfo info);
    public delegate void OnQRUpdatedHandler(QRInfo info);
    public delegate void OnQRLostHandler(QRInfo info);

    public event OnQRAddedHandler OnQRAdded;
    public event OnQRUpdatedHandler OnQRUpdated;
    public event OnQRLostHandler OnQRLost;

    // ===== 内部状態 =====
    private Dictionary<string, QRInfo> trackedQRs = new Dictionary<string, QRInfo>();
    private HashSet<string> currentTrackedUUIDs = new HashSet<string>();
    private MRUK mrukInstance;
    private CubeColorOnQr cubeColorChanger;
    private int detectionCount = 0;

    public HashSet<string> CurrentTrackedUUIDs => currentTrackedUUIDs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            LogError("[Awake] Another QRManager already exists. Destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Log("[START] QRManager initializing...");
        
        mrukInstance = MRUK.Instance;
        if (mrukInstance == null)
        {
            LogError("[START] MRUK.Instance is NULL!");
            enabled = false;
            return;
        }

        Log("[START] ✓ MRUK.Instance found");

        // Cube の自動探索
        if (targetCubeTransform == null)
        {
            GameObject cubeObj = GameObject.Find("Cube");
            if (cubeObj != null)
            {
                targetCubeTransform = cubeObj.transform;
                Log("[START] ✓ Found Cube automatically");
            }
        }

        // CubeColorOnQr を取得
        if (targetCubeTransform != null)
        {
            cubeColorChanger = targetCubeTransform.GetComponent<CubeColorOnQr>();
            if (cubeColorChanger != null)
            {
                Log("[START] ✓ CubeColorOnQr component found");
            }
        }

        Log("[START] ✓ Initialization complete");
    }

    private void Update()
    {
        if (mrukInstance == null) return;

        float currentTime = Time.time;

        // MRUK から全 Trackable を取得
        List<MRUKTrackable> allTrackables = new List<MRUKTrackable>();
        mrukInstance.GetTrackables(allTrackables);

        // 現在のフレームで見つかった UUID
        HashSet<string> currentUUIDs = new HashSet<string>();

        // 新規・更新処理
        foreach (var trackable in allTrackables)
        {
            if (trackable == null) continue;

            string uuid = trackable.gameObject.name;
            currentUUIDs.Add(uuid);
            currentTrackedUUIDs.Add(uuid);

            if (trackedQRs.ContainsKey(uuid))
            {
                // 既存 UUID: 位置更新のみ
                QRInfo info = trackedQRs[uuid];
                info.UpdatePose(new Pose(trackable.transform.position, trackable.transform.rotation));
                OnQRUpdated?.Invoke(info);
            }
            else
            {
                // 新規 UUID: 初回検出イベント
                detectionCount++;
                QRInfo newInfo = new QRInfo(
                    uuid,
                    new Pose(trackable.transform.position, trackable.transform.rotation)
                );
                trackedQRs[uuid] = newInfo;

                Log($"[QR_ADDED] QR Code #{detectionCount}: {uuid}");

                // Cube の色変更
                if (cubeColorChanger != null)
                {
                    cubeColorChanger.OnQrRecognized(uuid);
                }

                OnQRAdded?.Invoke(newInfo);
            }
        }

        // タイムアウトによる喪失処理
        List<string> toRemove = new List<string>();
        foreach (var kvp in trackedQRs)
        {
            string uuid = kvp.Key;
            QRInfo info = kvp.Value;

            if (!currentUUIDs.Contains(uuid) && (currentTime - info.lastSeenTime > lostTimeout))
            {
                toRemove.Add(uuid);
            }
        }

        // 喪失イベント発行
        foreach (var uuid in toRemove)
        {
            QRInfo info = trackedQRs[uuid];
            Log($"[QR_LOST] QR Code lost (timeout): {uuid}");

            // Cube をリセット
            if (cubeColorChanger != null)
            {
                cubeColorChanger.ResetToDefault();
            }

            OnQRLost?.Invoke(info);
            trackedQRs.Remove(uuid);
            currentTrackedUUIDs.Remove(uuid);
        }
    }

    public QRInfo GetQRInfo(string uuid)
    {
        return trackedQRs.ContainsKey(uuid) ? trackedQRs[uuid] : null;
    }

    public int GetTrackedQRCount()
    {
        return trackedQRs.Count;
    }

    private void Log(string message)
    {
        if (enableDetailedLogging)
            Debug.Log($"[QRManager] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[QRManager] {message}");
    }
}
```

**チェックリスト**
- [ ] ファイル作成
- [ ] コンパイル確認
- [ ] `QRInfo.cs` 参照が解決

---

### S1-3: 既存スクリプトの切り替え

#### `CubeColorOnQr_MRUK.cs` を修正

**削除する部分**
```csharp
[SerializeField, HideInInspector] private QRCodeTracker_MRUK qrCodeTracker;
// → 削除（QRManager に統合）
```

**追加する Start メソッド**
```csharp
private void Start()
{
    Log("[START] CubeColorOnQr initializing...");
    
    cubeRenderer = GetComponent<Renderer>();
    if (cubeRenderer == null)
    {
        LogError("[START] Renderer component not found!");
        return;
    }

    originalScale = transform.localScale;
    ResetToDefault();

    // QRManager のイベントに登録
    if (QRManager.Instance != null)
    {
        // OnQrRecognized は QRManager から直接呼ばれる（Start 時）
        QRManager.Instance.OnQRLost += OnQRLost;
        Log("[START] ✓ Registered to QRManager events");
    }
    else
    {
        LogError("[START] QRManager instance not found!");
    }
}

private void OnQRLost(QRInfo info)
{
    // QR 喪失時にリセット
    ResetToDefault();
}

private void OnDestroy()
{
    if (QRManager.Instance != null)
    {
        QRManager.Instance.OnQRLost -= OnQRLost;
    }
}
```

**チェックリスト**
- [ ] `OnValidate` メソッド削除
- [ ] `Start` メソッド修正
- [ ] `OnQRLost` ハンドラ追加
- [ ] `OnDestroy` でアンサブスクライブ
- [ ] コンパイル確認

---

#### `QRObjectPositioner.cs` を修正

**削除する**
```csharp
[SerializeField, HideInInspector] private QRCodeTracker_MRUK qrCodeTracker;
private bool subscribed = false;
// AutoAssignTrackerIfNeeded()
// EnsureSubscribed()
// Unsubscribe()
```

**追加する**
```csharp
private void Start()
{
    LogPos("[START] QRObjectPositioner initializing...");

    // QRManager のイベントに登録
    if (QRManager.Instance != null)
    {
        QRManager.Instance.OnQRAdded += OnQRAdded;
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
        QRManager.Instance.OnQRLost -= OnQRLost;
        LogPos("[OnDestroy] ✓ Event listeners unregistered");
    }
}

// 既存の OnQRDetected を OnQRAdded にリネーム
private void OnQRAdded(QRInfo info)
{
    OnQRDetected(info.uuid, info.lastPose.position, info.lastPose.rotation);
}
```

**チェックリスト**
- [ ] イベント登録コード修正
- [ ] `OnQRLost` ハンドラ修正（QRInfo パラメータ対応）
- [ ] コンパイル確認

---

#### `QRHitDetector.cs` を修正

**削除する**
```csharp
[SerializeField, HideInInspector] private QRCodeTracker_MRUK qrCodeTracker;
[SerializeField, HideInInspector] private QRObjectPositioner qrObjectPositioner;
private bool subscribed = false;
// AutoAssignReferences()
// EnsureSubscribed()
// Unsubscribe()
```

**追加する**
```csharp
private void Start()
{
    LogHit("[START] QRHitDetector initializing...");

    // QRManager のイベントに登録
    if (QRManager.Instance != null)
    {
        QRManager.Instance.OnQRLost += HandleQRLost;
        LogHit("[START] ✓ Registered to QRManager events");
    }
    else
    {
        LogErrorHit("[START] QRManager instance not found!");
        enabled = false;
        return;
    }

    LogHit("[START] ✓ QRHitDetector ready");
}

private void OnDestroy()
{
    if (QRManager.Instance != null)
    {
        QRManager.Instance.OnQRLost -= HandleQRLost;
        LogHit("[OnDestroy] ✓ Event listener unregistered");
    }
}

private void HandleQRLost(QRInfo info)
{
    // 既存の HandleQRLost を拡張（QRInfo パラメータ対応）
    totalHits++;
    lastHitTime = Time.time;

    LogHit("========================================");
    LogHit($"[HIT_DETECTED] ★★★ HIT DETECTED ★★★");
    LogHit("========================================");
    LogHit($"[HIT_DETECTED] QR UUID: {info.uuid}");
    LogHit($"[HIT_DETECTED] Total Hits: {totalHits}");
    LogHit($"[HIT_DETECTED] Time: {lastHitTime:F2}s");
    LogHit("========================================");

    OnHitSuccess?.Invoke(info.uuid);
    LogHit($"[HIT_DETECTED] ✓ OnHitSuccess event invoked");
}
```

**チェックリスト**
- [ ] イベント登録修正
- [ ] `HandleQRLost` シグネチャ変更（`QRInfo` パラメータ対応）
- [ ] コンパイル確認

---

### S1-4: Hierarchy 設定

**現在の状態を確認**
- [ ] QRCodeTracker GameObject が Hierarchy にあるか確認
- [ ] 複数の QRCodeTracker がないか確認

**修正**
1. 新規 Empty GameObject 「QRManager」作成
2. `QRManager.cs` スクリプトをアタッチ
3. Hierarchy 上の古い「QRCodeTracker」GameObject を削除（またはコンポーネント削除）
4. Cube Reference を QRManager の Inspector に割り当て

**チェックリスト**
- [ ] QRManager GameObject 作成
- [ ] スクリプト割り当て
- [ ] Cube Reference 設定
- [ ] 古い QRCodeTracker 削除

---

### S1-5: テスト・動作確認

**Unity Editor での動作確認**
```
✓ Play ボタン → ログが [QRManager] [START] で始まる
✓ コンパイルエラーなし
✓ Cube が Hierarchy に存在する
```

**実機テスト（Quest 3S）**
```
✓ QR をかざす
✓ ログに [QR_ADDED] が出現
✓ Cube が色変更
✓ QR をかくす（喪失）
✓ ログに [QR_LOST] が出現
✓ Cube が白に戻る
```

**チェックリスト**
- [ ] Editor コンパイル成功
- [ ] Editor でログ確認
- [ ] 実機ビルド成功
- [ ] 実機でログ確認（adb logcat）
- [ ] QR 検出動作確認
- [ ] QR 喪失動作確認

---

**PHASE 1 完了条件**
✅ QRManager が Singleton で動作  
✅ `OnQRAdded`, `OnQRUpdated`, `OnQRLost` が正常に発火  
✅ Cube 色変更・リセットが正常に動作  

**所要時間**: 1-2時間

---

# PHASE 2: CameraOrientationMonitor 追加（1時間）

## 目標
- 複数 QR 認識状態をカメラ向きの指標に使用
- 誤判定（単独QR時の誤検出）を抑止
- 後続の HitValidator で参照

## 実装ステップ

### S2-1: `CameraOrientationMonitor.cs` 作成

```csharp
// Assets/Scripts/CameraOrientationMonitor.cs
using UnityEngine;

/// <summary>
/// カメラ向きモニター
/// 
/// 責務:
/// - QRManager.CurrentTrackedUUIDs の個数をチェック
/// - minVisibleUUIDs 以上見えている = カメラが正しい向き
/// - HitValidator で参照される
/// </summary>
public class CameraOrientationMonitor : MonoBehaviour
{
    public static CameraOrientationMonitor Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int minVisibleUUIDs = 2;  // 最低何個の QR が見えていれば OK か
    [SerializeField] private bool enableLogging = true;

    public bool IsCameraFacingEnough { get; private set; } = false;
    public int VisibleUUIDCount { get; private set; } = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (QRManager.Instance == null) return;

        // 現在見えている UUID 個数を取得
        VisibleUUIDCount = QRManager.Instance.CurrentTrackedUUIDs.Count;

        // カメラ向き判定（閾値チェック）
        bool previousState = IsCameraFacingEnough;
        IsCameraFacingEnough = (VisibleUUIDCount >= minVisibleUUIDs);

        // 状態変化時にログ
        if (previousState != IsCameraFacingEnough)
        {
            Log($"[ORIENTATION] Camera facing state changed: {IsCameraFacingEnough} (Visible UUIDs: {VisibleUUIDCount})");
        }
    }

    private void Log(string message)
    {
        if (enableLogging)
            Debug.Log($"[CameraOrientationMonitor] {message}");
    }
}
```

**チェックリスト**
- [ ] ファイル作成
- [ ] コンパイル確認

---

### S2-2: Hierarchy 設定

1. 新規 Empty GameObject 「CameraOrientationMonitor」作成
2. スクリプト割り当て
3. Inspector:
   - `minVisibleUUIDs` = 2 (デフォルト)
   - `enableLogging` = true

**チェックリスト**
- [ ] GameObject 作成
- [ ] スクリプト割り当て
- [ ] Inspector 設定

---

### S2-3: テスト・動作確認

**Editor テスト**
```
✓ Play → ログが [CameraOrientationMonitor] で出る
✓ QR 1つ検出 → IsCameraFacingEnough = false
✓ QR 2つ検出 → IsCameraFacingEnough = true
✓ minVisibleUUIDs を変更 → 動作確認
```

**実機テスト**
```
✓ QR を1つ見える位置に → Camera facing: false
✓ QR を2つ見える位置に → Camera facing: true
```

**チェックリスト**
- [ ] Editor ログ確認
- [ ] 実機ログ確認
- [ ] 複数QR認識でフラグ変化

---

**PHASE 2 完了条件**
✅ `CameraOrientationMonitor.Instance.IsCameraFacingEnough` が正常に変わる  
✅ ログで UUID 個数が追跡される  

**所要時間**: 1時間

---

# PHASE 3: HitValidator 複合判定実装（2-3時間）

## 目標
- QR 喪失時に **複合判定**（カメラ向き + QR 喪失）を実行
- 誤判定を排除し、真の HIT のみを検出
- ハンマーは「実物のプラスチック製」を想定し、コントローラー設定やスイング検出は不要

## 実装ステップ

### S3-1: ハードウェア前提の調整

- ハンマーは実物を使用する前提。Unity上でのコントローラー設定やスイング計測は行わない。
- 当たり判定は「QRコードの検出喪失」をトリガーにする（遮蔽や叩きでQRが見えなくなることを利用）。

---

### S3-2: `HitValidator.cs` 実装

```csharp
// Assets/Scripts/HitValidator.cs
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HIT判定バリデーター（複合判定）
/// 
/// QR 喪失時に以下をすべてチェック:
/// 1. CameraOrientationMonitor.IsCameraFacingEnough == true
/// 2. QR 喪失が一定時間内に発生（lostDuration <= maxLossWindow）
/// ※ ハンマーは実物使用のため Unity 側でのスイング検出・距離計測は行わない
/// </summary>
public class HitValidator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraOrientationMonitor cameraMonitor;

    [Header("Hit Settings")]
    [SerializeField] private float maxLossWindow = 0.5f;  // 秒（QR喪失からの判定ウィンドウ）
    [SerializeField] private bool enableDetailedLogging = true;

    [Header("Events")]
    public UnityEvent<string> OnHitSuccess;

    private float lostStartTime = 0f;

    private void Start()
    {
        Log("[START] HitValidator initializing...");

        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRLost += ValidateHit;
            Log("[START] ✓ Registered to QRManager.OnQRLost");
        }
        else
        {
            LogError("[START] QRManager instance not found!");
            enabled = false;
            return;
        }

        if (cameraMonitor == null)
            cameraMonitor = CameraOrientationMonitor.Instance;

        Log("[START] ✓ HitValidator ready");
    }

    private void OnDestroy()
    {
        if (QRManager.Instance != null)
        {
            QRManager.Instance.OnQRLost -= ValidateHit;
        }
    }

    private void ValidateHit(QRInfo info)
    {
        lostStartTime = Time.time;

        // ===== 複合判定開始 =====
        Log($"\n========================================");
        Log($"[HIT_VALIDATION] Checking HIT for UUID: {info.uuid}");
        Log($"========================================");

        // チェック1: カメラ向き
        bool cameraOK = (cameraMonitor != null && cameraMonitor.IsCameraFacingEnough);
        Log($"[HIT_CHECK1] Camera Facing: {cameraOK} (Visible UUIDs: {cameraMonitor?.VisibleUUIDCount ?? 0})");
        if (!cameraOK)
        {
            Log($"[HIT_FAIL] Camera not facing enough - aborting");
            Log($"========================================\n");
            return;
        }

        // チェック2: 喪失ウィンドウ
        float lostDuration = Time.time - lostStartTime;
        bool timeOK = (lostDuration <= maxLossWindow);
        Log($"[HIT_CHECK2] Loss Duration: {lostDuration:F2}s <= {maxLossWindow:F2}s? {timeOK}");
        if (!timeOK)
        {
            Log($"[HIT_FAIL] Loss window exceeded - aborting");
            Log($"========================================\n");
            return;
        }

        // ===== すべてのチェック成功 =====
        Log($"[HIT_SUCCESS] ★★★ ALL CHECKS PASSED ★★★");
        Log($"[HIT_SUCCESS] UUID: {info.uuid}");
        Log($"[HIT_SUCCESS] Position: {info.lastPose.position}");
        Log($"========================================\n");

        // イベント発火
        OnHitSuccess?.Invoke(info.uuid);
        Log($"[HIT_SUCCESS] ✓ OnHitSuccess event invoked");
    }

    private void Log(string message)
    {
        if (enableDetailedLogging)
            Debug.Log($"[HitValidator] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[HitValidator] {message}");
    }
}
```

**チェックリスト**
- [ ] ファイル作成
- [ ] コンパイル確認

---

### S3-3: Hierarchy 設定

1. 既存の「QRHitDetector」GameObject を削除（古い実装）
2. 新規 Empty GameObject 「HitValidator」作成
   - スクリプト割り当て
   - Inspector:
     - `cameraMonitor` = CameraOrientationMonitor の GameObject を割り当て
     - `maxLossWindow` = 0.5

**チェックリスト**
- [ ] HitValidator GameObject 作成
- [ ] 参照設定（cameraMonitor）
- [ ] 古い QRHitDetector 削除

---

### S3-4: テスト・動作確認

**Editor テスト**
```
✓ Play → ログが [HitValidator] [START] で出る
✓ QRManager で QR 検出 → Camera facing が true
✓ QR 喪失 → ValidateHit 呼び出し
✓ 複合判定ログが出る
```

**実機テスト**
```
✓ 複数 QR が見える位置で待機
✓ 実物ハンマーで QR を叩く（QR が隠れて喪失）
✓ ログで複合判定の結果を確認
  - Camera facing: true/false
  - Loss duration: X.XXs
✓ すべて true なら [HIT_SUCCESS] ログが出る
```

**チェックリスト**
- [ ] Editor ログ確認（複合判定が実行される）
- [ ] 実機ビルド成功
- [ ] 実機でQR喪失時に判定実行
- [ ] 実機でHIT成功ログ確認

---

**PHASE 3 完了条件**
✅ `HitValidator.ValidateHit` がカメラ向きと喪失ウィンドウをチェック  
✅ **判定を満たすときのみ** `[HIT_SUCCESS]` ログが出力  

**所要時間**: 2-3時間

---

# PHASE 4: UI/デバッグ・ポーランド（1-2時間、オプション）

## 目標
- スコアシステム実装
- デバッグ用Gizmo追加
- ゲーム体験向上

## 実装内容（簡潔）

### S4-1: ScoreManager.cs

```csharp
// Assets/Scripts/ScoreManager.cs
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI scoreText;  // UI表示用
    [SerializeField] private bool enableLogging = true;

    private int totalScore = 0;
    private int hitCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (HitValidator.Instance != null)
        {
            HitValidator.Instance.OnHitSuccess.AddListener(OnHitSuccess);
        }
        UpdateUI();
    }

    private void OnHitSuccess(string uuid)
    {
        hitCount++;
        totalScore += 10;  // 1HIT = 10点
        Log($"[SCORE] HIT! Total: {totalScore} (Hits: {hitCount})");
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {totalScore}\nHits: {hitCount}";
        }
    }

    private void Log(string message)
    {
        if (enableLogging)
            Debug.Log($"[ScoreManager] {message}");
    }
}
```

### S4-2: デバッグGizmo（QRManager に追加）

```csharp
private void OnDrawGizmos()
{
    if (trackedQRs == null) return;

    foreach (var kvp in trackedQRs.Values)
    {
        // firstPose を赤, lastPose を緑で描画
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(kvp.firstPose.position, 0.1f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(kvp.lastPose.position, 0.1f);

        // 線で繋ぐ
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(kvp.firstPose.position, kvp.lastPose.position);
    }
}
```

---

# 📋 全フェーズの実装チェックリスト

## PHASE 1: QRManager 中央管理化
- [ ] `QRInfo.cs` 作成
- [ ] `QRManager.cs` 作成（Singleton）
- [ ] `CubeColorOnQr.cs` 修正（イベント登録）
- [ ] `QRObjectPositioner.cs` 修正（イベント登録）
- [ ] `QRHitDetector.cs` 修正（イベント登録）
- [ ] Hierarchy: QRManager GameObject 作成・設定
- [ ] Editor テスト成功
- [ ] 実機テスト成功（QR検出・色変更・喪失・リセット）

## PHASE 2: CameraOrientationMonitor 追加
- [ ] `CameraOrientationMonitor.cs` 作成
- [ ] Hierarchy: GameObject 作成・設定
- [ ] Inspector: `minVisibleUUIDs` = 2
- [ ] Editor テスト: QR個数でフラグ変化確認
- [ ] 実機テスト: 複数QR認識確認

## PHASE 3: HitValidator 複合判定
- [ ] `HammerController.cs` 作成
- [ ] `HitValidator.cs` 作成
- [ ] Hierarchy: 両 GameObject 作成・参照設定
- [ ] Hierarchy: 古い QRHitDetector 削除
- [ ] Inspector: hitThreshold=0.35, maxLossWindow=0.5
- [ ] Editor テスト: 複合判定ロジック確認
- [ ] 実機テスト: ハンマースイング → HIT成功

## PHASE 4: UI/デバッグ（オプション）
- [ ] `ScoreManager.cs` 作成
- [ ] デバッグGizmo実装
- [ ] UI Canvas に Score テキスト追加
- [ ] Hierarchy: ScoreManager GameObject 作成
- [ ] テスト: HIT時にスコア加算確認

---

# 🧪 統合テストシナリオ

### テストケース1: 単体QR検出
```
1. QR を1つ用意
2. Play → QRManager.OnQRAdded 発火確認
3. Cube が色変更 ✓
4. QR を隠す → QRManager.OnQRLost 発火確認
5. Cube が白に戻る ✓
```

### テストケース2: カメラ向きチェック
```
1. QR を1つだけ見える位置に配置
2. Camera facing = false 確認 ✓
3. QR を2つ見える位置に移動
4. Camera facing = true 確認 ✓
5. QR を1つ隠す
6. Camera facing = false に戻る ✓
```

### テストケース3: HIT判定（すべて満たす）
```
1. 複数 QR が見える（Camera facing = true）
2. ハンマーをスイング（IsSwinging = true）
3. ハンマーで QR を叩く（distance OK）
4. QR が隠れる（喪失時刻OK）
5. [HIT_SUCCESS] ログが出力 ✓
6. Score 加算（オプション）✓
```

### テストケース4: HIT判定（失敗パターン）
```
4-1. QR1つだけ → Camera facing=false → HIT_FAIL ✓
4-2. スイングしない → IsSwinging=false → HIT_FAIL ✓
4-3. 遠すぎる（distance > 0.35m） → HIT_FAIL ✓
```

---

# 📚 参考資料・ログサンプル

## 正常動作時のログ出力例

```
[QRManager] [START] QRManager initializing...
[QRManager] [START] ✓ MRUK.Instance found
[CameraOrientationMonitor] [UPDATE] Camera facing state changed: False (Visible UUIDs: 1)
[QRManager] [QR_ADDED] QR Code #1: Trackable(Qrcode) 7fa855f2-5a12-ea47-0c03-e4aeba0450ce
[CubeColorOnQr] [QR_RECOGNIZED] ★★★ QR CODE #1 ★★★
[CubeColorOnQr] Color changed to: RGB(0.451, 0.008, 0.729)
[CameraOrientationMonitor] [UPDATE] Camera facing state changed: True (Visible UUIDs: 2)
[HammerController] [SWING] Hammer swing detected! Velocity: 1.23 m/s
[HitValidator] [HIT_VALIDATION] Checking HIT for UUID: 7fa855f2-5a12-ea47-0c03-e4aeba0450ce
[HitValidator] [HIT_CHECK1] Camera Facing: True (Visible UUIDs: 2)
[HitValidator] [HIT_CHECK2] Hammer Swinging: True (Velocity: 1.23 m/s)
[HitValidator] [HIT_CHECK3] Distance: 0.25m <= 0.35m? True
[HitValidator] [HIT_CHECK4] Loss Duration: 0.12s <= 0.50s? True
[HitValidator] [HIT_SUCCESS] ★★★ ALL CHECKS PASSED ★★★
[ScoreManager] [SCORE] HIT! Total: 10 (Hits: 1)
```

---

# ⚠️ よくある問題・対処法

| 問題 | 原因 | 対処 |
|------|------|------|
| QRManager が見つからない | Singleton が未初期化 | Start() 前に参照されている可能性。OnEnable() ではなく Start() で参照 |
| Cube が見えない | cubeColorChanger が null | Cube GameObject の名前が "Cube" か確認 |
| Camera facing が常に false | minVisibleUUIDs が高すぎる | Inspector で値を低く（デフォルト2） |
| HIT_SUCCESS が出ない | 4つのチェックのどれかが失敗 | ログで各チェック結果を確認 |
| ハンマーがスイング検出されない | TipTransform が正しく割り当てられていない | Inspector で確認、またはハンマーのコライダ確認 |

---

**最終完了予定日**: 今週末  
**推定総工数**: 10-14時間（フェーズ1-3）  
**フェーズ4（UI）**: 余力があれば実施
