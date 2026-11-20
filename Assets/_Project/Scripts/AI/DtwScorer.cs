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
            var result = ComputeWithDeviations(frames);
            return result.accuracy;
        }

        /// <summary>
        /// Tính điểm DTW, trích xuất Warping Path và phân tích chi tiết các đoạn sai lệch nhịp/tư thế.
        /// </summary>
        public static (float accuracy, List<DtwDeviationSegment> deviations, float avgTimeDrift) ComputeWithDeviations(List<FrameEvaluationResult> frames, float fps = 30f)
        {
            var emptyDeviations = new List<DtwDeviationSegment>();
            if (frames == null || frames.Count < 2)
                return (0f, emptyDeviations, 0f);

            var studentSeq = new List<float[]>();
            var teacherSeq = new List<float[]>();
            var validFrames = new List<FrameEvaluationResult>();

            foreach (var fr in frames)
            {
                float[] sv = ExtractPoseVector(fr, useStudent: true);
                float[] tv = ExtractPoseVector(fr, useStudent: false);

                if (sv != null && tv != null)
                {
                    studentSeq.Add(sv);
                    teacherSeq.Add(tv);
                    validFrames.Add(fr);
                }
            }

            if (studentSeq.Count < 2 || teacherSeq.Count < 2)
                return (0f, emptyDeviations, 0f);

            // Chạy DTW và trích xuất Warping Path
            var (rawCost, path) = ComputeDtwWithPath(studentSeq, teacherSeq);
            float accuracy = CostToAccuracy(rawCost, studentSeq.Count);

            // Phân tích sai lệch từ Warping Path
            var (deviations, avgDrift) = AnalyzeDeviationsFromWarpingPath(path, validFrames, studentSeq, teacherSeq, fps);

            Debug.Log($"[DtwScorer] DTW Done! Frames: {studentSeq.Count}, Accuracy: {accuracy:F1}%, Avg Drift: {avgDrift:F2}s, Deviations found: {deviations.Count}");
            return (accuracy, deviations, avgDrift);
        }

        /// <summary>
        /// Thuật toán DTW chuẩn với Sakoe-Chiba band kết hợp lưu ma trận để truy vết Warping Path.
        /// </summary>
        private static (float normalizedCost, List<(int studentIdx, int teacherIdx)> warpingPath) ComputeDtwWithPath(List<float[]> seq1, List<float[]> seq2)
        {
            int n = seq1.Count;
            int m = seq2.Count;
            int band = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(n, m) * SAKOE_CHIBA_RATIO));

            float[,] dtw = new float[n, m];

            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                    dtw[i, j] = float.MaxValue;

            dtw[0, 0] = EuclideanDistance(seq1[0], seq2[0]);

            for (int i = 1; i < n && i <= band; i++)
                dtw[i, 0] = dtw[i - 1, 0] + EuclideanDistance(seq1[i], seq2[0]);

            for (int j = 1; j < m && j <= band; j++)
                dtw[0, j] = dtw[0, j - 1] + EuclideanDistance(seq1[0], seq2[j]);

            for (int i = 1; i < n; i++)
            {
                int jStart = Mathf.Max(1, i - band);
                int jEnd   = Mathf.Min(m - 1, i + band);

                for (int j = jStart; j <= jEnd; j++)
                {
                    float cost = EuclideanDistance(seq1[i], seq2[j]);
                    float prev = Mathf.Min(dtw[i - 1, j],
                                 Mathf.Min(dtw[i, j - 1],
                                           dtw[i - 1, j - 1]));

                    dtw[i, j] = cost + (prev == float.MaxValue ? cost : prev);
                }
            }

            float finalCost = dtw[n - 1, m - 1];
            if (finalCost == float.MaxValue) finalCost = 999f;
            float normalizedCost = finalCost / (n + m);

            // Backtracking để tìm Warping Path từ (n-1, m-1) về (0, 0)
            var path = new List<(int, int)>();
            int curI = n - 1;
            int curJ = m - 1;
            path.Add((curI, curJ));

            while (curI > 0 || curJ > 0)
            {
                if (curI == 0)
                {
                    curJ--;
                }
                else if (curJ == 0)
                {
                    curI--;
                }
                else
                {
                    float diag = dtw[curI - 1, curJ - 1];
                    float up   = dtw[curI - 1, curJ];
                    float left = dtw[curI, curJ - 1];

                    if (diag <= up && diag <= left)
                    {
                        curI--;
                        curJ--;
                    }
                    else if (up <= left)
                    {
                        curI--;
                    }
                    else
                    {
                        curJ--;
                    }
                }
                path.Add((curI, curJ));
            }

            path.Reverse();
            return (normalizedCost, path);
        }

        /// <summary>
        /// Phân tích Warping Path để phân loại các phân đoạn lệch nhịp (Timing Drift) và lệch tư thế (Posture Mismatch).
        /// </summary>
        private static (List<DtwDeviationSegment> segments, float avgDrift) AnalyzeDeviationsFromWarpingPath(
            List<(int sIdx, int tIdx)> path,
            List<FrameEvaluationResult> frames,
            List<float[]> seq1,
            List<float[]> seq2,
            float fps)
        {
            var segments = new List<DtwDeviationSegment>();
            if (path == null || path.Count == 0) return (segments, 0f);

            float totalDrift = 0f;
            int count = 0;

            // Đánh giá từng điểm trong warping path
            var pointStates = new List<(float time, float offset, float dist, int severity, string type, string topJoint)>();

            foreach (var (sIdx, tIdx) in path)
            {
                if (sIdx >= frames.Count) continue;
                float timeS = frames[sIdx].timestamp;
                float timeT = (tIdx < frames.Count) ? frames[tIdx].timestamp : (tIdx / fps);

                float timeOffset = timeS - timeT; // > 0: trễ nhịp (học viên đi sau mẫu), < 0: sớm nhịp
                totalDrift += Mathf.Abs(timeOffset);
                count++;

                float dist = (sIdx < seq1.Count && tIdx < seq2.Count) ? EuclideanDistance(seq1[sIdx], seq2[tIdx]) : 0f;

                int severity = 0; // 0 = Good, 1 = Minor, 2 = Severe
                string type = "Khớp nhịp";

                if (dist > 1.0f)
                {
                    severity = 2;
                    type = "Sai động tác";
                }
                else if (timeOffset > 0.35f)
                {
                    severity = 2;
                    type = "Trễ nhịp";
                }
                else if (timeOffset < -0.35f)
                {
                    severity = 2;
                    type = "Vội nhịp";
                }
                else if (timeOffset > 0.18f)
                {
                    severity = 1;
                    type = "Chậm nhịp nhẹ";
                }
                else if (timeOffset < -0.18f)
                {
                    severity = 1;
                    type = "Sớm nhịp nhẹ";
                }
                else if (dist > 0.65f)
                {
                    severity = 1;
                    type = "Lệch tư thế";
                }

                // Tìm khớp sai nhiều nhất trong frame
                string maxErrorJoint = "";
                float maxJointDiff = 0f;
                if (frames[sIdx].joints != null)
                {
                    foreach (var kvp in frames[sIdx].joints)
                    {
                        float d = Vector2.Distance(kvp.Value.studentPos, kvp.Value.teacherPos);
                        if (d > maxJointDiff)
                        {
                            maxJointDiff = d;
                            maxErrorJoint = kvp.Key;
                        }
                    }
                }

                pointStates.Add((timeS, timeOffset, dist, severity, type, maxErrorJoint));
            }

            float avgDrift = count > 0 ? (totalDrift / count) : 0f;

            // Nhóm các điểm liên tiếp có cùng trạng thái thành Segment
            if (pointStates.Count == 0) return (segments, avgDrift);

            int segStart = 0;
            for (int i = 1; i <= pointStates.Count; i++)
            {
                bool isEnd = (i == pointStates.Count);
                bool stateChanged = !isEnd && (pointStates[i].severity != pointStates[segStart].severity || pointStates[i].type != pointStates[segStart].type);

                if (isEnd || stateChanged)
                {
                    int segEndIdx = i - 1;
                    float startT = pointStates[segStart].time;
                    float endT = pointStates[segEndIdx].time;

                    // Chỉ gom nếu đoạn kéo dài ít nhất 0.25s hoặc là đoạn kết thúc
                    if (endT - startT >= 0.25f || isEnd)
                    {
                        float sumOffset = 0f;
                        float sumDist = 0f;
                        var jointCounts = new Dictionary<string, int>();

                        for (int k = segStart; k <= segEndIdx; k++)
                        {
                            sumOffset += pointStates[k].offset;
                            sumDist += pointStates[k].dist;
                            string jn = pointStates[k].topJoint;
                            if (!string.IsNullOrEmpty(jn))
                            {
                                if (!jointCounts.ContainsKey(jn)) jointCounts[jn] = 0;
                                jointCounts[jn]++;
                            }
                        }

                        int len = (segEndIdx - segStart + 1);
                        float avgOffset = sumOffset / len;
                        float avgDist = sumDist / len;

                        string prominentJoint = "";
                        int maxJc = 0;
                        foreach (var kv in jointCounts)
                        {
                            if (kv.Value > maxJc)
                            {
                                maxJc = kv.Value;
                                prominentJoint = kv.Key;
                            }
                        }

                        string desc = "";
                        string typeStr = pointStates[segStart].type;
                        int sev = pointStates[segStart].severity;

                        if (sev == 0)
                        {
                            desc = "Đúng nhịp và đúng tư thế chuẩn xác.";
                        }
                        else if (typeStr.Contains("nhịp"))
                        {
                            desc = $"{typeStr} khoảng {Mathf.Abs(avgOffset):F2}s so với giáo viên.";
                        }
                        else
                        {
                            string jointVn = TranslateJoint(prominentJoint);
                            desc = $"Lệch góc tư thế ở {jointVn}, biên độ chưa đạt.";
                        }

                        segments.Add(new DtwDeviationSegment
                        {
                            startTime = startT,
                            endTime = endT,
                            deviationType = typeStr,
                            timeOffsetSeconds = avgOffset,
                            spatialDistance = avgDist,
                            severity = sev,
                            primaryJoint = prominentJoint,
                            description = desc
                        });
                    }

                    segStart = i;
                }
            }

            return (segments, avgDrift);
        }

        private static string TranslateJoint(string j)
        {
            switch (j)
            {
                case "left_wrist": return "Cổ tay trái";
                case "right_wrist": return "Cổ tay phải";
                case "left_elbow": return "Khuỷu tay trái";
                case "right_elbow": return "Khuỷu tay phải";
                case "left_shoulder": return "Vai trái";
                case "right_shoulder": return "Vai phải";
                case "left_knee": return "Đầu gối trái";
                case "right_knee": return "Đầu gối phải";
                case "left_ankle": return "Cổ chân trái";
                case "right_ankle": return "Cổ chân phải";
                case "left_hip": return "Hông trái";
                case "right_hip": return "Hông phải";
                default: return "Khớp cơ thể";
            }
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
