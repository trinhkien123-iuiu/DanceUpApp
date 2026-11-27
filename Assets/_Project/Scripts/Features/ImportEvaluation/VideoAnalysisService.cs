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

        private class CachedReferenceData
        {
            public long fileStampTicks;
            public float duration;
            public float fps;
            public List<Dictionary<string, Vector2>> frames;
        }

        private static readonly Dictionary<string, CachedReferenceData> referencePoseCache =
            new Dictionary<string, CachedReferenceData>();

        private const float PASS_STANDARD_THRESHOLD = 70f;

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
        /// Bắt đầu phân tích toàn diện một video bài nhảy có sẵn (hỗ trợ cả video mẫu tùy chọn).
        /// </summary>
        public void StartVideoAnalysis(
            string videoPath,
            VideoClip referenceClip,
            string lessonTitle,
            Action<float, string> onProgress,
            Action<SessionEvaluationData> onComplete,
            Action<string> onError,
            string referenceVideoPath = "")
        {
            StartCoroutine(AnalyzeVideoRoutine(videoPath, referenceClip, lessonTitle, referenceVideoPath, onProgress, onComplete, onError));
        }

        private IEnumerator RunPoseExtractorAsync(
            string pyExe,
            string scriptPath,
            string targetVideoPath,
            string outJsonPath,
            float progStart,
            float progEnd,
            string stageLabel,
            Action<float, string> onProgress,
            Action<bool, string> onResult)
        {
            bool pythonSuccess = false;
            float pythonProgress = 0f;
            string pythonError = "";
            bool isDone = false;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = pyExe,
                        Arguments = $"-u \"{scriptPath}\" --mode analyze_video --video \"{targetVideoPath}\" --output-json \"{outJsonPath}\"",
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
                        process.WaitForExit(90000);

                        if (process.ExitCode == 0 && File.Exists(outJsonPath))
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

            while (!isDone)
            {
                float mappedProgress = Mathf.Lerp(progStart, progEnd, pythonProgress);
                string statusMsg = $"{stageLabel} ({Mathf.RoundToInt(pythonProgress * 100f)}%)";
                onProgress?.Invoke(mappedProgress, statusMsg);
                yield return new WaitForSeconds(0.1f);
            }

            onResult?.Invoke(pythonSuccess && File.Exists(outJsonPath), pythonError);
        }

        private IEnumerator AnalyzeVideoRoutine(
            string videoPath,
            VideoClip referenceClip,
            string lessonTitle,
            string referenceVideoPath,
            Action<float, string> onProgress,
            Action<SessionEvaluationData> onComplete,
            Action<string> onError)
        {
            if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
            {
                onError?.Invoke("Tệp tin video bài nhảy không tồn tại hoặc đường dẫn không hợp lệ!");
                yield break;
            }

            onProgress?.Invoke(0.05f, "Đang khởi tạo bộ phân tích MediaPipe AI...");
            yield return null;

            string scriptPath = Path.Combine(Application.dataPath, "_Project/Scripts/AI/pose_extractor.py");
            string tempDir = Path.Combine(Application.temporaryCachePath, "DanceUpAnalysis");
            Directory.CreateDirectory(tempDir);
            string pyExe = ResolvePythonExecutable();

            // 1. Nạp hoặc trích xuất dữ liệu động tác mẫu (Reference Teacher Poses)
            float teacherDuration = 12.27f;
            float teacherFps = 30f;
            List<Dictionary<string, Vector2>> teacherFrames = null;
            string effectiveRefPath = referenceVideoPath;

            bool useCustomRefVideo = !string.IsNullOrEmpty(referenceVideoPath) && File.Exists(referenceVideoPath);
            bool isDefaultBuiltinTeacher = useCustomRefVideo &&
                Path.GetFileName(referenceVideoPath).Equals("1_teacher.mp4", StringComparison.OrdinalIgnoreCase);

            float studentProgStart = 0.05f;
            float studentProgEnd = 0.65f;

            if (useCustomRefVideo && !isDefaultBuiltinTeacher)
            {
                string fullRefPath = Path.GetFullPath(referenceVideoPath);
                long stamp = File.GetLastWriteTimeUtc(fullRefPath).Ticks;

                if (referencePoseCache.TryGetValue(fullRefPath, out var cached) &&
                    cached.fileStampTicks == stamp &&
                    cached.frames != null && cached.frames.Count > 0)
                {
                    teacherFrames = cached.frames;
                    teacherDuration = cached.duration;
                    teacherFps = cached.fps;
                    onProgress?.Invoke(0.25f, "Đã nạp dữ liệu pose video mẫu từ bộ nhớ đệm...");
                    yield return null;
                }
                else
                {
                    string refJson = Path.Combine(tempDir, "ref_pose_" + DateTime.Now.Ticks + ".json");
                    bool refOk = false;
                    string refErr = "";

                    yield return RunPoseExtractorAsync(
                        pyExe, scriptPath, fullRefPath, refJson,
                        0.05f, 0.35f, "Trích xuất 13 khớp video mẫu",
                        onProgress, (ok, err) => { refOk = ok; refErr = err; });

                    if (!refOk)
                    {
                        Debug.LogError("[VideoAnalysisService] Trích xuất video mẫu thất bại: " + refErr);
                        onError?.Invoke("Không thể trích xuất khớp từ video mẫu: " + refErr);
                        yield break;
                    }

                    var parsedRef = ParseStudentPoseJson(refJson, out teacherDuration, out teacherFps);
                    InterpolateLowVisibility(parsedRef, 0.5f);

                    teacherFrames = new List<Dictionary<string, Vector2>>(parsedRef.Count);
                    foreach (var rf in parsedRef)
                    {
                        teacherFrames.Add(rf.joints);
                    }

                    if (teacherFrames.Count > 0)
                    {
                        referencePoseCache[fullRefPath] = new CachedReferenceData
                        {
                            fileStampTicks = stamp,
                            duration = teacherDuration,
                            fps = teacherFps,
                            frames = teacherFrames
                        };
                    }

                    studentProgStart = 0.35f;
                    studentProgEnd = 0.72f;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(effectiveRefPath))
                {
                    string defaultTeacherMp4 = Path.Combine(Application.dataPath, "_Project/Video/1_teacher.mp4");
                    if (File.Exists(defaultTeacherMp4)) effectiveRefPath = defaultTeacherMp4;
                }
                teacherFrames = LoadTeacherReferenceData(referenceClip, out teacherDuration);
            }

            if (teacherFrames == null || teacherFrames.Count == 0)
            {
                onError?.Invoke("Không tìm thấy dữ liệu động tác mẫu của giáo viên!");
                yield break;
            }

            // 2. Trích xuất tọa độ khớp thực tế từ video bài nhảy học viên
            string tempJson = Path.Combine(tempDir, "analyzed_pose_" + DateTime.Now.Ticks + ".json");
            bool studentOk = false;
            string studentErr = "";

            yield return RunPoseExtractorAsync(
                pyExe, scriptPath, videoPath, tempJson,
                studentProgStart, studentProgEnd, "Trích xuất 13 khớp video bài nhảy",
                onProgress, (ok, err) => { studentOk = ok; studentErr = err; });

            if (!studentOk)
            {
                Debug.LogError("[VideoAnalysisService] Phân tích MediaPipe thất bại: " + studentErr);
                onError?.Invoke("Không thể trích xuất khớp từ video: " + studentErr);
                yield break;
            }

            // 3. Đọc JSON khớp của học viên từ video thực tế
            onProgress?.Invoke(0.76f, "Đọc tọa độ khớp thực tế của bài nhảy...");
            yield return null;

            float sessionDuration, fps;
            var studentFrames = ParseStudentPoseJson(tempJson, out sessionDuration, out fps);
            if (studentFrames == null || studentFrames.Count == 0)
            {
                onError?.Invoke("Không phát hiện chuyển động cơ thể trong video đã chọn!");
                yield break;
            }

            // 4. Tiền xử lý (Nội suy các khớp có Visibility < 0.5) & Căn chỉnh DTW theo chuẩn processing_11_ver1
            if (mlPipeline == null)
            {
                mlPipeline = gameObject.AddComponent<DanceMLPipeline>();
                mlPipeline.InitModel();
            }
            mlPipeline.InitModel();
            mlPipeline.ResetSession();

            InterpolateLowVisibility(studentFrames, 0.5f);

            var studentNormSeq = new List<Dictionary<string, Vector2>>(studentFrames.Count);
            var studentRawSeq = new List<Dictionary<string, Vector2>>(studentFrames.Count);
            for (int i = 0; i < studentFrames.Count; i++)
            {
                studentRawSeq.Add(studentFrames[i].joints);
                studentNormSeq.Add(mlPipeline.NormalizePose(studentFrames[i].joints));
            }

            // Kiểm tra video đứng yên / không có chuyển động vũ đạo thực tế
            float sequenceMotionStd = mlPipeline.ComputeSequenceMotionStd(studentRawSeq);
            bool isStaticVideo = sequenceMotionStd < 0.010f;

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
                        teacherFrameIndex = tIdx,
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
                    fr.teacherFrameIndex = tIdx;

                    if (isStaticVideo)
                    {
                        fr.isPass = false;
                        fr.frameScore = Mathf.Min(fr.frameScore, 4.0f);
                    }

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
            yield return new WaitForSeconds(0.15f);

            // 5. Chạy thuật toán DTW thực tế trực tiếp trên hai chuỗi đã chuẩn hóa (tránh chạy lặp trên chuỗi đã warp)
            float actualStudentFps = fps > 0f ? fps : 30f;
            float actualTeacherFps = teacherFps > 0f ? teacherFps : 30f;
            var (dtwAcc, deviations, avgDrift) = DtwScorer.ComputeWithDeviationsFromSequences(
                frameResults, studentNormSeq, teacherNormSeq, actualStudentFps, actualTeacherFps);

            // 6. Tổng hợp dữ liệu kết quả thực tế theo chuẩn processing_11_ver1 (Frame Pass Rate với T* = 8.1)
            float frameAcc = (frameResults.Count > 0) ? ((float)passCount / frameResults.Count * 100f) : 0f;
            float overallScore = isStaticVideo ? 5f : Mathf.Clamp(Mathf.Round(frameAcc), 5f, 100f);
            bool passedStandard = !isStaticVideo && overallScore >= PASS_STANDARD_THRESHOLD;

            var sessionData = new SessionEvaluationData
            {
                lessonTitle = string.IsNullOrEmpty(lessonTitle) ? "HIP HOP BASIC • ARM GROOVE" : lessonTitle,
                sessionDuration = sessionDuration > 0f ? sessionDuration : (studentFrames.Count / actualStudentFps),
                analyzedVideoPath = videoPath,
                referenceVideoPath = effectiveRefPath ?? "",
                isImportedVideo = true,
                isPassedStandard = passedStandard,
                passStandardThreshold = PASS_STANDARD_THRESHOLD,
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

            // Đổ các đoạn cần cải thiện từ DTW vào incorrectSections để nút "Xem lại đoạn lỗi" nhảy chính xác
            if (deviations != null)
            {
                foreach (var dev in deviations)
                {
                    if (dev.severity >= 1)
                    {
                        sessionData.incorrectSections.Add(new IncorrectSectionInfo
                        {
                            startTime = dev.startTime,
                            endTime = dev.endTime,
                            primaryJointError = dev.primaryJoint,
                            description = $"{dev.deviationType}: {dev.description}",
                            avgScore = dev.severity == 2 ? 4f : 7f
                        });
                    }
                }
            }

            // Tạo accuracyOverTime curve (lấy mẫu đều cho biểu đồ nếu có)
            int curveStep = Mathf.Max(1, frameResults.Count / 25);
            for (int i = 0; i < frameResults.Count; i += curveStep)
            {
                sessionData.accuracyOverTime.Add(new Vector2(frameResults[i].timestamp, frameResults[i].cumulativeAccuracy));
            }

            // Top khớp sai lệch nhiều nhất
            var sortedErrors = new List<KeyValuePair<string, int>>(errorCounts);
            sortedErrors.Sort((a, b) => b.Value.CompareTo(a.Value));
            sessionData.topErrorJoints = sortedErrors;

            // Nhận xét thực tế từ AI Coach dựa trên điểm số, chuyển động và khớp sai nhiều nhất
            if (isStaticVideo)
            {
                sessionData.coachAdvice = "Video hầu như không có chuyển động vũ đạo! Hãy tải lên video có động tác nhảy thực tế.";
            }
            else if (overallScore >= 88f)
            {
                sessionData.coachAdvice = "Đạt chuẩn xuất sắc! Động tác chuẩn xác và vào nhịp rất đều.";
            }
            else if (overallScore >= PASS_STANDARD_THRESHOLD)
            {
                if (Mathf.Abs(avgDrift) > 0.25f)
                {
                    string dir = avgDrift > 0 ? "trễ" : "sớm";
                    sessionData.coachAdvice = $"Đạt chuẩn! Tuy nhiên bạn có xu hướng đi {dir} nhịp {Mathf.Abs(avgDrift):F2}s ở vài đoạn.";
                }
                else if (sortedErrors.Count > 0 && sortedErrors[0].Value > (frameResults.Count * 0.25f))
                {
                    string jointVi = TranslateJoint(sortedErrors[0].Key);
                    sessionData.coachAdvice = $"Đạt chuẩn! Hãy chú ý mở góc {jointVi} chuẩn xác hơn để đạt điểm tối đa.";
                }
                else
                {
                    sessionData.coachAdvice = "Đạt chuẩn! Giữ vững biên độ chuyển động và duy trì nhịp đều đặn.";
                }
            }
            else if (overallScore >= 45f)
            {
                if (sortedErrors.Count > 0)
                {
                    string jointVi = TranslateJoint(sortedErrors[0].Key);
                    sessionData.coachAdvice = $"Chưa đạt chuẩn: Khớp {jointVi} sai lệch nhiều so với video mẫu.";
                }
                else
                {
                    sessionData.coachAdvice = "Chưa đạt chuẩn: Động tác chưa theo kịp biên độ và tiết tấu bài mẫu.";
                }
            }
            else
            {
                sessionData.coachAdvice = "Chưa đạt chuẩn: Động tác sai lệch nhiều so với bài nhảy mẫu. Hãy xem lại từng đoạn lỗi!";
            }

            // 7. Tính 7 kỹ năng thực tế & PlayerPrefs persistence
            DanceSessionManager.CalculateSevenSkills(sessionData, frameResults, errorCounts);
            DanceSessionManager.ApplyPlayerPrefsData(sessionData);

            onProgress?.Invoke(1.0f, "Hoàn tất phân tích!");
            yield return new WaitForSeconds(0.2f);

            Debug.Log($"[VideoAnalysisService] Phân tích hoàn tất! Ref: {(string.IsNullOrEmpty(effectiveRefPath) ? "default" : Path.GetFileName(effectiveRefPath))}, Student: {Path.GetFileName(videoPath)}, Verdict: {(passedStandard ? "ĐẠT CHUẨN" : "CHƯA ĐẠT")}, Điểm: {overallScore:F0}% (Khung: {frameAcc:F1}%, DTW: {dtwAcc:F1}%), Drift: {avgDrift:F2}s, MotionStd: {sequenceMotionStd:F4}");
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

            // Lưu analysis data ra JSON đầy đủ cả Dictionary khớp (JsonUtility không serialize được Dictionary!)
            string tempDir = Path.Combine(Application.temporaryCachePath, "DanceUpAnalysis");
            Directory.CreateDirectory(tempDir);
            string tempAnalysisJson = Path.Combine(tempDir, $"analysis_export_{timestamp}.json");

            string serializedJson = SerializeExportAnalysisJson(sessionData);
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

        private static string SerializeExportAnalysisJson(SessionEvaluationData data)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder(65536);
            sb.Append("{\n");
            sb.AppendFormat(inv, "  \"overallAccuracy\": {0:F1},\n", data != null ? data.overallAccuracy : 0f);
            sb.AppendFormat("  \"isPassedStandard\": {0},\n", (data != null && data.isPassedStandard) ? "true" : "false");
            sb.Append("  \"frames\": [\n");

            var frames = data != null ? data.frameResults : null;
            if (frames != null)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    var fr = frames[i];
                    sb.Append("    {\n");
                    sb.AppendFormat(inv, "      \"frame_index\": {0},\n", fr.frameIndex);
                    sb.AppendFormat(inv, "      \"timestamp\": {0:F4},\n", fr.timestamp);
                    sb.AppendFormat(inv, "      \"frame_score\": {0:F2},\n", fr.frameScore);
                    sb.AppendFormat("      \"is_pass\": {0},\n", fr.isPass ? "true" : "false");
                    sb.Append("      \"student_joints\": {");

                    if (fr.joints != null && fr.joints.Count > 0)
                    {
                        int jCount = 0;
                        foreach (var kvp in fr.joints)
                        {
                            if (jCount > 0) sb.Append(",");
                            sb.AppendFormat(inv,
                                "\"{0}\":{{\"x\":{1:F4},\"y\":{2:F4},\"is_pass\":{3},\"joint_error\":{4:F4}}}",
                                kvp.Key, kvp.Value.studentPos.x, kvp.Value.studentPos.y,
                                kvp.Value.isPass, kvp.Value.jointError);
                            jCount++;
                        }
                    }

                    sb.Append("}\n    }");
                    if (i < frames.Count - 1) sb.Append(",");
                    sb.Append("\n");
                }
            }

            sb.Append("  ]\n}");
            return sb.ToString();
        }

        #endregion
    }
}
