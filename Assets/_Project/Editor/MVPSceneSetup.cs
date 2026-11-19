#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DanceUp.AI;

namespace DanceUp.Editor
{
    public static class MVPSceneSetup
    {
        private const string MVP_SCENE_PATH = "Assets/_Project/Scenes/DanceReplayMVP_Scene.unity";
        private const string MAIN_SCENE_PATH = "Assets/Scenes/SampleScene.unity";

        [MenuItem("DanceUp/Cảnh/Tạo và Mở Cảnh MVP (DanceReplayMVP 3D)", false, 20)]
        public static void CreateOrOpenMVPScene()
        {
            if (System.IO.File.Exists(MVP_SCENE_PATH))
            {
                EditorSceneManager.OpenScene(MVP_SCENE_PATH);
                Debug.Log("[MVPSceneSetup] Đã mở cảnh: " + MVP_SCENE_PATH);
                return;
            }

            // Tạo mới scene nếu chưa có
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0.5f, -9f);
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);

            // 2. Directional Light
            var lightGo = new GameObject("Directional Light", typeof(Light));
            lightGo.transform.position = new Vector3(0f, 3f, 0f);
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;

            // 3. DanceReplayMVP
            var mvpGo = new GameObject("DanceReplayMVP", typeof(DanceReplayMVP), typeof(DanceMLPipeline));
            mvpGo.transform.position = Vector3.zero;
            var mvp = mvpGo.GetComponent<DanceReplayMVP>();
            mvp.mode = DanceReplayMVP.PlayMode.LiveUdpStream;
            mvp.autoStartPythonInLiveMode = true;
            mvp.jsonFileName = "dance_demo_data.json";

            // 4. Lưu scene
            string dir = System.IO.Path.GetDirectoryName(MVP_SCENE_PATH);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(newScene, MVP_SCENE_PATH);
            AssetDatabase.Refresh();
            Debug.Log("[MVPSceneSetup] Đã tạo thành công cảnh MVP tại: " + MVP_SCENE_PATH);
        }

        [MenuItem("DanceUp/Cảnh/Mở Cảnh Chính (SampleScene)", false, 21)]
        public static void OpenMainScene()
        {
            if (System.IO.File.Exists(MAIN_SCENE_PATH))
            {
                EditorSceneManager.OpenScene(MAIN_SCENE_PATH);
                Debug.Log("[MVPSceneSetup] Đã mở cảnh chính: " + MAIN_SCENE_PATH);
            }
            else
            {
                Debug.LogError("[MVPSceneSetup] Không tìm thấy cảnh chính: " + MAIN_SCENE_PATH);
            }
        }
    }
}
#endif
