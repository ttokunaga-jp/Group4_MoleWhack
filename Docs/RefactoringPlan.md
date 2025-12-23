# リファクタリング計画（スクリプト整理を最優先）

旧計画は一旦後ろ倒しし、シーン別の責務分離とスクリプト整理を優先する。

## 1. スクリプト配置と命名（現状反映）

- フォルダ: `Assets/Scripts/{Setup,Gameplay,Results,Common}`
- 配置
  - Common: `Common_QRManager.cs`, `Common_QRInfo.cs`, `Common_QRManagerBootstrap.cs`, `Common_CameraOrientationMonitor.cs`, `Common_GameFlowController.cs`, `Common_GameSessionManager.cs`, `Common_GameSystemsBootstrap.cs`, `Common_ScoreManager.cs`, `Common_LogToFile.cs`
  - Setup: `Setup_QRObjectPositioner.cs`, `Setup_PermissionRequester.cs`, `Setup_QRPoseLocker.cs`, `Setup_QRTrustMonitor.cs`, `Setup_SetupUIController.cs`, `Setup_SetupXRUIController.cs`
  - Gameplay: `Gameplay_CubeColorOnQr.cs`, `Gameplay_HitValidator.cs`, `Gameplay_GameplayUIController.cs`, `Gameplay_GameplayXRUIController.cs`
  - Results: `Results_ResultUIController.cs`

## 2. 責務の現状と分割/統合の提案

- Common_QRManager  
  - 役割: MRUK Trackable を集約し `OnQRAdded/OnQRUpdated/OnQRLost` と `CurrentTrackedUUIDs` を公開。  
  - 分割案: MRUK連携とイベント配信を `Common_QRTrackingService`（取得・喪失判定）と `Common_QREventHub`（イベントハブ）に分離しテスト容易化。

- Common_CameraOrientationMonitor  
  - 役割: 可視UUID数からカメラ向き判定。  
  - 分割案: カウント取得を `Common_VisibilityCounter` に切出し、Monitor は閾値・履歴のみ担当。

- Common_GameFlowController / Common_GameSessionManager / Common_ScoreManager  
  - 役割: シーン遷移・ゲーム進行・スコア管理の核。  
  - 改善案: 進行/スコアをイベントドリブン化する小型イベントバスを用意（UI/演出と疎結合に）。`autoStart`/`autoResults` を Scriptable 設定に出すとビルド分けが容易。

- Setup_QRObjectPositioner  
  - 役割: QR に紐づく RespawnPlace（旧Cube）と Enemy（旧Sphere）生成/追従、IQR 平滑化。  
  - 分割案:
    1. `Setup_QRPrefabResolver`（Prefab解決とInspector補完）
    2. `Setup_QRAnchorFactory`（オブジェクト生成と再利用）
    3. `Common_QRPoseSmoother`（IQR平滑化ユーティリティ）  
    → Positioner本体はイベント購読と配置に集中。

- Setup_QRPoseLocker / Setup_QRTrustMonitor  
  - 役割: ポーズロックとUUID信頼度管理。  
  - 改善案: 状態機械（Idle/Collecting/Locked/Failed）を明示し、UI へイベントで通知。距離しきい値/サンプル時間を Inspector に出す。

- Gameplay_HitValidator  
  - 役割: OnQRLost をもとに複合判定（カメラ向き＋喪失ウィンドウ）。  
  - 分割案: `Gameplay_HitPipeline` を新設し、OrientationGate/TimingGate/OnSuccess をモジュール化。`Gameplay_QRHitDetector` 相当の統計はパイプラインに吸収。

- Gameplay_CubeColorOnQr  
  - 役割: 色・スケール演出。  
  - 分割案: 色ハッシュを `Common_QRColorUtility` に切出し、演出部分を `Gameplay_QRVisualFeedback` として分離すると Prefab 差し替えが容易。

- UI コントローラ群  
  - `Setup_*UIController`, `Gameplay_*UIController`, `Results_ResultUIController` はシーン固有。  
  - 改善案: 共通 UI イベントを `Common_GameEventBus` でまとめ、各シーン UI はイベント購読のみにする。

## 3. シーンごとの責務（設計方針）

1) Setup シーン  
- 権限取得 → MRUK 起動確認 → Pose ロック/信頼度チェック → 配置。  
- ロック完了イベントで Gameplay へ遷移、失敗時はリトライ導線。

2) Gameplay シーン  
- Locked Pose を用いて配置（基本は更新しない）。  
- OnQRLost → HitPipeline → スコア/演出。  
- セッション時間管理（カウントダウン→プレイ→終了）。

3) Results シーン  
- スコア/統計表示、リプレイ/再セットアップボタン。  
- 将来は CSV/JSON エクスポートを Results_* に追加。

## 4. 直近タスク（優先順）

1. `Gameplay_HitPipeline` を実装し、`Gameplay_HitValidator` をパイプライン経由に整理。  
2. `Common_QRPoseSmoother` と `Setup_QRAnchorFactory` を導入し、Positioner を薄くする。  
3. `Setup_QRPrefabResolver` を用意し、Prefab未設定時の Resource ロード/警告を一箇所に集約。  
4. `Results_ResultUIController` でスコア/統計表示を仕上げ、Replay/Setup ボタンのハンドラを GameFlowController に接続。  
5. 旧フェーズ計画（ImplementationPhases 等）は上記整理後に再評価。

## 5. メモ

- ファイル名に `Common_/Setup_/Gameplay_/Results_` を付与済み。クラス名は現行のまま（Prefab/シーン参照を崩さないため）。クラス名を変える場合は MonoScript 差し替えを前提に別タスク化。  
- 大きな分割/統合は1機能ずつ進める（ブランチ/PRを分けてレビューしやすくする）。
