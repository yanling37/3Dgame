using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Jianglin
{
    /// <summary>
    /// Minimal IMGUI for 降临吧: view switching and returning to the 俯瞰 mode.
    /// </summary>
    public class JianglinHud : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] JianglinPlayerController player;
        [SerializeField] string overviewScene = "Boot";

        JianglinCameraController _cameraControl;

        public void Bind(Camera camera, JianglinPlayerController playerController)
        {
            targetCamera = camera;
            player = playerController;
            if (player != null)
            {
                _cameraControl = player.GetComponentInChildren<JianglinCameraController>();
                if (_cameraControl == null)
                {
                    _cameraControl = FindObjectOfType<JianglinCameraController>();
                }
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                SceneManager.LoadScene(overviewScene);
            }
        }

        void OnGUI()
        {
            GUI.Box(new Rect(10f, 10f, 320f, 122f), "降临吧");

            GUILayout.BeginArea(new Rect(22f, 38f, 296f, 84f));
            GUILayout.Label("WASD 移动 · 侧键1索敌/跟随 · 侧键2换目标 · Tab 切模式");

            string viewButton = _cameraControl != null && _cameraControl.FirstPerson
                ? "切换第三人称 (V)"
                : "切换第一人称 (V)";
            if (GUILayout.Button(viewButton, GUILayout.Height(28f)) && _cameraControl != null)
            {
                _cameraControl.ToggleView();
            }

            if (GUILayout.Button("返回俯瞰 (Backspace)", GUILayout.Height(28f)))
            {
                SceneManager.LoadScene(overviewScene);
            }

            GUILayout.EndArea();
        }
    }
}
