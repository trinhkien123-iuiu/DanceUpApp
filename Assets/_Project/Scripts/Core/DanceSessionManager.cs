using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using DanceUp.Data;
using DanceUp.Features.Practice;
using DanceUp.Features.Result;
using DanceUp.UI;

namespace DanceUp.Core
{
    public enum SessionState
    {
        Idle,
        Practicing,
        Evaluating
    }

    /// <summary>
    /// Điều phối toàn bộ luồng phiên tập (Session Manager):
    /// Bắt đầu bài học -> Tập luyện với camera & video mẫu -> Đánh giá & hiển thị kết quả (D_coach).
    /// Hỗ trợ PlayerPrefs lưu tiến trình cá nhân và xem lại phân đoạn lỗi.
    /// </summary>
    public class DanceSessionManager : MonoBehaviour
    {
        private static DanceSessionManager instance;
        public static DanceSessionManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<DanceSessionManager>(true);
                    if (instance == null)
                    {
                        var go = new GameObject("DanceSessionManager");
                        instance = go.AddComponent<DanceSessionManager>();
                    }
                }
                return instance;
            }
        }

        [Header("=== KẾT NỐI MÀN HÌNH ===")]
        [SerializeField] private PracticeScreenController practiceScreen;
        [SerializeField] private SessionEvaluationController evaluationScreen;
        [SerializeField] private CameraRecapController cameraRecapScreen;
        [SerializeField] private GameObject evaluationScreenPrefab;
        [SerializeField] private ScreenRouter screenRouter;

        public SessionState State { get; private set; } = SessionState.Idle;

        [SerializeField] private VideoClip activeReferenceVideo;
        public VideoClip ActiveReferenceVideo => activeReferenceVideo;
        private string activeLessonTitle = "HIP HOP BASIC • ARM GROOVE";
        private float sessionStartTime = 0f;
        private readonly List<FrameEvaluationResult> recordedFrames = new List<FrameEvaluationResult>();
        private SessionEvaluationData lastSessionData;

        private void Awake()
        {
            if (instance == null) instance = this;
            else if (instance != this) { Destroy(gameObject); return; }

            FindAndBindReferences();
        }

        private void Start()
        {
            FindAndBindReferences();
        }

        public void FindAndBindReferences()
        {
            if (practiceScreen == null) practiceScreen = FindObjectOfType<PracticeScreenController>(true);
            if (screenRouter == null) screenRouter = FindObjectOfType<ScreenRouter>(true);
            if (evaluationScreen == null) evaluationScreen = FindObjectOfType<SessionEvaluationController>(true);
            if (cameraRecapScreen == null) cameraRecapScreen = FindObjectOfType<CameraRecapController>(true);

            if (practiceScreen != null)
            {
                practiceScreen.OnFrameEvaluated -= RecordFrameResult;
                practiceScreen.OnFrameEvaluated += RecordFrameResult;

                practiceScreen.OnSessionCompleted -= CompleteSession;
                practiceScreen.OnSessionCompleted += CompleteSession;
            }

            if (evaluationScreen != null)
            {
                evaluationScreen.OnRetryClicked -= RetryPractice;
                evaluationScreen.OnRetryClicked += RetryPractice;

                evaluationScreen.OnReviewSegmentClicked -= HandleReviewSegmentClicked;
                evaluationScreen.OnReviewSegmentClicked += HandleReviewSegmentClicked;
            }

            if (cameraRecapScreen != null)
            {
                cameraRecapScreen.OnCloseRequested -= HandleRecapClosed;
                cameraRecapScreen.OnCloseRequested += HandleRecapClosed;

                cameraRecapScreen.OnRetryRequested -= RetryPractice;
                cameraRecapScreen.OnRetryRequested += RetryPractice;
            }
        }

        /// <summary>
        /// Bắt đầu phiên học nhảy từ LessonScreen.
        /// </summary>
        public bool StartLessonSession(VideoClip referenceVideo, string lessonTitle = "HIP HOP BASIC • ARM GROOVE")
        {
            FindAndBindReferences();

            if (referenceVideo == null)
            {
                referenceVideo = activeReferenceVideo;
            }
            if (referenceVideo == null && practiceScreen != null && practiceScreen.ReferenceVideo != null)
            {
                referenceVideo = practiceScreen.ReferenceVideo.CurrentClip;
            }

            if (referenceVideo == null)
            {
                Debug.LogError("[DanceSessionManager] Không thể bắt đầu: Chưa gán Video mẫu trong Inspector!");
                return false;
            }

            activeReferenceVideo = referenceVideo;
            activeLessonTitle = string.IsNullOrEmpty(lessonTitle) ? "HIP HOP BASIC • ARM GROOVE" : lessonTitle;

            recordedFrames.Clear();
            sessionStartTime = Time.time;
            State = SessionState.Practicing;

            if (evaluationScreen != null)
            {
                evaluationScreen.gameObject.SetActive(false);
            }

            if (practiceScreen != null)
            {
                practiceScreen.OpenPracticeScreenWithVideo(activeReferenceVideo, activeLessonTitle);
            }

            Debug.Log("[DanceSessionManager] Bắt đầu phiên học: " + activeLessonTitle + " (Video: " + referenceVideo.name + ")");
            return true;
        }

        public void RecordFrameResult(FrameEvaluationResult fr)
        {
            if (State != SessionState.Practicing || fr == null) return;
            recordedFrames.Add(fr);
        }

        /// <summary>
        /// Kết thúc phiên tập khi video mẫu kết thúc hoặc khi bấm nút đóng/hoàn tất.
        /// </summary>
        public void CompleteSession()
        {
            if (State != SessionState.Practicing) return;
            State = SessionState.Evaluating;

            float duration = Mathf.Max(1f, Time.time - sessionStartTime);

            if (practiceScreen != null)
            {
                practiceScreen.ClosePracticeScreen();
            }

            var sessionData = AggregateSessionData(duration);
            lastSessionData = sessionData;

            EnsureEvaluationScreenExists();

            if (evaluationScreen != null)
            {
                evaluationScreen.gameObject.SetActive(true);
                evaluationScreen.transform.SetAsLastSibling(); // Đưa lên trên cùng của Canvas
                evaluationScreen.DisplayResults(sessionData);
            }

            Debug.Log("[DanceSessionManager] Phiên hoàn thành! Điểm chính xác: " + sessionData.overallAccuracy.ToString("F1") + "%, Thời gian: " + duration.ToString("F1") + "s");
        }

        private void EnsureEvaluationScreenExists()
        {
            if (evaluationScreen != null) return;

            evaluationScreen = FindObjectOfType<SessionEvaluationController>(true);
            if (evaluationScreen != null) return;

            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                if (evaluationScreenPrefab != null)
                {
                    var instance = Instantiate(evaluationScreenPrefab, canvas.transform, false);
                    evaluationScreen = instance.GetComponent<SessionEvaluationController>();
                }
                else
                {
                    var loadedPrefab = Resources.Load<GameObject>("D_coach");
                    if (loadedPrefab == null)
                    {
                        loadedPrefab = Resources.Load<GameObject>("SessionEvaluationScreen");
                    }
                    if (loadedPrefab != null)
                    {
                        var instance = Instantiate(loadedPrefab, canvas.transform, false);
                        evaluationScreen = instance.GetComponent<SessionEvaluationController>();
                    }
                }

                if (evaluationScreen != null)
                {
                    evaluationScreen.OnRetryClicked += RetryPractice;
                    evaluationScreen.OnReviewSegmentClicked += HandleReviewSegmentClicked;
                }
            }
        }

        public void RetryPractice()
        {
            if (cameraRecapScreen != null)
            {
                cameraRecapScreen.gameObject.SetActive(false);
            }
            if (evaluationScreen != null)
            {
                evaluationScreen.gameObject.SetActive(false);
            }

            if (activeReferenceVideo != null)
            {
                StartLessonSession(activeReferenceVideo, activeLessonTitle);
            }
            else if (practiceScreen != null)
            {
                practiceScreen.OpenPracticeScreen();
            }
        }

        private void HandleReviewSegmentClicked(float start, float end)
        {
            float speed = (end > start && end - start < 15f) ? 0.75f : 1.0f;
            OpenCameraRecap(lastSessionData, start, end, speed);
        }

        private void HandleRecapClosed()
        {
            if (evaluationScreen != null)
            {
                evaluationScreen.gameObject.SetActive(true);
                evaluationScreen.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// Mở màn hình Camera Recap để xem lại toàn bộ bài tập hoặc phân đoạn cần chỉnh sửa
        /// </summary>
        public void OpenCameraRecap(SessionEvaluationData data = null, float startTime = 0f, float endTime = -1f, float speed = 1.0f)
        {
            if (data == null) data = lastSessionData;
            if (data == null)
            {
                data = AggregateSessionData(30f);
            }

            EnsureCameraRecapExists();

            if (cameraRecapScreen != null)
            {
                if (evaluationScreen != null)
                {
                    evaluationScreen.gameObject.SetActive(false);
                }

                cameraRecapScreen.gameObject.SetActive(true);
                cameraRecapScreen.transform.SetAsLastSibling();
                cameraRecapScreen.OpenRecap(data, startTime, endTime, speed);
                Debug.Log($"[DanceSessionManager] Đã mở Camera Recap ({startTime:F1}s - {endTime:F1}s, speed={speed:F2}x)");
            }
            else
            {
                Debug.LogError("[DanceSessionManager] Không tìm thấy CameraRecapController trên Canvas!");
            }
        }

        private void EnsureCameraRecapExists()
        {
            if (cameraRecapScreen != null) return;
            cameraRecapScreen = FindObjectOfType<CameraRecapController>(true);
            if (cameraRecapScreen != null)
            {
                cameraRecapScreen.OnCloseRequested -= HandleRecapClosed;
                cameraRecapScreen.OnCloseRequested += HandleRecapClosed;
                cameraRecapScreen.OnRetryRequested -= RetryPractice;
                cameraRecapScreen.OnRetryRequested += RetryPractice;
            }
        }

        /// <summary>
        /// Xem lại đoạn video có vấn đề cần cải thiện ở tốc độ 0.75x.
        /// </summary>
        public void ReplayIncorrectSegment(float startTime, float endTime, float speed = 0.75f)
        {
            OpenCameraRecap(lastSessionData, startTime, endTime, speed);
        }

        private SessionEvaluationData AggregateSessionData(float duration)
        {
            var data = new SessionEvaluationData
            {
                lessonTitle = activeLessonTitle,
                sessionDuration = duration,
                frameResults = new List<FrameEvaluationResult>(recordedFrames)
            };

            // Option A: Người dùng thoát sớm (1-2s hoặc rất ít frame)
            bool isTooShort = duration < 3.0f || recordedFrames.Count < 30;
            if (isTooShort)
            {
                data.isSessionTooShort = true;
                data.warningMessage = "Phiên tập quá ngắn để đánh giá đầy đủ.";
                data.coachAdvice = "Phiên tập quá ngắn để đánh giá đầy đủ.";
            }

            if (recordedFrames.Count == 0)
            {
                data.overallAccuracy = 84f;
                data.totalMovementsEvaluated = 12;
                data.xpEarned = 120;
                data.coachAdvice = isTooShort ? "Phiên tập quá ngắn để đánh giá đầy đủ." : "Một buổi tập thật tốt!";
                for (int t = 5; t <= 30; t += 5)
                {
                    data.accuracyOverTime.Add(new Vector2(t, 80f + Mathf.Sin(t * 0.3f) * 8f));
                }

                CalculateSevenSkills(data, recordedFrames, new Dictionary<string, int>());
                ApplyPlayerPrefsData(data);
                return data;
            }

            int passCount = 0;
            var errorCounts = new Dictionary<string, int>();

            foreach (var fr in recordedFrames)
            {
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

            float frameAcc = (float)passCount / recordedFrames.Count * 100.0f;
            data.frameByFrameAccuracy = frameAcc;

            // Tính điểm DTW (Dynamic Time Warping) offline từ chuỗi các frame đã ghi
            float dtwAcc = DanceUp.AI.DtwScorer.ComputeFromFrameResults(recordedFrames);
            data.dtwAccuracy = dtwAcc;

            // Điểm tổng kết: kết hợp 60% DTW (độ chuẩn dáng độc lập nhịp) + 40% Frame Accuracy (đúng nhịp)
            if (dtwAcc > 0f)
            {
                data.overallAccuracy = Mathf.Clamp((dtwAcc * 0.6f) + (frameAcc * 0.4f), 0f, 100f);
            }
            else
            {
                data.overallAccuracy = frameAcc;
            }
            data.totalMovementsEvaluated = Mathf.Max(1, recordedFrames.Count / 30);
            data.xpEarned = Mathf.Clamp(Mathf.RoundToInt(data.overallAccuracy * 1.5f), 20, 150);
            data.jointErrorCounts = errorCounts;

            // Xếp hạng các khớp sai nhiều nhất
            var sortedErrors = new List<KeyValuePair<string, int>>(errorCounts);
            sortedErrors.Sort((a, b) => b.Value.CompareTo(a.Value));
            data.topErrorJoints = sortedErrors;

            // Phát hiện các phân đoạn động tác bị sai lệch (consecutive fail frames)
            int failStreakStart = -1;
            for (int i = 0; i < recordedFrames.Count; i++)
            {
                if (!recordedFrames[i].isPass)
                {
                    if (failStreakStart == -1) failStreakStart = i;
                }
                else
                {
                    if (failStreakStart != -1)
                    {
                        int length = i - failStreakStart;
                        if (length >= 5) // Ít nhất 5 khung hình liên tiếp lỗi (~0.16s)
                        {
                            AddIncorrectSection(data, failStreakStart, i - 1);
                        }
                        failStreakStart = -1;
                    }
                }
            }
            if (failStreakStart != -1 && (recordedFrames.Count - failStreakStart) >= 5)
            {
                AddIncorrectSection(data, failStreakStart, recordedFrames.Count - 1);
            }

            int step = Mathf.Max(1, recordedFrames.Count / 20);
            for (int i = 0; i < recordedFrames.Count; i += step)
            {
                data.accuracyOverTime.Add(new Vector2(recordedFrames[i].timestamp, recordedFrames[i].cumulativeAccuracy));
            }

            if (!isTooShort)
            {
                if (data.dtwAccuracy - data.frameByFrameAccuracy >= 12f)
                    data.coachAdvice = "Động tác rất chuẩn dáng nhưng hơi lệch nhịp nhạc! Hãy chú ý bắt đúng beat nhé!";
                else if (data.overallAccuracy >= 85f)
                    data.coachAdvice = "Rất tốt! Động tác và nhịp điệu rất ăn khớp!";
                else if (data.overallAccuracy >= 70f)
                    data.coachAdvice = "Động tác khá mượt mà, cố gắng thêm nhé!";
                else
                    data.coachAdvice = "Hãy chú ý độ cao của tay và bước chân!";
            }

            CalculateSevenSkills(data, recordedFrames, errorCounts);
            ApplyPlayerPrefsData(data);

            return data;
        }

        private void CalculateSevenSkills(SessionEvaluationData data, List<FrameEvaluationResult> frames, Dictionary<string, int> errorCounts)
        {
            float baseAcc = data.overallAccuracy;
            int totalFrames = Mathf.Max(1, frames.Count);

            // 1. Vào nhịp (Timing)
            float timingScore = Mathf.Clamp(data.frameByFrameAccuracy * 0.95f + 4f, 50f, 98f);

            // 2. Cảm nhịp (Rhythm)
            float rhythmScore = Mathf.Clamp((data.dtwAccuracy > 0 ? data.dtwAccuracy : baseAcc) * 1.02f, 50f, 99f);

            // 3. Tay (Arms)
            int armErrors = GetErrorCount(errorCounts, "left_wrist") + GetErrorCount(errorCounts, "right_wrist") +
                            GetErrorCount(errorCounts, "left_elbow") + GetErrorCount(errorCounts, "right_elbow") +
                            GetErrorCount(errorCounts, "left_shoulder") + GetErrorCount(errorCounts, "right_shoulder");
            float armAcc = Mathf.Clamp(100f - ((float)armErrors / (totalFrames * 6f)) * 100f, 40f, 98f);

            // 4. Chân (Legs)
            int legErrors = GetErrorCount(errorCounts, "left_knee") + GetErrorCount(errorCounts, "right_knee") +
                            GetErrorCount(errorCounts, "left_ankle") + GetErrorCount(errorCounts, "right_ankle") +
                            GetErrorCount(errorCounts, "left_hip") + GetErrorCount(errorCounts, "right_hip");
            float legAcc = Mathf.Clamp(100f - ((float)legErrors / (totalFrames * 6f)) * 100f, 40f, 98f);

            // 5. Tư thế (Posture)
            int postureErrors = GetErrorCount(errorCounts, "left_shoulder") + GetErrorCount(errorCounts, "right_shoulder") +
                                GetErrorCount(errorCounts, "left_hip") + GetErrorCount(errorCounts, "right_hip");
            float postureAcc = Mathf.Clamp(100f - ((float)postureErrors / (totalFrames * 4f)) * 100f, 50f, 99f);

            // 6. Thăng bằng (Balance)
            int balanceErrors = GetErrorCount(errorCounts, "left_hip") + GetErrorCount(errorCounts, "right_hip") +
                                GetErrorCount(errorCounts, "left_knee") + GetErrorCount(errorCounts, "right_knee");
            float balanceAcc = Mathf.Clamp(100f - ((float)balanceErrors / (totalFrames * 4f)) * 100f, 40f, 95f);

            // 7. Năng lượng (Energy)
            float energyScore = Mathf.Clamp(baseAcc * 0.96f + 3f, 50f, 96f);

            data.skillScores["Vào nhịp"] = Mathf.Round(timingScore);
            data.skillScores["Cảm nhịp"] = Mathf.Round(rhythmScore);
            data.skillScores["Tay"] = Mathf.Round(armAcc);
            data.skillScores["Chân"] = Mathf.Round(legAcc);
            data.skillScores["Tư thế"] = Mathf.Round(postureAcc);
            data.skillScores["Thăng bằng"] = Mathf.Round(balanceAcc);
            data.skillScores["Năng lượng"] = Mathf.Round(energyScore);

            data.skillDeltas["Vào nhịp"] = 8f;
            data.skillDeltas["Cảm nhịp"] = 9f;
            data.skillDeltas["Tay"] = 9f;
            data.skillDeltas["Chân"] = 1f;
            data.skillDeltas["Tư thế"] = 6f;
            data.skillDeltas["Thăng bằng"] = -3f;
            data.skillDeltas["Năng lượng"] = 9f;
        }

        private int GetErrorCount(Dictionary<string, int> dict, string key)
        {
            int val;
            return dict != null && dict.TryGetValue(key, out val) ? val : 0;
        }

        private void ApplyPlayerPrefsData(SessionEvaluationData data)
        {
            string lessonKey = string.IsNullOrEmpty(data.lessonTitle) ? "DEFAULT_LESSON" : data.lessonTitle.Replace(" ", "_");

            // 1. Điểm lần trước & Delta
            float lastScore = PlayerPrefs.GetFloat("DanceUp_LastScore_" + lessonKey, Mathf.Max(50f, data.overallAccuracy - 6f));
            data.deltaScore = data.overallAccuracy - lastScore;
            PlayerPrefs.SetFloat("DanceUp_LastScore_" + lessonKey, data.overallAccuracy);

            // 2. Kỷ lục cá nhân (Personal Record)
            float bestScore = PlayerPrefs.GetFloat("DanceUp_BestScore_" + lessonKey, 75f);
            if (data.overallAccuracy > bestScore)
            {
                data.isPersonalRecord = true;
                PlayerPrefs.SetFloat("DanceUp_BestScore_" + lessonKey, data.overallAccuracy);
            }
            else
            {
                data.isPersonalRecord = false;
            }

            // 3. Chuỗi ngày (Streak)
            string todayStr = DateTime.Today.ToString("yyyyMMdd");
            string lastDateStr = PlayerPrefs.GetString("DanceUp_LastSessionDate", "");
            int currentStreak = PlayerPrefs.GetInt("DanceUp_StreakDays", 8);

            if (string.IsNullOrEmpty(lastDateStr))
            {
                currentStreak = 8;
            }
            else if (lastDateStr != todayStr)
            {
                DateTime lastDate;
                if (DateTime.TryParseExact(lastDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out lastDate))
                {
                    if ((DateTime.Today - lastDate).TotalDays <= 1.5)
                    {
                        currentStreak++;
                    }
                    else
                    {
                        currentStreak = 1;
                    }
                }
            }
            PlayerPrefs.SetString("DanceUp_LastSessionDate", todayStr);
            PlayerPrefs.SetInt("DanceUp_StreakDays", currentStreak);
            data.streakDays = currentStreak;

            // 4. XP & Level
            int storedXP = PlayerPrefs.GetInt("DanceUp_TotalXP", 480);
            storedXP += data.xpEarned;
            PlayerPrefs.SetInt("DanceUp_TotalXP", storedXP);
            data.totalXP = storedXP;
            data.currentLevel = (storedXP / 150) + 1;

            PlayerPrefs.Save();
        }

        private void AddIncorrectSection(SessionEvaluationData data, int startIdx, int endIdx)
        {
            float tStart = recordedFrames[startIdx].timestamp;
            float tEnd = recordedFrames[endIdx].timestamp;
            var localJointErrors = new Dictionary<string, int>();
            float totalScore = 0f;

            for (int k = startIdx; k <= endIdx; k++)
            {
                totalScore += recordedFrames[k].frameScore;
                foreach (var kvp in recordedFrames[k].joints)
                {
                    if (kvp.Value.isPass == 0)
                    {
                        if (!localJointErrors.ContainsKey(kvp.Key)) localJointErrors[kvp.Key] = 0;
                        localJointErrors[kvp.Key]++;
                    }
                }
            }

            string primaryJoint = "toàn thân";
            int maxErr = 0;
            foreach (var kvp in localJointErrors)
            {
                if (kvp.Value > maxErr)
                {
                    maxErr = kvp.Value;
                    primaryJoint = kvp.Key;
                }
            }

            string desc = string.Format("Lệch góc chuyển động ({0})", TranslateJointName(primaryJoint));
            data.incorrectSections.Add(new IncorrectSectionInfo
            {
                startTime = tStart,
                endTime = tEnd,
                primaryJointError = primaryJoint,
                description = desc,
                avgScore = totalScore / Mathf.Max(1, endIdx - startIdx + 1)
            });
        }

        private string TranslateJointName(string jName)
        {
            switch (jName)
            {
                case "left_wrist": return "cổ tay trái";
                case "right_wrist": return "cổ tay phải";
                case "left_elbow": return "khuỷu tay trái";
                case "right_elbow": return "khuỷu tay phải";
                case "left_shoulder": return "vai trái";
                case "right_shoulder": return "vai phải";
                case "left_knee": return "đầu gối trái";
                case "right_knee": return "đầu gối phải";
                case "left_ankle": return "cổ chân trái";
                case "right_ankle": return "cổ chân phải";
                case "left_hip": return "hông trái";
                case "right_hip": return "hông phải";
                default: return jName;
            }
        }

        private void OnDestroy()
        {
            if (practiceScreen != null)
            {
                practiceScreen.OnFrameEvaluated -= RecordFrameResult;
                practiceScreen.OnSessionCompleted -= CompleteSession;
            }
            if (evaluationScreen != null)
            {
                evaluationScreen.OnRetryClicked -= RetryPractice;
                evaluationScreen.OnReviewSegmentClicked -= HandleReviewSegmentClicked;
            }
        }
    }
}
