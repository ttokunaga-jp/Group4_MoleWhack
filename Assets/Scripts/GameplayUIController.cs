using UnityEngine;

/// <summary>
/// Gameplay シーン用のシンプルな遷移 UI。
/// 右上に「End」ボタンを表示し、結果シーンへ遷移する。
/// </summary>
public class GameplayUIController : MonoBehaviour
{
    private void OnGUI()
    {
        float width = 100f;
        float height = 30f;
        float padding = 16f;
        Rect rect = new Rect(Screen.width - width - padding, padding, width, height);
        if (GUI.Button(rect, "End"))
        {
            GameFlowController.Instance?.GoToResults();
        }
    }
}
