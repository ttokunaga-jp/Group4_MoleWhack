using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// リザルト表示用のシンプルなプレゼンター。スコアと最後のヒット情報を表示し、リプレイ/セットアップへ遷移させる。
/// </summary>
public class Results_ResultUIController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text legacyScoreText;
    [SerializeField] private TMP_Text tmpScoreText;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button setupButton;

    [Header("Events")]
    public UnityEvent OnReplayRequested;
    public UnityEvent OnSetupRequested;

    private int totalScore;
    private int totalHits;
    private float lastHitTime;
    private string lastHitUuid;

    private void Awake()
    {
        if (replayButton != null) replayButton.onClick.AddListener(RequestReplay);
        if (setupButton != null) setupButton.onClick.AddListener(RequestSetup);
    }

    public void SetSummary(int score, int hits, float lastHitTime, string lastHitUuid)
    {
        this.totalScore = score;
        this.totalHits = hits;
        this.lastHitTime = lastHitTime;
        this.lastHitUuid = lastHitUuid;
        Render();
    }

    private void Render()
    {
        string summary = $"Score: {totalScore}\nHits: {totalHits}\nLast Hit Time: {lastHitTime:F2}s\nLast UUID: {lastHitUuid}";

        if (legacyScoreText != null)
            legacyScoreText.text = summary;

        if (tmpScoreText != null)
            tmpScoreText.text = summary;
    }

    public void RequestReplay()
    {
        OnReplayRequested?.Invoke();
    }

    public void RequestSetup()
    {
        OnSetupRequested?.Invoke();
    }
}
