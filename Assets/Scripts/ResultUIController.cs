using UnityEngine;

/// <summary>
/// Results シーンで Setup に戻るためのシンプルな UI。
/// </summary>
public class ResultUIController : MonoBehaviour
{
    private void OnGUI()
    {
        float width = 140f;
        float height = 40f;
        Rect rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        if (GUI.Button(rect, "Back to Setup"))
        {
            GameFlowController.Instance?.GoToSetup();
        }
    }
}
