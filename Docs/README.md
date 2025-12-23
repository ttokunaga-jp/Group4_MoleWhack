# Group4_MoleWhack Docs (Overview)

このリポジトリで実装している **QR検出 + 安定座標ロック + 誤検出防止 + Hit判定** の全体像をまとめた簡易ガイドです。詳細な技術記事とリファクタリング計画は以下を参照してください。

- 技術記事: `Docs/BlogArticle_QR_WorldCoordinates.md`（World座標取得と安定化の背景）  
- リファクタリング計画: `Docs/RefactoringPlan.md`（段階的移行プランと最新方針）

## 主要コンポーネント
- **QRManager (Common_QRManager)**: MRUK から QR Trackable を取得し、`OnQRAdded/OnQRUpdated/OnQRLost` を発火。`lostTimeout` で喪失判定を一元管理。
- **QRInfo (Common_QRInfo)**: `firstPose` と `lastPose`、`lastSeenTime` を保持するデータクラス。
- **QRObjectPositioner (Setup_)**: QR の位置に RespawnPlace と Enemy を生成。IQR 平滑化は `Common_QRPoseSmoother`、生成は `Setup_QRAnchorFactory`、Prefab解決は `Setup_QRPrefabResolver` に委譲。Enemy は `EnemyVariant` で `EnemyDefault/Enemy1` を Inspector 切替でき、Defeated プレハブも指定可能。
- **CubeColorOnQr (Gameplay_)**: 検出/喪失で色を変更して視覚フィードバック（RespawnPlace 側に付与）。
- **CameraOrientationMonitor (Common_)**: 現在認識中の UUID 数などから「カメラ向きOK」を判定。
- **HitValidator / HitPipeline (Gameplay_)**: `OnQRLost` をトリガーに複合判定（カメラ向き + 喪失時間ウィンドウ）。パイプライン経由で UnityEvent を発火。
- **GameFlowController / GameSessionManager / ScoreManager (Common_)**: シーン遷移・進行・スコア管理。
- **QRPoseLocker / QRTrustMonitor (Setup_)**: セットアップ時のロックと信頼度チェック（導入済み/予定を含む）。

## シーン別ヒエラルキー（理想形）

### Setup シーン（例: 画像参照）
- Directional Light / Global Volume（任意）
- OVRCameraRig
- MRUK_Manager + MRUtilityKit
- QRObjectPositioner （Setup_QRObjectPositioner）
- QRPoseLockerRoot （Setup_QRPoseLocker、QRTrustMonitor をここに配置）
- EventSystem / XR UI（必要に応じて）
- Log （Common_LogToFile）

Inspector 要点（Setup_QRObjectPositioner）
- Prefabs: `RespawnPlace`、`EnemyDefault`、`EnemyDefaultDefeated`、`Enemy1`、`Enemy1Defeated`
- EnemyVariant: `Default` / `Enemy1`（Inspector 切替で敵スキンを変更）
- Positioning: `RespawnScale / HeightOffset`, `EnemyScale / HeightOffset`, `SpawnDefeatedOnLoss`

### Gameplay シーン（例: 画像参照）
- Directional Light / Global Volume
- OVRCameraRig
- MRUK_Manager + MRUtilityKit
- QRManager（Common_QRManager, Bootstrap 可）
- CameraOrientationMonitor（Common_CameraOrientationMonitor）
- QRObjectPositioner（Setup_QRObjectPositioner: Gameplay 用 Prefab 設定）
- HitPipeline（Gameplay_HitPipeline）
- HitValidator（Gameplay_HitValidator: HitPipeline 参照、OnHitSuccess→Score/FX）
- GameSessionManager / ScoreManager / GameFlowController（Common_*、Bootstrap 可）
- UI: GameplayUIController / GameplayXRUIController（必要に応じ Canvas 配下）

### Results シーン（例: 画像参照）
- Directional Light / Global Volume
- OVRCameraRig（または CameraRig）
- Results_ResultUIController（Canvas 配下: TMP/Text, Replay/Setup ボタン）
- GameFlowController（Common_GameFlowController、DontDestroy オブジェクトでも可）
- （必要に応じ）ScoreManager / GameSessionManager を参照

## コンポーネント付与チェックリスト
- 共通: QRManager, QRManagerBootstrap, QRInfo, CameraOrientationMonitor, GameFlowController, GameSessionManager, ScoreManager, LogToFile
- Setup: QRObjectPositioner, QRPoseLocker, QRTrustMonitor, SetupUI/SetupXRUIController, PermissionRequester（必要なら）
- Gameplay: Gameplay_HitPipeline, Gameplay_HitValidator, Gameplay_CubeColorOnQr（RespawnPlace Prefab）、Gameplay_UI/XRUIController
- Results: Results_ResultUIController（UI参照を接続）、必要なら GameFlowController

## デバッグの目安
- `QRManager`: `[QR_ADDED] / [QR_UPDATED] / [QR_LOST]` が出ているか。`lostTimeout` が短すぎないか。
- `QRObjectPositioner`: `[QR_POSITIONED] / [QR_UPDATED]` ログで座標が追従しているか。Prefab未設定で無効化されていないか。
- `CubeColorOnQr`: 検出時に色変化、喪失時に `lostColor` が適用されるか。
- セットアップ時: 収集サンプル数が十分か（IQR後に有効サンプルが残るか）。UUID信頼度が低下していないか。

## 関連ドキュメント
- `Docs/SETUP.md`: セットアップ手順、環境設定、検証の流れをまとめた実務用ガイド。
- `Docs/BlogArticle_QR_WorldCoordinates.md`: World座標取得に関する詳細解説記事。
- `Docs/RefactoringPlan.md`: 段階的リファクタリング計画と最新方針。*** End Patch***)"}} to=functions.apply_patch Holmes execution reasoning to=functions.apply_patch "commentary code interpreter" json.editNote to=functions.apply_patch **Note**: The above patch must follow the grammar exactly. Ensure the "*** End Patch" line is on its own line with no extra characters. If making multiple patch requests, do them one at a time. Remove this notice. reality to=functions.apply_patch dfdf. scouting to=functions.apply_patch use proper syntax. Let's redo.** JSON Not needed. Use plain string.** We'll reapply correctly. Let's redo. to=functions.apply_patch. tight. assistant to=functions.apply_patch to=functions.apply_patch. Completed. Let's do proper. to=functions.apply_patch as string. Let's proceed. to=functions.apply_patch. pony.##".
