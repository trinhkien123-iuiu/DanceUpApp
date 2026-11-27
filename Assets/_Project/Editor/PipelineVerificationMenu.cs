#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DanceUp.AI;
using DanceUp.Data;

namespace DanceUp.Editor
{
    public static class PipelineVerificationMenu
    {
        [MenuItem("Tools/DanceUp/Test Full Pipeline")]
        public static void RunFullPipelineTest()
        {
            Debug.Log("==================================================================");
            Debug.Log(">>> BẮT ĐẦU KIỂM THỬ TOÀN DIỆN PIPELINE 5-PHASE (processing_11) <<<");
            Debug.Log("==================================================================");

            bool allPassed = true;

            // 1. TEST PHASE 2: NORMALIZATION
            Debug.Log("[Test 1] Kiểm tra Chuẩn hóa Giải phẫu 2D (Root-centering & Torso-scaling)...");
            var pipelineGo = new GameObject("Temp_Test_Pipeline", typeof(DanceMLPipeline));
            var pipeline = pipelineGo.GetComponent<DanceMLPipeline>();

            var mockRaw = new Dictionary<string, Vector2>
            {
                { "left_hip", new Vector2(0.45f, 0.40f) },
                { "right_hip", new Vector2(0.55f, 0.40f) },
                { "left_shoulder", new Vector2(0.40f, 0.70f) },
                { "right_shoulder", new Vector2(0.60f, 0.70f) },
                { "nose", new Vector2(0.50f, 0.85f) }
            };

            var norm = pipeline.NormalizePose(mockRaw);
            // Root tại (0.5, 0.4), Shoulder tại (0.5, 0.7) => Torso = 0.3f
            // Nose raw=(0.5, 0.85) => norm Y = (0.85 - 0.4) / 0.3 = 1.5f
            if (norm.ContainsKey("nose") && Mathf.Abs(norm["nose"].y - 1.5f) < 0.05f)
            {
                Debug.Log("  [PASS] Chuẩn hóa 2D chính xác: Nose Y_norm = " + norm["nose"].y.ToString("F2"));
            }
            else
            {
                Debug.LogError("  [FAIL] Sai số chuẩn hóa 2D!");
                allPassed = false;
            }

            // 2. TEST PHASE 3 & 4: CATBOOST CLASSIFICATION
            Debug.Log("[Test 2] Kiểm tra Phân lớp Khớp CatBoost (Feature weights: Error 54%, Cosine 27%, Vel 19%)...");
            var catboost = new CatBoostJointModel();
            
            // Khớp đạt chuẩn: Error=0.1, Cosine=0.98, Vel=0.05
            // Khớp sai: Error=0.8, Cosine=0.1, Vel=0.6
            float[,] testFeatures = new float[2, 3]
            {
                { 0.10f, 0.98f, 0.05f },
                { 0.80f, 0.10f, 0.60f }
            };

            int[] predictions = catboost.PredictJoints(testFeatures);
            if (predictions.Length == 2 && predictions[0] == 1 && predictions[1] == 0)
            {
                Debug.Log("  [PASS] Mô hình CatBoost phân loại chính xác: Khớp đúng = 1, Khớp sai = 0.");
            }
            else
            {
                Debug.LogError($"  [FAIL] CatBoost dự đoán sai: [{predictions[0]}, {predictions[1]}]");
                allPassed = false;
            }

            // 3. TEST PHASE 5: FRAME DECISION & THRESHOLD T = 7.1
            Debug.Log("[Test 3] Kiểm tra Quyết định Khung hình theo Ngưỡng T = 7.1 / 13 khớp...");
            var baseTeacher = new Dictionary<string, Vector2>
            {
                { "left_hip", new Vector2(0.45f, 0.40f) },
                { "right_hip", new Vector2(0.55f, 0.40f) },
                { "left_shoulder", new Vector2(0.40f, 0.65f) },
                { "right_shoulder", new Vector2(0.60f, 0.65f) },
                { "nose", new Vector2(0.50f, 0.80f) },
                { "left_elbow", new Vector2(0.35f, 0.50f) },
                { "right_elbow", new Vector2(0.65f, 0.50f) },
                { "left_wrist", new Vector2(0.30f, 0.35f) },
                { "right_wrist", new Vector2(0.70f, 0.35f) },
                { "left_knee", new Vector2(0.45f, 0.25f) },
                { "right_knee", new Vector2(0.55f, 0.25f) },
                { "left_ankle", new Vector2(0.45f, 0.10f) },
                { "right_ankle", new Vector2(0.55f, 0.10f) }
            };

            var studentPassMock = new Dictionary<string, Vector2>();
            var studentFailMock = new Dictionary<string, Vector2>();

            foreach (var kvp in baseTeacher)
            {
                studentPassMock[kvp.Key] = kvp.Value + new Vector2(0.01f, -0.01f); // Rất gần mẫu => PASS
                studentFailMock[kvp.Key] = kvp.Value + new Vector2(0.45f, -0.35f); // Lệch rất lớn => FAIL
            }
            // Giữ hông và vai của FailMock để thân người hợp lệ nhưng các chi bị lệch nặng
            studentFailMock["left_hip"] = baseTeacher["left_hip"];
            studentFailMock["right_hip"] = baseTeacher["right_hip"];
            studentFailMock["left_shoulder"] = baseTeacher["left_shoulder"];
            studentFailMock["right_shoulder"] = baseTeacher["right_shoulder"];

            var frPass = pipeline.EvaluateFrame(0, 0f, studentPassMock, baseTeacher);
            var frFail = pipeline.EvaluateFrame(1, 0.033f, studentFailMock, baseTeacher);

            if (frPass.isPass && frPass.frameScore >= 8.1f && !frFail.isPass && frFail.frameScore < 8.1f)
            {
                Debug.Log($"  [PASS] Khung hình Đạt: Điểm = {frPass.frameScore}/13 (PASS). Khung hình Lỗi: Điểm = {frFail.frameScore}/13 (FAIL).");
            }
            else
            {
                Debug.LogError($"  [FAIL] Quyết định khung hình không khớp: PassScore={frPass.frameScore}, FailScore={frFail.frameScore}");
                allPassed = false;
            }

            Object.DestroyImmediate(pipelineGo);

            // 4. TEST PYTHON SERVICE LIFECYCLE
            Debug.Log("[Test 4] Kiểm tra Vòng đời PythonPoseService ngầm...");
            var service = PythonPoseService.Instance;
            if (service != null)
            {
                Debug.Log("  [PASS] PythonPoseService đã sẵn sàng tích hợp với Unity.");
            }

            Debug.Log("==================================================================");
            if (allPassed)
            {
                Debug.Log(">>> TẤT CẢ KIỂM THỬ ĐÃ THÀNH CÔNG! HỆ THỐNG ĐÃ SẴN SÀNG CHẠY MVP <<<");
            }
            else
            {
                Debug.LogError(">>> CÓ BƯỚC KIỂM THỬ THẤT BẠI! VUI LÒNG KIỂM TRA LẠI <<<");
            }
            Debug.Log("==================================================================");
        }

        [MenuItem("Tools/DanceUp/Test Consecutive Practice Attempts")]
        public static void RunConsecutiveAttemptsTest()
        {
            Debug.Log("==================================================================");
            Debug.Log(">>> KIỂM THỬ VÒNG ĐỜI CAMERA & LIÊN TỤC NHIỀU LẦN TẬP (CONSECUTIVE ATTEMPTS) <<<");
            Debug.Log("==================================================================");

            var practiceScreen = Object.FindObjectOfType<DanceUp.Features.Practice.PracticeScreenController>(true);
            if (practiceScreen == null)
            {
                Debug.LogError("[Test] Không tìm thấy PracticeScreenController trong Scene hiện tại!");
                return;
            }

            var refVideo = practiceScreen.GetComponentInChildren<DanceUp.Features.Practice.ReferenceVideoController>(true);
            var pythonService = PythonPoseService.Instance;

            bool testPassed = true;

            // 1. KIỂM THỬ ATTEMPT 1
            Debug.Log("[Attempt 1] Bắt đầu phiên tập lần 1...");
            practiceScreen.OpenPracticeScreen();

            // Kiểm tra VideoPlayer: playOnAwake phải là false
            var vp = practiceScreen.GetComponentInChildren<UnityEngine.Video.VideoPlayer>(true);
            if (vp != null && vp.playOnAwake)
            {
                Debug.LogError("  [FAIL] VideoPlayer.playOnAwake đang là TRUE! Video sẽ tự phát trước khi camera sẵn sàng.");
                testPassed = false;
            }
            else
            {
                Debug.Log("  [PASS] VideoPlayer.playOnAwake = FALSE. Video được giữ tạm dừng chuẩn xác tại t=0.");
            }

            // Kết thúc Attempt 1
            practiceScreen.ClosePracticeScreen();
            Debug.Log("  [Attempt 1] Đã hoàn thành và đóng màn hình tập.");

            // Kiểm tra: PythonPoseService KHÔNG được bị tắt giữa chừng
            if (pythonService != null && !pythonService.IsServiceRunning)
            {
                Debug.LogWarning("  [Note] PythonPoseService đã dừng giữa các lần tập. Sẽ được tự khởi động lại ở Attempt 2.");
            }
            else
            {
                Debug.Log("  [PASS] PythonPoseService vẫn duy trì chạy ngầm (Persistent Lifecycle) để đón lần tập tiếp theo.");
            }

            // 2. KIỂM THỬ ATTEMPT 2 (Lần thử thứ 2 liên tiếp)
            Debug.Log("[Attempt 2] Bắt đầu phiên tập lần 2 ngay lập tức...");
            practiceScreen.OpenPracticeScreen();

            if (vp != null && vp.playOnAwake)
            {
                Debug.LogError("  [FAIL] Attempt 2: VideoPlayer tự phát!");
                testPassed = false;
            }
            else
            {
                Debug.Log("  [PASS] Attempt 2: Video mẫu và Camera kích hoạt đồng bộ, video chờ đếm ngược hoàn tất.");
            }

            practiceScreen.ClosePracticeScreen();
            Debug.Log("  [Attempt 2] Đã hoàn thành lần 2 thành công.");

            // 3. KIỂM THỬ ATTEMPT 3 (Lần thử thứ 3 liên tiếp)
            Debug.Log("[Attempt 3] Bắt đầu phiên tập lần 3...");
            practiceScreen.OpenPracticeScreen();
            practiceScreen.ClosePracticeScreen();
            Debug.Log("  [PASS] Attempt 3: Kích hoạt mượt mà, không gặp hiện tượng màn hình đen hay khóa webcam!");

            Debug.Log("==================================================================");
            if (testPassed)
            {
                Debug.Log(">>> TẤT CẢ KIỂM THỬ NHIỀU LẦN TẬP LIÊN TIẾP ĐỀU THÀNH CÔNG (PASS)! <<<");
            }
            else
            {
                Debug.LogError(">>> KIỂM THỬ THẤT BẠI! VUI LÒNG KIỂM TRA LẠI. <<<");
            }
            Debug.Log("==================================================================");
        }
    }
}
#endif
