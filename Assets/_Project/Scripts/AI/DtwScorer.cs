using System.Collections.Generic;
using UnityEngine;
using DanceUp.Data;

namespace DanceUp.AI
{
    /// <summary>
    /// DTW (Dynamic Time Warping) offline scorer.
    ///
    /// Cách hoạt động (giải thích đơn giản):
    ///   Thay vì so khớp frame 1-1 theo thứ tự thời gian, DTW tìm ra cách
    ///   "co giãn" trục thời gian sao cho hai chuỗi động tác khớp nhau tốt nhất.
    ///   Kết quả là một điểm chính xác "công bằng hơn" cho người nhảy đúng
    ///   động tác nhưng bị lệch nhịp so với giáo viên mẫu.
    ///
    /// Mức độ: Offline (tính sau khi kết thúc bài, không realtime).
    /// Input : Hai chuỗi vector pose (student vs teacher) dạng List of float[].
    /// Output: dtwAccuracy ∈ [0, 100] (%).
    /// </summary>
    public static class DtwScorer
    {
        // Các khớp được dùng để tính vector pose cho DTW
        private static readonly string[] DTW_JOINTS = DanceMLPipeline.JOINTS;

        // Giới hạn Sakoe-Chiba band: cho phép lệch tối đa bao nhiêu % độ dài chuỗi.
        // Ví dụ 0.15 = học viên có thể lệch tối đa 15% thời lượng bài so với mẫu.
        // Tăng lên nếu muốn tha thứ nhiều hơn, giảm xuống nếu muốn chặt chẽ hơn.
        private const float SAKOE_CHIBA_RATIO = 0.15f;

        /// <summary>
        /// Tính điểm DTW từ danh sách FrameEvaluationResult đã ghi trong session.
        /// Trích xuất chuỗi pose của student và teacher rồi chạy DTW.
        /// </summary>
        /// <returns>dtwAccuracy ∈ [0, 100] (%)</returns>
        public static float ComputeFromFrameResults(List<FrameEvaluationResult> frames)
        {
            if (frames == null || frames.Count < 2)
                return 0f;

            // Trích xuất hai chuỗi vector pose từ dữ liệu frame đã ghi
            var studentSeq = new List<float[]>();
            var teacherSeq = new List<float[]>();

            foreach (var fr in frames)
            {
                float[] sv = ExtractPoseVector(fr, useStudent: true);
                float[] tv = ExtractPoseVector(fr, useStudent: false);

                // Chỉ thêm frame khi cả student lẫn teacher đều có đủ dữ liệu khớp
                if (sv != null && tv != null)
                {
                    studentSeq.Add(sv);
                    teacherSeq.Add(tv);
                }
            }

            if (studentSeq.Count < 2 || teacherSeq.Count < 2)
                return 0f;

            // Chạy DTW và chuyển cost → accuracy %
            float dtwCost = ComputeDtw(studentSeq, teacherSeq);
            float accuracy = CostToAccuracy(dtwCost, studentSeq.Count);

            Debug.Log($"[DtwScorer] Frames used: {studentSeq.Count}, DTW cost: {dtwCost:F3}, DTW accuracy: {accuracy:F1}%");
            return accuracy;
        }

        // ─────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Trích xuất vector pose phẳng từ 1 frame: [x0, y0, x1, y1, ..., x12, y12]
        /// Dùng vị trí đã chuẩn hóa (studentPos / teacherPos từ JointScoreItem).
        /// </summary>
        private static float[] ExtractPoseVector(FrameEvaluationResult fr, bool useStudent)
        {
            if (fr.joints == null || fr.joints.Count == 0) return null;

            int dim = DTW_JOINTS.Length * 2;   // 13 khớp × (x, y) = 26 chiều
            float[] vec = new float[dim];
            int validCount = 0;

            for (int i = 0; i < DTW_JOINTS.Length; i++)
            {
                string jName = DTW_JOINTS[i];
                if (!fr.joints.TryGetValue(jName, out JointScoreItem item)) continue;

                Vector2 pos = useStudent ? item.studentPos : item.teacherPos;
                vec[i * 2]     = pos.x;
                vec[i * 2 + 1] = pos.y;
                validCount++;
            }

            // Nếu ít hơn một nửa số khớp có dữ liệu → bỏ frame này
            return validCount >= DTW_JOINTS.Length / 2 ? vec : null;
        }

        /// <summary>
        /// Thuật toán DTW chuẩn với Sakoe-Chiba band để giới hạn vùng tìm kiếm.
        ///
        /// Ý tưởng đơn giản:
        ///   Tạo bảng cost[i,j] = chi phí khi ghép frame i của student với frame j của teacher.
        ///   Dùng quy hoạch động để tìm đường đi từ góc trên-trái đến góc dưới-phải
        ///   có tổng chi phí nhỏ nhất → đó là căn chỉnh tốt nhất.
        /// </summary>
        private static float ComputeDtw(List<float[]> seq1, List<float[]> seq2)
        {
            int n = seq1.Count;
            int m = seq2.Count;
            int band = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(n, m) * SAKOE_CHIBA_RATIO));

            // dtw[i,j] = tổng chi phí tối ưu để căn chỉnh seq1[0..i] với seq2[0..j]
            float[,] dtw = new float[n, m];

            // Khởi tạo tất cả bằng vô cực (chưa thăm)
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                    dtw[i, j] = float.MaxValue;

            dtw[0, 0] = EuclideanDistance(seq1[0], seq2[0]);

            // Điền cột đầu
            for (int i = 1; i < n && i <= band; i++)
                dtw[i, 0] = dtw[i - 1, 0] + EuclideanDistance(seq1[i], seq2[0]);

            // Điền hàng đầu
            for (int j = 1; j < m && j <= band; j++)
                dtw[0, j] = dtw[0, j - 1] + EuclideanDistance(seq1[0], seq2[j]);

            // Điền bảng DP trong phạm vi Sakoe-Chiba band
            for (int i = 1; i < n; i++)
            {
                int jStart = Mathf.Max(1, i - band);
                int jEnd   = Mathf.Min(m - 1, i + band);

                for (int j = jStart; j <= jEnd; j++)
                {
                    float cost = EuclideanDistance(seq1[i], seq2[j]);

                    // Lấy min của 3 ô đến được: trên, trái, chéo trên-trái
                    float prev = Mathf.Min(dtw[i - 1, j],           // lên 1 bước
                                 Mathf.Min(dtw[i, j - 1],           // sang trái 1 bước
                                           dtw[i - 1, j - 1]));     // chéo

                    dtw[i, j] = cost + (prev == float.MaxValue ? cost : prev);
                }
            }

            // DTW cost = giá trị ô cuối cùng, chuẩn hóa theo độ dài đường đi
            float rawCost = dtw[n - 1, m - 1];
            if (rawCost == float.MaxValue) rawCost = 999f;

            // Chia cho (n+m) để chuẩn hóa, tránh phụ thuộc vào độ dài chuỗi
            return rawCost / (n + m);
        }

        /// <summary>
        /// Khoảng cách Euclidean giữa hai vector pose phẳng (26 chiều).
        /// </summary>
        private static float EuclideanDistance(float[] a, float[] b)
        {
            float sum = 0f;
            int len = Mathf.Min(a.Length, b.Length);
            for (int k = 0; k < len; k++)
            {
                float d = a[k] - b[k];
                sum += d * d;
            }
            return Mathf.Sqrt(sum);
        }

        /// <summary>
        /// Chuyển DTW cost (thấp = tốt) sang accuracy % (cao = tốt).
        ///
        /// Dùng hàm mũ: accuracy = exp(-k * cost) × 100
        /// k = 3.0 là hệ số độ nhạy – tăng k lên thì khắt khe hơn, giảm k thì khoan dung hơn.
        /// Tại cost=0 → accuracy=100%. Tại cost=1 → accuracy≈5%.
        /// </summary>
        private static float CostToAccuracy(float normalizedCost, int frameCount)
        {
            const float k = 3.0f;
            float accuracy = Mathf.Exp(-k * normalizedCost) * 100f;
            return Mathf.Clamp(accuracy, 0f, 100f);
        }
    }
}
