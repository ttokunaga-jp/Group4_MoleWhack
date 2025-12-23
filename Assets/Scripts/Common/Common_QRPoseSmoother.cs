using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// QR の Pose を IQR で平滑化するユーティリティ。サンプル追加と平滑化取得のみを担当。
/// </summary>
public class Common_QRPoseSmoother
{
    private class PoseSample
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
    }

    private readonly Dictionary<string, List<PoseSample>> poseHistories = new Dictionary<string, List<PoseSample>>();
    private readonly float poseHistorySeconds;
    private readonly float iqrOutlierK;

    public Common_QRPoseSmoother(float poseHistorySeconds = 5f, float iqrOutlierK = 1.5f)
    {
        this.poseHistorySeconds = poseHistorySeconds;
        this.iqrOutlierK = iqrOutlierK;
    }

    public void AddSample(string uuid, Vector3 position, Quaternion rotation)
    {
        if (string.IsNullOrEmpty(uuid)) return;

        if (!poseHistories.TryGetValue(uuid, out var list))
        {
            list = new List<PoseSample>();
            poseHistories[uuid] = list;
        }
        list.Add(new PoseSample { time = Time.time, position = position, rotation = rotation });

        float cutoff = Time.time - poseHistorySeconds;
        list.RemoveAll(s => s.time < cutoff);
    }

    public (Vector3 position, Quaternion rotation) GetSmoothedPose(string uuid, Vector3 fallbackPos, Quaternion fallbackRot)
    {
        if (!poseHistories.TryGetValue(uuid, out var list) || list.Count < 3)
        {
            return (fallbackPos, fallbackRot);
        }

        float SmoothAxis(System.Func<Vector3, float> selector)
        {
            var values = list.Select(s => selector(s.position)).OrderBy(v => v).ToList();
            int n = values.Count;
            if (n < 3) return selector(fallbackPos);

            float Q1 = values[(int)(0.25f * (n - 1))];
            float Q3 = values[(int)(0.75f * (n - 1))];
            float IQR = Q3 - Q1;
            float min = Q1 - iqrOutlierK * IQR;
            float max = Q3 + iqrOutlierK * IQR;

            var filtered = values.Where(v => v >= min && v <= max).ToList();
            if (filtered.Count == 0) return selector(fallbackPos);
            return filtered.Average();
        }

        Vector3 smoothedPos = new Vector3(
            SmoothAxis(p => p.x),
            SmoothAxis(p => p.y),
            SmoothAxis(p => p.z)
        );

        // 回転は最新値と fallback の補間で安定化
        Quaternion latestRot = list[^1].rotation;
        Quaternion smoothedRot = Quaternion.Slerp(fallbackRot, latestRot, 0.2f);

        return (smoothedPos, smoothedRot);
    }
}
