using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Video;
using DanceUp.AI;
using DanceUp.Core;
using DanceUp.Data;
using Debug = UnityEngine.Debug;

namespace DanceUp.Features.ImportEvaluation
{
    /// <summary>
    /// Dịch vụ phân tích video vũ đạo ngoại tuyến (Offline Video Evaluation) và Xuất video gắn khung xương.
    /// Tích hợp MediaPipe Python Pose Extractor, DTW Scorer và DanceMLPipeline (CatBoost).
    /// </summary>
    public class VideoAnalysisService : MonoBehaviour
    {
        private static VideoAnalysisService instance;
        public static VideoAnalysisService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<VideoAnalysisService>(true);
                    if (instance == null)
                    {
                        var go = new GameObject("VideoAnalysisService");
                        instance = go.AddComponent<VideoAnalysisService>();
                        if (Application.isPlaying) DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private DanceMLPipeline mlPipeline;

        private void Awake()
        {
            if (instance == null) instance = this;
            else if (instance != this) { Destroy(gameObject); return; }

            mlPipeline = GetComponent<DanceMLPipeline>();
            if (mlPipeline == null) mlPipeline = gameObject.AddComponent<DanceMLPipeline>();
            mlPipeline.InitModel();
        }

        private string ResolvePythonExecutable()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidatePaths = new string[]
            {
                Path.Combine(localAppData, @"Programs\Python\Python313\python.exe"),
                Path.Combine(localAppData, @"Programs\Python\Python312\python.exe"),
                Path.Combine(localAppData, @"Programs\Python\Python311\python.exe"),
                Path.Combine(localAppData, @"Programs\Python\Python310\python.exe"),
                @"C:\Python313\python.exe",
                @"C:\Python312\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python310\python.exe"
            };

            foreach (var p in candidatePaths)
            {
                if (File.Exists(p)) return p;
            }

            return "python";
        }

        /// <summary>
        /// Bắt đầu phân tích toàn diện một video bài nhảy có sẵn.
        /// </summary>
        public void StartVideoAnalysis(
            string videoPath,
            VideoClip referenceClip,
            string lessonTitle,
            Action<float, string> onProgress,
            Action<SessionEvaluationData> onComplete,
            Action<string> onError)
        {
            StartCoroutine(AnalyzeVideoRoutine(videoPath, referenceClip, lessonTitle, onProgress, onComplete, onError));
        }

        private IEnumerator AnalyzeVideoRoutine(
            string videoPath,
            VideoClip referenceClip,
            string lessonTitle,
            Action<float, string> onProgress,
            Action<SessionEvaluationData> onComplete,
            Action<string> onError)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                onError?.Invoke("Tệp tin video không tồn tại hoặc đường dẫn không hợp lệ!");
                yield break;
            }

            onProgress?.Invoke(0.05f, "Đang khởi tạo bộ phân tích MediaPipe AI...");
            yield return null;

            string scriptPath = Path.Combine(Application.dataPath, "_Project/Scripts/AI/pose_extractor.py");
            string tempDir = Path.Combine(Application.temporaryCachePath, "DanceUpAnalysis");
            Directory.CreateDirectory(tempDir);
            string tempJson = Path.Combine(tempDir, "analyzed_pose_" + DateTime.Now.Ticks + ".json");

            bool pythonSuccess = false;
            float pythonProgress = 0f;
            string pythonError = "";
            bool isDone = false;
            string pyExe = ResolvePythonExecutable();

            // Chạy Python pose_extractor.py trong Background Thread
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = pyExe,
                        Arguments = $"-u \"{scriptPath}\" --mode analyze_video --video \"{videoPath}\" --output-json \"{tempJson}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (var process = new Process { StartInfo = psi })
                    {
                        process.OutputDataReceived += (s, e) =>
                        {
                            if (string.IsNullOrEmpty(e.Data)) return;
                            string line = e.Data.Trim();

                            if (line.StartsWith("PROGRESS:"))
                            {
                                string pStr = line.Substring("PROGRESS:".Length).Split(' ')[0];
                                if (float.TryParse(pStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float p))
                                {
                                    pythonProgress = p / 100f;
                                }
                            }
                            else if (line.StartsWith("ANALYSIS_COMPLETE:"))
                            {
                                pythonSuccess = true;
                            }
                        };

                        process.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data) && !e.Data.Contains("W0000") && !e.Data.Contains("oneDNN"))
                            {
                                pythonError += e.Data + "\n";
                            }
                        };

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit(90000); // 90 giây timeout

                        if (process.ExitCode == 0 && File.Exists(tempJson))
                        {
                            pythonSuccess = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    pythonError = ex.Message;
                    pythonSuccess = false;
                }
                finally
                {
                    isDone = true;
                }
            });

            // Vòng lặp chờ Python và cập nhật UI Progress
            while (!isDone)
            {
                float mappedProgress = Mathf.Lerp(0.05f, 0.65f, pythonProgress);
                string statusMsg = $"Trích xuất 13 khớp MediaPipe... ({Mathf.RoundToInt(pythonProgress * 100f)}%)";
                onProgress?.Invoke(mappedProgress, statusMsg);
                yield return new WaitForSeconds(0.1f);
            }

            if (!pythonSuccess || !File.Exists(tempJson))
            {
                Debug.LogError("[VideoAnalysisService] Phân tích MediaPipe thất bại: " + pythonError);
                onError?.Invoke("Không thể trích xuất khớp từ video: " + pythonError);
                yield break;
            }

            onProgress?.Invoke(0.70f, "Đang nạp dữ liệu động tác mẫu của giáo viên...");
            yield return new WaitForSeconds(0.15f);

            // 1. Nạp dữ liệu mẫu của giáo viên (Reference Teacher Poses)
            float teacherDuration;
            var teacherFrames = LoadTeacherReferenceData(referenceClip, out teacherDuration);
            if (teacherFrames == null || teacherFrames.Count == 0)
            {
                onError?.Invoke("Không tìm thấy dữ liệu động tác mẫu của giáo viên!");
                yield break;
            }

            // 2. Phân tích JSON khớp của học viên từ video thực tế
            onProgress?.Invoke(0.75f, "Đọc tọa độ khớp thực tế của bài nhảy...");
            yield return null;

            float sessionDuration, fps;
            var studentFrames = ParseStudentPoseJson(tempJson, out sessionDuration, out fps);
            if (studentFrames == null || studentFrames.Count == 0)
            {
                onError?.Invoke("Không phát hiện chuyển động cơ thể trong video đã chọn!");
                yield break;
            }

            // 3. Tiền xử lý (Nội suy các khớp có Visibility < 0.5) & Căn chỉnh DTW theo chuẩn processing_11_ver1
            if (mlPipeline == null)
            {
                mlPipeline = gameObject.AddComponent<DanceMLPipeline>();
                mlPipeline.InitModel();
            }
            mlPipeline.InitModel();
            mlPipeline.ResetSession();

            InterpolateLowVisibility(studentFrames, 0.5f);

            var studentNormSeq = new List<Dictionary<string, Vector2>>(studentFrames.Count);
            for (int i = 0; i < studentFrames.Count; i++)
                studentNormSeq.Add(mlPipeline.NormalizePose(studentFrames[i].joints));

            var teacherNormSeq = new List<Dictionary<string, Vector2>>(teacherFrames.Count);
            for (int j = 0; j < teacherFrames.Count; j++)
                teacherNormSeq.Add(mlPipeline.NormalizePose(teacherFrames[j]));

            onProgress?.Invoke(0.82f, "Đang căn chỉnh chuỗi thời gian FastDTW...");
            yield return null;

            int[] alignedTeacherIndices = DtwScorer.AlignNormalizedSequences(studentNormSeq, teacherNormSeq);

            onProgress?.Invoke(0.88f, "Chấm điểm từng khung hình qua mô hình ML CatBoost (T* = 8.1)...");
            yield return new WaitForSeconds(0.1f);

            var frameResults = new List<FrameEvaluationResult>();
            int passCount = 0;
            var errorCounts = new Dictionary<string, int>();

            for (int i = 0; i < studentFrames.Count; i++)
            {
                var sf = studentFrames[i];
                float t = sf.timestamp;

                int tIdx = (i < alignedTeacherIndices.Length)
                    ? Mathf.Clamp(alignedTeacherIndices[i], 0, teacherFrames.Count - 1)
                    : 0;
                var teacherJoints = teacherFrames[tIdx];

                FrameEvaluationResult fr;
                if (!sf.poseDetected || sf.joints == null || sf.joints.Count < 6)
                {
                    // Khung hình không phát hiện được người / người ra ngoài camera
                    fr = new FrameEvaluationResult
                    {
                        frameIndex = i,
                        timestamp = t,
                        maxScore = 13.0f,
                        frameScore = 0f,
                        isPass = false
                    };
                    foreach (var j in DanceMLPipeline.JOINTS)
                    {
                        fr.joints[j] = new JointScoreItem
                        {
                            jointName = j,
                            studentPos = Vector2.zero,
                            teacherPos = teacherJoints.ContainsKey(j) ? teacherJoints[j] : Vector2.zero,
                            isPass = 0,
                            jointError = 1.0f
                        };
                        if (!errorCounts.ContainsKey(j)) errorCounts[j] = 0;
                        errorCounts[j]++;
                    }
                }
                else
                {
                    // Chấm điểm thực tế qua DanceMLPipeline (chuẩn hóa hình học & CatBoost classifier)
                    fr = mlPipeline.EvaluateFrame(i, t, sf.joints, teacherJoints);
                    if (fr.isPass) passCount++;

                    foreach (var kvp in fr.joints)
                    {
                        if (kvp.Value.isPass == 0)
                        {
                            if (!errorCounts.ContainsKey(kvp.Key)) errorCounts[kvp.Key] = 0;
                            errorCounts[kvp.Key]++;
                        }
                    }
                }

                frameResults.Add(fr);
            }

            onProgress?.Invoke(0.92f, "Đối sánh căn chỉnh chuỗi động tác theo thời gian (DTW)...");
            yield return new WaitForSeconds(0.2f);

            // 4. Chạy thuật toán Dynamic Time Warping (DTW) thực tế
            var (dtwAcc, deviations, avgDrift) = DtwScorer.ComputeWithDeviations(frameResults, fps > 0f ? fps : 30f);

            // 5. Tổng hợp dữ liệu kết quả thực tế theo chuẩn processing_11_ver1 (Frame Pass Rate với T* = 8.1)
            float frameAcc = (frameResults.Count > 0) ? ((float)passCount / frameResults.Count * 100f) : 0f;
            float overallScore = Mathf.Clamp(Mathf.Round(frameAcc), 5f, 100f);

            var sessionData = new SessionEvaluationData
            {
                lessonTitle = string.IsNullOrEmpty(lessonTitle) ? "HIP HOP BASIC • ARM GROOVE" : lessonTitle,
                sessionDuration = sessionDuration > 0f ? sessionDuration : (studentFrames.Count / (fps > 0 ? fps : 30f)),
                analyzedVideoPath = videoPath,
                isImportedVideo = true,
                frameByFrameAccuracy = Mathf.Round(frameAcc),
                dtwAccuracy = Mathf.Round(dtwAcc),
                overallAccuracy = overallScore,
                dtwDeviations = deviations,
                dtwTimeDriftSeconds = avgDrift,
                frameResults = frameResults,
                totalMovementsEvaluated = Mathf.Max(6, Mathf.RoundToInt(sessionDuration / 2.5f)),
                xpEarned = Mathf.Clamp(Mathf.RoundToInt(overallScore * 1.5f), 10, 150),
                jointErrorCounts = errorCounts
            };

            // Top khớp sai lệch nhiều nhất
            var sortedErrors = new List<KeyValuePair<string, int>>(errorCounts);
            sortedErrors.Sort((a, b) => b.Value.CompareTo(a.Value));
            sessionData.topErrorJoints = sortedErrors;

            // Nhận xét thực tế từ AI Coach dựa trên điểm số và khớp sai nhiều nhất
            if (overallScore >= 88f)
            {
                sessionData.coachAdvice = "Vũ đạo xuất sắc! Động tác chuẩn xác và vào nhịp rất đều.";
            }
            else if (overallScore >= 70f)
            {
                if (Mathf.Abs(avgDrift) > 0.2f)
                {
                    string dir = avgDrift > 0 ? "sớm" : "trễ";
                    sessionData.coachAdvice = $"Khá tốt! Bạn có xu hướng đi {dir} nhịp {Mathf.Abs(avgDrift):F2}s ở các đoạn chuyển động.";
                }
                else if (sortedErrors.Count > 0 && sortedErrors[0].Value > (frameResults.Count * 0.25f))
                {
                    string jointVi = TranslateJoint(sortedErrors[0].Key);
                    sessionData.coachAdvice = $"Khá tốt! Hãy chú ý mở góc {jointVi} chuẩn xác hơn theo huấn luyện viên.";
                }
                else
                {
                    sessionData.coachAdvice = "Khá tốt! Giữ vững biên độ chuyển động và duy trì nhịp đều đặn.";
                }
            }
            else if (overallScore >= 45f)
            {
                if (sortedErrors.Count > 0)
                {
                    string jointVi = TranslateJoint(sortedErrors[0].Key);
                    sessionData.coachAdvice = $"Cần luyện tập thêm: Khớp {jointVi} sai lệch nhiều so với động tác mẫu.";
                }
                else
                {
                    sessionData.coachAdvice = "Cần luyện tập thêm: Động tác chưa theo kịp biên độ và tiết tấu của bài nhảy.";
                }
            }
            else
            {
                sessionData.coachAdvice = "Động tác sai lệch nhiều so với bài nhảy mẫu. Hãy xem lại video hướng dẫn và tập chậm từng nhịp!";
            }

            // 6. Tính 7 kỹ năng thực tế & PlayerPrefs persistence
            DanceSessionManager.CalculateSevenSkills(sessionData, frameResults, errorCounts);
            DanceSessionManager.ApplyPlayerPrefsData(sessionData);

            onProgress?.Invoke(1.0f, "Hoàn tất phân tích!");
            yield return new WaitForSeconds(0.2f);

            Debug.Log($"[VideoAnalysisService] Phân tích hoàn tất! Video: {Path.GetFileName(videoPath)}, Điểm tổng kết: {overallScore:F0}% (Khung: {frameAcc:F1}%, DTW: {dtwAcc:F1}%), Lệch nhịp: {avgDrift:F2}s");
            onComplete?.Invoke(sessionData);
        }

        #region Pose JSON Parsing & Teacher Data

        public class FramePoseData
        {
            public int frameIndex;
            public float timestamp;
            public bool poseDetected;
            public Dictionary<string, Vector2> joints = new Dictionary<string, Vector2>();
            public Dictionary<string, float> visibilities = new Dictionary<string, float>();
        }

        private static void InterpolateLowVisibility(List<FramePoseData> frames, float visThreshold = 0.5f)
        {
            if (frames == null || frames.Count == 0) return;
            int n = frames.Count;

            foreach (var joint in DanceMLPipeline.JOINTS)
            {
                var validIndices = new List<int>();
                for (int i = 0; i < n; i++)
                {
                    if (frames[i].joints.ContainsKey(joint))
                    {
                        float vis = frames[i].visibilities.TryGetValue(joint, out float v) ? v : 1.0f;
                        if (vis >= visThreshold) validIndices.Add(i);
                    }
                }

                if (validIndices.Count == 0) continue;

                // Nội suy tuyến tính giữa các frame hợp lệ và mở rộng 2 đầu (limit_direction='both')
                int firstValid = validIndices[0];
                Vector2 firstPos = frames[firstValid].joints[joint];
                for (int i = 0; i < firstValid; i++)
                {
                    frames[i].joints[joint] = firstPos;
                    frames[i].visibilities[joint] = 1.0f;
                }

                for (int k = 0; k < validIndices.Count - 1; k++)
                {
                    int idxA = validIndices[k];
                    int idxB = validIndices[k + 1];
                    if (idxB - idxA <= 1) continue;

                    Vector2 posA = frames[idxA].joints[joint];
                    Vector2 posB = frames[idxB].joints[joint];
                    for (int i = idxA + 1; i < idxB; i++)
                    {
                        float alpha = (float)(i - idxA) / (idxB - idxA);
                        frames[i].joints[joint] = Vector2.Lerp(posA, posB, alpha);
                        frames[i].visibilities[joint] = 1.0f;
                    }
                }

                int lastValid = validIndices[validIndices.Count - 1];
                Vector2 lastPos = frames[lastValid].joints[joint];
                for (int i = lastValid + 1; i < n; i++)
                {
                    frames[i].joints[joint] = lastPos;
                    frames[i].visibilities[joint] = 1.0f;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (frames[i].joints.Count >= 6) frames[i].poseDetected = true;
            }
        }

        private List<FramePoseData> ParseStudentPoseJson(string jsonPath, out float duration, out float fps)
        {
            duration = 12.3f;
            fps = 30f;
            var list = new List<FramePoseData>();

            try
            {
                string json = File.ReadAllText(jsonPath);

                // Đọc metadata
                int dfIdx = json.IndexOf("\"duration\":");
                if (dfIdx >= 0)
                {
                    string dStr = json.Substring(dfIdx + 11, 10).Split(',', '\n', '}')[0].Trim();
                    float.TryParse(dStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out duration);
                }

                int fpsIdx = json.IndexOf("\"fps\":");
                if (fpsIdx >= 0)
                {
                    string fStr = json.Substring(fpsIdx + 6, 8).Split(',', '\n', '}')[0].Trim();
                    float.TryParse(fStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fps);
                }

                // Quét từng frame và student_joints
                int sPos = 0;
                int frameCounter = 0;

                while ((sPos = json.IndexOf("\"student_joints\":", sPos)) >= 0)
                {
                    int openBrace = json.IndexOf('{', sPos);
                    if (openBrace < 0) break;

                    int depth = 0;
                    int closeBrace = -1;
                    for (int i = openBrace; i < json.Length; i++)
                    {
                        if (json[i] == '{') depth++;
                        else if (json[i] == '}')
                        {
                            depth--;
                            if (depth == 0) { closeBrace = i; break; }
                        }
                    }
                    if (closeBrace < 0) break;

                    string block = json.Substring(openBrace, closeBrace - openBrace + 1);
                    var fpd = new FramePoseData
                    {
                        frameIndex = frameCounter,
                        timestamp = frameCounter / (fps > 0 ? fps : 30f),
                        poseDetected = true
                    };

                    int detectedJointsCount = 0;
                    foreach (var j in DanceMLPipeline.JOINTS)
                    {
                        int jIdx = block.IndexOf("\"" + j + "\":");
                        if (jIdx >= 0)
                        {
                            int xIdx = block.IndexOf("\"x\":", jIdx);
                            int yIdx = block.IndexOf("\"y\":", jIdx);
                            if (xIdx >= 0 && yIdx >= 0)
                            {
                                string xStr = block.Substring(xIdx + 4, 8).Split(',', '}')[0].Trim();
                                string yStr = block.Substring(yIdx + 4, 8).Split(',', '}')[0].Trim();
                                float x, y;
                                if (float.TryParse(xStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                                    float.TryParse(yStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                                {
                                    fpd.joints[j] = new Vector2(x, y);
                                    float vis = 1.0f;
                                    int vIdx = block.IndexOf("\"visibility\":", jIdx);
                                    int nextBrace = block.IndexOf('}', jIdx);
                                    if (vIdx >= 0 && (nextBrace < 0 || vIdx < nextBrace))
                                    {
                                        string vStr = block.Substring(vIdx + 13, Math.Min(8, block.Length - (vIdx + 13))).Split(',', '}')[0].Trim();
                                        float.TryParse(vStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out vis);
                                    }
                                    fpd.visibilities[j] = vis;
                                    detectedJointsCount++;
                                }
                            }
                        }
                    }

                    fpd.poseDetected = (detectedJointsCount >= 6);
                    list.Add(fpd);

                    frameCounter++;
                    sPos = closeBrace + 1;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VideoAnalysisService] Lỗi khi phân tích JSON học viên: " + ex.Message);
            }

            return list;
        }

        private List<Dictionary<string, Vector2>> LoadTeacherReferenceData(VideoClip refClip, out float duration)
        {
            duration = 12.27f;
            var list = new List<Dictionary<string, Vector2>>();

            // 1. Thử nạp 1_teacher_pose.json từ StreamingAssets
            string path1 = Path.Combine(Application.streamingAssetsPath, "1_teacher_pose.json");
            if (!File.Exists(path1))
            {
                path1 = Path.Combine(Application.dataPath, "_Project/StreamingAssets/1_teacher_pose.json");
            }

            if (File.Exists(path1))
            {
                list = ParseTeacherPoseJson(path1, "student_joints", out duration);
                if (list.Count > 0) return list;
            }

            // 2. Thử nạp dance_demo_data.json
            string path2 = Path.Combine(Application.streamingAssetsPath, "dance_demo_data.json");
            if (!File.Exists(path2))
            {
                path2 = Path.Combine(Application.dataPath, "_Project/StreamingAssets/dance_demo_data.json");
            }

            if (File.Exists(path2))
            {
                list = ParseTeacherPoseJson(path2, "teacher_joints", out duration);
                if (list.Count > 0) return list;
            }

            return list;
        }

        private List<Dictionary<string, Vector2>> ParseTeacherPoseJson(string path, string jointsKey, out float duration)
        {
            duration = 12.27f;
            var list = new List<Dictionary<string, Vector2>>();

            try
            {
                string json = File.ReadAllText(path);

                int dIdx = json.IndexOf("\"duration\":");
                if (dIdx >= 0)
                {
                    string dStr = json.Substring(dIdx + 11, 10).Split(',', '\n', '}')[0].Trim();
                    float.TryParse(dStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out duration);
                }

                int pos = 0;
                string searchKey = "\"" + jointsKey + "\":";
                while ((pos = json.IndexOf(searchKey, pos)) >= 0)
                {
                    int openBrace = json.IndexOf('{', pos);
                    if (openBrace < 0) break;

                    int depth = 0;
                    int closeBrace = -1;
                    for (int i = openBrace; i < json.Length; i++)
                    {
                        if (json[i] == '{') depth++;
                        else if (json[i] == '}')
                        {
                            depth--;
                            if (depth == 0) { closeBrace = i; break; }
                        }
                    }
                    if (closeBrace < 0) break;

                    string block = json.Substring(openBrace, closeBrace - openBrace + 1);
                    var dict = new Dictionary<string, Vector2>();

                    foreach (var j in DanceMLPipeline.JOINTS)
                    {
                        int jIdx = block.IndexOf("\"" + j + "\":");
                        if (jIdx >= 0)
                        {
                            int xIdx = block.IndexOf("\"x\":", jIdx);
                            int yIdx = block.IndexOf("\"y\":", jIdx);
                            if (xIdx >= 0 && yIdx >= 0)
                            {
                                string xStr = block.Substring(xIdx + 4, 8).Split(',', '}')[0].Trim();
                                string yStr = block.Substring(yIdx + 4, 8).Split(',', '}')[0].Trim();
                                float x, y;
                                if (float.TryParse(xStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
                                    float.TryParse(yStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y))
                                {
                                    dict[j] = new Vector2(x, y);
                                }
                            }
                        }
                    }

                    list.Add(dict);
                    pos = closeBrace + 1;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[VideoAnalysisService] Lỗi khi nạp teacher poses: " + ex.Message);
            }

            return list;
        }

        private static string TranslateJoint(string j)
        {
            switch (j)
            {
                case "left_wrist": return "cổ tay trái";
                case "right_wrist": return "cổ tay phải";
                case "left_elbow": return "khuỷu tay trái";
                case "right_elbow": return "khuỷu tay phải";
                case "left_shoulder": return "vai trái";
                case "right_shoulder": return "vai phải";
                case "left_hip": return "hông trái";
                case "right_hip": return "hông phải";
                case "left_knee": return "đầu gối trái";
                case "right_knee": return "đầu gối phải";
                case "left_ankle": return "cổ chân trái";
                case "right_ankle": return "cổ chân phải";
                case "nose": return "đầu / mũi";
                default: return j;
            }
        }

        #endregion

        #region Video Exporting

        /// <summary>
        /// Xuất video MP4 có gắn khung xương skeleton và HUD điểm số bằng backend OpenCV.
        /// </summary>
        public void ExportVideoWithSkeleton(
            string videoPath,
            SessionEvaluationData sessionData,
            Action<float> onProgress,
            Action<bool, string> onComplete)
        {
            StartCoroutine(ExportVideoRoutine(videoPath, sessionData, onProgress, onComplete));
        }

        private IEnumerator ExportVideoRoutine(
            string videoPath,
            SessionEvaluationData sessionData,
            Action<float> onProgress,
            Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                onComplete?.Invoke(false, "Không tìm thấy video gốc để xuất!");
                yield break;
            }

            onProgress?.Invoke(0.05f);
            yield return null;

            string exportsDir = Path.Combine(Application.dataPath, "../Exports");
            Directory.CreateDirectory(exportsDir);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outVideoPath = Path.Combine(exportsDir, $"DanceUp_Analyzed_{timestamp}.mp4");

            // Lưu analysis data ra JSON tạm thời
            string tempDir = Path.Combine(Application.temporaryCachePath, "DanceUpAnalysis");
            Directory.CreateDirectory(tempDir);
            string tempAnalysisJson = Path.Combine(tempDir, $"analysis_export_{timestamp}.json");

            string serializedJson = JsonUtility.ToJson(sessionData);
            File.WriteAllText(tempAnalysisJson, serializedJson, Encoding.UTF8);

            string scriptPath = Path.Combine(Application.dataPath, "_Project/Scripts/AI/pose_extractor.py");
            string pyExe = ResolvePythonExecutable();

            bool isDone = false;
            bool exportSuccess = false;
            float exportProgress = 0f;
            string exportError = "";

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = pyExe,
                        Arguments = $"-u \"{scriptPath}\" --mode export_video --video \"{videoPath}\" --analysis \"{tempAnalysisJson}\" --output-video \"{outVideoPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (var process = new Process { StartInfo = psi })
                    {
                        process.OutputDataReceived += (s, e) =>
                        {
                            if (string.IsNullOrEmpty(e.Data)) return;
                            string line = e.Data.Trim();

                            if (line.StartsWith("EXPORT_PROGRESS:"))
                            {
                                string pStr = line.Substring("EXPORT_PROGRESS:".Length).Split(' ')[0];
                                if (float.TryParse(pStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float p))
                                {
                                    exportProgress = p / 100f;
                                }
                            }
                            else if (line.StartsWith("EXPORT_COMPLETE:"))
                            {
                                exportSuccess = true;
                            }
                        };

                        process.ErrorDataReceived += (s, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data) && !e.Data.Contains("W0000") && !e.Data.Contains("oneDNN"))
                            {
                                exportError += e.Data + "\n";
                            }
                        };

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit(120000); // 120 giây timeout

                        if (process.ExitCode == 0 && File.Exists(outVideoPath))
                        {
                            exportSuccess = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    exportError = ex.Message;
                    exportSuccess = false;
                }
                finally
                {
                    isDone = true;
                }
            });

            while (!isDone)
            {
                onProgress?.Invoke(Mathf.Clamp01(exportProgress));
                yield return new WaitForSeconds(0.1f);
            }

            if (exportSuccess && File.Exists(outVideoPath))
            {
                onProgress?.Invoke(1.0f);
                onComplete?.Invoke(true, Path.GetFullPath(outVideoPath));
            }
            else
            {
                Debug.LogWarning("[VideoAnalysisService] Export failed: " + exportError);
                onComplete?.Invoke(false, "Lỗi khi xuất video: " + exportError);
            }
        }

        #endregion
    }
}
