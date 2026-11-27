using System;
using System.Collections.Generic;
using UnityEngine;
using DanceUp.Data;

namespace DanceUp.AI
{
    /// <summary>
    /// Pipeline tiền xử lý, trích xuất đặc trưng và đánh giá theo chuẩn processing_11.
    /// Hoàn toàn tách biệt khỏi tầng UI và tầng suy luận mô hình.
    /// </summary>
    public class DanceMLPipeline : MonoBehaviour
    {
        [Header("=== CẤU HÌNH MÔ HÌNH ML ===")]
        [SerializeField] private TextAsset onnxModelAsset;
        [SerializeField] private float frameScoreThreshold = 8.1f; // Ngưỡng đạt từ processing_11_ver1 (T* = 8.1) từ processing_11
        [SerializeField] private float visibilityThreshold = 0.5f;

        public static readonly string[] JOINTS = new string[]
        {
            "nose",
            "left_shoulder", "right_shoulder",
            "left_elbow", "right_elbow",
            "left_wrist", "right_wrist",
            "left_hip", "right_hip",
            "left_knee", "right_knee",
            "left_ankle", "right_ankle"
        };

        private IJointClassifierModel model;
        private readonly Dictionary<string, Vector2> prevStudentNorm = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Vector2> prevTeacherNorm = new Dictionary<string, Vector2>();

        private int cumulativePassFrames = 0;
        private int totalEvaluatedFrames = 0;
        private readonly List<Dictionary<string, Vector2>> recentPosesWindow = new List<Dictionary<string, Vector2>>();
        private const int MOMENTUM_WINDOW_SIZE = 15;
        private const float MOMENTUM_VARIANCE_THRESHOLD = 0.020f; // Ngưỡng chặn video tĩnh từ processing_11_ver1từ processing_11

        private void Awake()
        {
            InitModel();
        }

        public void InitModel(IJointClassifierModel customModel = null)
        {
            if (customModel != null)
            {
                model = customModel;
            }
            else
            {
                var onnx = new OnnxJointModel();
                if (onnxModelAsset != null && onnxModelAsset.bytes != null)
                {
                    onnx.LoadModel(onnxModelAsset.bytes);
                }
                model = onnx;
            }
            Debug.Log("[DanceMLPipeline] Khởi tạo mô hình: " + model.ModelName + " (" + model.Version + ")");
        }

        public void ResetSession()
        {
            cumulativePassFrames = 0;
            totalEvaluatedFrames = 0;
            prevStudentNorm.Clear();
            prevTeacherNorm.Clear();
            recentPosesWindow.Clear();
        }

        /// <summary>
        /// Tính độ lệch chuẩn chuyển động tổng thể trên toàn bộ chuỗi khung hình để phát hiện video đứng yên / ảnh tĩnh.
        /// </summary>
        public float ComputeSequenceMotionStd(List<Dictionary<string, Vector2>> sequence)
        {
            if (sequence == null || sequence.Count < 5) return 0f;
            return ComputeAverageKeyJointStd(sequence);
        }

        /// <summary>
        /// Phase 1: Lọc nhiễu động lượng (Noise Isolation / Momentum Filter).
        /// Kiểm tra độ lệch chuẩn chuyển động của các khớp then chốt (cổ tay, cổ chân) >= 0.020.
        /// </summary>
        public bool CheckIsDancing(Dictionary<string, Vector2> currentRaw)
        {
            if (currentRaw == null || currentRaw.Count == 0) return false;
            recentPosesWindow.Add(new Dictionary<string, Vector2>(currentRaw));
            if (recentPosesWindow.Count > MOMENTUM_WINDOW_SIZE) recentPosesWindow.RemoveAt(0);
            if (recentPosesWindow.Count < 5) return true; // Đang tích lũy frame ban đầu

            float avgStd = ComputeAverageKeyJointStd(recentPosesWindow);
            return avgStd >= MOMENTUM_VARIANCE_THRESHOLD;
        }

        private static float ComputeAverageKeyJointStd(List<Dictionary<string, Vector2>> sequence)
        {
            string[] keyJoints = { "left_wrist", "right_wrist", "left_ankle", "right_ankle" };
            float totalStd = 0f;
            int count = 0;

            foreach (var j in keyJoints)
            {
                float sumX = 0f, sumY = 0f;
                int n = 0;
                foreach (var pDict in sequence)
                {
                    if (pDict != null && pDict.TryGetValue(j, out var pt))
                    {
                        sumX += pt.x;
                        sumY += pt.y;
                        n++;
                    }
                }
                if (n > 1)
                {
                    float meanX = sumX / n;
                    float meanY = sumY / n;
                    float varX = 0f, varY = 0f;
                    foreach (var pDict in sequence)
                    {
                        if (pDict != null && pDict.TryGetValue(j, out var pt))
                        {
                            varX += (pt.x - meanX) * (pt.x - meanX);
                            varY += (pt.y - meanY) * (pt.y - meanY);
                        }
                    }
                    float std = (Mathf.Sqrt(varX / (n - 1)) + Mathf.Sqrt(varY / (n - 1))) / 2f;
                    totalStd += std;
                    count++;
                }
            }

            return count > 0 ? (totalStd / count) : 0f;
        }

        /// <summary>
        /// Chuẩn hóa tọa độ dựa trên độ dài thân người 2D (Torso Scaling - processing_11_ver1).
        /// </summary>
        public Dictionary<string, Vector2> NormalizePose(Dictionary<string, Vector2> rawJoints)
        {
            var normalized = new Dictionary<string, Vector2>();
            if (rawJoints == null) return normalized;

            // Kiểm tra xem có đủ 4 khớp cốt lõi không để tránh lệch tâm do Vector2.zero
            if (!rawJoints.TryGetValue("left_hip", out Vector2 lHip) ||
                !rawJoints.TryGetValue("right_hip", out Vector2 rHip) ||
                !rawJoints.TryGetValue("left_shoulder", out Vector2 lSh) ||
                !rawJoints.TryGetValue("right_shoulder", out Vector2 rSh))
            {
                return normalized;
            }

            Vector2 root = (lHip + rHip) * 0.5f;
            Vector2 shoulderMid = (lSh + rSh) * 0.5f;

            // Độ dài thân người 2D (Euclidean distance từ trung điểm hông đến trung điểm vai)
            float torsoLength = Vector2.Distance(shoulderMid, root);
            if (torsoLength < 1e-5f) torsoLength = 1.0f;

            foreach (var kvp in rawJoints)
            {
                normalized[kvp.Key] = (kvp.Value - root) / torsoLength;
            }

            return normalized;
        }


        /// <summary>
        /// Đánh giá một khung hình thời gian thực theo đúng quy trình processing_11:
        /// 1. Chuẩn hóa hình học 2D
        /// 2. Trích xuất 3 đặc trưng [Joint_Error, Joint_Cosine, Velocity_Error]
        /// 3. Phân lớp qua Model ML (Pass=1, Fail=0)
        /// 4. Tổng hợp điểm khung hình (Score >= 7.1 => PASS)
        /// </summary>
        public FrameEvaluationResult EvaluateFrame(
            int frameIndex,
            float timestamp,
            Dictionary<string, Vector2> studentRaw,
            Dictionary<string, Vector2> teacherRaw)
        {
            if (model == null) InitModel();

            var studentNorm = NormalizePose(studentRaw);
            var teacherNorm = NormalizePose(teacherRaw);

            int nJoints = JOINTS.Length;
            float[,] features = new float[nJoints, 3];

            for (int i = 0; i < nJoints; i++)
            {
                string jName = JOINTS[i];
                Vector2 sNorm = studentNorm.ContainsKey(jName) ? studentNorm[jName] : Vector2.zero;
                Vector2 tNorm = teacherNorm.ContainsKey(jName) ? teacherNorm[jName] : Vector2.zero;

                // 1. Joint_Error: Khoảng cách Euclidean
                float jError = Vector2.Distance(sNorm, tNorm);

                // 2. Joint_Cosine: Độ tương đồng góc
                float dot = (sNorm.x * tNorm.x) + (sNorm.y * tNorm.y);
                float normS = sNorm.magnitude;
                float normT = tNorm.magnitude;
                float jCosine = dot / ((normS * normT) + 1e-9f);

                // 3. Velocity_Error: Sai lệch vận tốc 2 khung liên tiếp
                Vector2 prevS = prevStudentNorm.ContainsKey(jName) ? prevStudentNorm[jName] : sNorm;
                Vector2 prevT = prevTeacherNorm.ContainsKey(jName) ? prevTeacherNorm[jName] : tNorm;

                Vector2 velS = sNorm - prevS;
                Vector2 velT = tNorm - prevT;
                float velError = Vector2.Distance(velS, velT);

                features[i, 0] = jError;
                features[i, 1] = jCosine;
                features[i, 2] = velError;

                prevStudentNorm[jName] = sNorm;
                prevTeacherNorm[jName] = tNorm;
            }

            // Gọi suy luận ML
            int[] predictions = model.PredictJoints(features);

            // Tổng hợp kết quả khung hình
            var fr = new FrameEvaluationResult
            {
                frameIndex = frameIndex,
                timestamp = timestamp,
                maxScore = 13.0f
            };

            float totalScore = 0f;
            for (int i = 0; i < nJoints; i++)
            {
                string jName = JOINTS[i];
                int isPass = (i < predictions.Length) ? predictions[i] : 1;
                totalScore += isPass;

                fr.joints[jName] = new JointScoreItem
                {
                    jointName = jName,
                    studentPos = studentRaw.ContainsKey(jName) ? studentRaw[jName] : Vector2.zero,
                    teacherPos = teacherRaw.ContainsKey(jName) ? teacherRaw[jName] : Vector2.zero,
                    isPass = isPass,
                    jointError = features[i, 0],
                    jointCosine = features[i, 1],
                    velocityError = features[i, 2]
                };
            }

            fr.frameScore = totalScore;
            fr.isPass = (totalScore >= frameScoreThreshold);

            totalEvaluatedFrames++;
            if (fr.isPass) cumulativePassFrames++;

            fr.cumulativeAccuracy = (float)cumulativePassFrames / Mathf.Max(1, totalEvaluatedFrames) * 100.0f;

            return fr;
        }

        public string GenerateCoachAdvice(FrameEvaluationResult fr)
        {
            if (fr == null) return "Giữ nhịp và bắt đầu chuyển động!";

            bool handError = (fr.joints.ContainsKey("left_wrist") && fr.joints["left_wrist"].isPass == 0)
                          || (fr.joints.ContainsKey("right_wrist") && fr.joints["right_wrist"].isPass == 0)
                          || (fr.joints.ContainsKey("left_elbow") && fr.joints["left_elbow"].isPass == 0)
                          || (fr.joints.ContainsKey("right_elbow") && fr.joints["right_elbow"].isPass == 0);

            bool legError = (fr.joints.ContainsKey("left_knee") && fr.joints["left_knee"].isPass == 0)
                         || (fr.joints.ContainsKey("right_knee") && fr.joints["right_knee"].isPass == 0)
                         || (fr.joints.ContainsKey("left_ankle") && fr.joints["left_ankle"].isPass == 0)
                         || (fr.joints.ContainsKey("right_ankle") && fr.joints["right_ankle"].isPass == 0);

            if (handError) return "Nâng tay cao hơn một chút";
            if (legError) return "Hạ thấp trọng tâm và bước chắc chân";
            if (fr.isPass) return "Chuẩn rồi! Giữ đúng nhịp điệu nhé";
            return "Chú ý giữ thẳng lưng và mở rộng vai";
        }
    }
}
