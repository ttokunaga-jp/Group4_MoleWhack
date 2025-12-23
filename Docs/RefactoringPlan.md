# リファクタリング計画（フェーズB/C/Dのみ）

完了済みは **done** と記載。残りはこの順に進める。

## フェーズB: 座標ロック（Setup 30秒 IQR 固定） — **done**
- `QRPoseLocker`: Idle/Collecting/Locked/Failed/Retry 状態管理、OnQRUpdated を 30 秒収集（IsTracked=true のみ）、IQR で外れ値除外しロック。API: `BeginCollect`, `Abort`, `Retry`, `GetLockedPose`.
- `QRManager`: isTracked を更新（Added/Updated で true、Lost で false）。
- `QRObjectPositioner`: `useLockedPoseOnly` で Locked Pose を一度だけ配置し、ロック後は更新しない。

## フェーズC: 誤検出防止（UUID集合 + 距離しきい値） — **done**
- `QRTrustMonitor`: Setup 中に同時可視かつ距離しきい値（初期 1m）内の UUID を既知集合に登録。Gameplay 中に可視集合と比較し信頼度を算出（未知増加や既知欠落で低下）。Inspector で距離/閾値調整可。

## フェーズD: シーン分割とフロー強化（進行・UI 仕上げ） — **todo**
- Gameplay: HitPipeline/HitValidator を接続し、Score/FX イベントを統合。
- Results: Results_ResultUIController をスコア/統計表示＋ Replay/Setup 遷移に接続。
- 共通: GameFlowController / GameSessionManager / ScoreManager のイベントドリブン化（任意）。

## 備考
- スクリプト配置: `Assets/Scripts/{Setup,Gameplay,Results,Common}`。ファイル名は `Common_/Setup_/Gameplay_/Results_`。
- 色変更ロジックは削除済み（CubeColor 系）。*** End Patch***
