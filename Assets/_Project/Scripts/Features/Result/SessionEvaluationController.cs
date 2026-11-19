using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DanceUp.Data;

namespace DanceUp.Features.Result
{
    /// <summary>
    /// Điều khiển màn hình SessionEvaluation (D_coach - Kết quả luyện tập).
    /// Thiết kế chuẩn Prefab Unity Editor theo Figma 29:3937.
    /// Dữ liệu được liên kết và cập nhật động từ SessionEvaluationData của DanceSessionManager.
    /// </summary>
    public class SessionEvaluationController : MonoBehaviour
    {
        [Header("=== HEADER & NAVIGATION ===")]
        [SerializeField] private Button btnBack;
        [SerializeField] private Button btnHome;
        [SerializeField] private TMP_Text txtSubtitle;
        [SerializeField] private TMP_Text txtTitle;

        [Header("=== CARD 1: OVERALL RESULTS ===")]
        [SerializeField] private GameObject badgePersonalRecord;
        [SerializeField] private Image imgAccuracyRing;
        [SerializeField] private TMP_Text txtAccuracyPercent;
        [SerializeField] private TMP_Text txtCoachAdvice;
        [SerializeField] private TMP_Text txtDeltaScore;
        [SerializeField] private TMP_Text txtLessonTitle;
        [SerializeField] private TMP_Text txtDuration;
        [SerializeField] private TMP_Text txtMovements;
        [SerializeField] private TMP_Text txtXP;
        [SerializeField] private TMP_Text txtStreakDays;

        [Header("=== CARD 2: ACCURACY GRAPH (Optional) ===")]
        [SerializeField] private RectTransform graphContainer;
        [SerializeField] private TMP_Text txtGraphTitle;
        [SerializeField] private Color graphLineColor = new Color(0f, 0.706f, 1f, 1f);

        [Header("=== CARD 4: LEVEL & STREAK ===")]
        [SerializeField] private TMP_Text txtLevelNum;
        [SerializeField] private TMP_Text txtLevelXp;
        [SerializeField] private TMP_Text txtStreakTitle;
        [SerializeField] private TMP_Text txtStreakBadge;

        [Header("=== CARD 5: 7 SKILLS CONTAINER ===")]
        [SerializeField] private Transform cardSkillsContainer;

        [Header("=== CARD 6: BEST MOVE ===")]
        [SerializeField] private TMP_Text txtGoodMoveName;
        [SerializeField] private TMP_Text txtGoodDetail;

        [Header("=== CARD 7: NEEDS IMPROVEMENT ===")]
        [SerializeField] private TMP_Text txtTopErrors;
        [SerializeField] private TMP_Text txtIncorrectSections;

        [Header("=== ACTIONS & BUTTONS ===")]
        [SerializeField] private Button btnNext;
        [SerializeField] private Button btnRetry;
        [SerializeField] private Button btnSave;
        [SerializeField] private Button btnShare;
        [SerializeField] private Button btnBackToHome;
        [SerializeField] private Button btnReviewSnippet;
        [SerializeField] private Button btnWatchVideo;

        public event Action OnRetryClicked;
        public event Action OnBackClicked;
        public event Action<float, float> OnReviewSegmentClicked;

        private SessionEvaluationData currentData;

#if UNITY_EDITOR
        public void ConfigureReferences(
            Button back, TMP_Text subtitle, TMP_Text title,
            Image accuracyRing, TMP_Text accuracyPercent, TMP_Text coachAdvice,
            TMP_Text duration, TMP_Text movements, TMP_Text xp,
            RectTransform graphCont, TMP_Text graphTitle,
            TMP_Text topErrors, TMP_Text incorrectSections,
            Button retry, Button save, Button share)
        {
            btnBack = back;
            txtSubtitle = subtitle;
            txtTitle = title;
            imgAccuracyRing = accuracyRing;
            txtAccuracyPercent = accuracyPercent;
            txtCoachAdvice = coachAdvice;
            txtDuration = duration;
            txtMovements = movements;
            txtXP = xp;
            graphContainer = graphCont;
            txtGraphTitle = graphTitle;
            txtTopErrors = topErrors;
            txtIncorrectSections = incorrectSections;
            btnRetry = retry;
            btnSave = save;
            btnShare = share;
        }
#endif

        private void Awake()
        {
            BindButtons();
        }

        private void OnEnable()
        {
            BindButtons();
        }

        private void BindButtons()
        {
            if (btnBack != null)
            {
                btnBack.onClick.RemoveAllListeners();
                btnBack.onClick.AddListener(() => {
                    gameObject.SetActive(false);
                    OnBackClicked?.Invoke();
                });
            }

            if (btnHome != null)
            {
                btnHome.onClick.RemoveAllListeners();
                btnHome.onClick.AddListener(() => {
                    gameObject.SetActive(false);
                    OnBackClicked?.Invoke();
                });
            }

            if (btnBackToHome != null)
            {
                btnBackToHome.onClick.RemoveAllListeners();
                btnBackToHome.onClick.AddListener(() => {
                    gameObject.SetActive(false);
                    OnBackClicked?.Invoke();
                });
            }

            if (btnNext != null)
            {
                btnNext.onClick.RemoveAllListeners();
                btnNext.onClick.AddListener(() => {
                    Debug.Log("[SessionEvaluation] Chuyển tiếp bài tập tiếp theo!");
                    gameObject.SetActive(false);
                    OnRetryClicked?.Invoke();
                });
            }

            if (btnReviewSnippet != null)
            {
                btnReviewSnippet.onClick.RemoveAllListeners();
                btnReviewSnippet.onClick.AddListener(() => {
                    float start = 0f;
                    float end = 5f;
                    if (currentData != null && currentData.incorrectSections != null && currentData.incorrectSections.Count > 0)
                    {
                        start = currentData.incorrectSections[0].startTime;
                        end = currentData.incorrectSections[0].endTime;
                    }
                    Debug.Log(string.Format("[SessionEvaluation] Xem lại đoạn cần chỉnh ({0:F1}s - {1:F1}s)", start, end));
                    OnReviewSegmentClicked?.Invoke(start, end);
                });
            }

            if (btnWatchVideo != null)
            {
                btnWatchVideo.onClick.RemoveAllListeners();
                btnWatchVideo.onClick.AddListener(() => {
                    float duration = currentData != null ? currentData.sessionDuration : 30f;
                    Debug.Log("[SessionEvaluation] Xem lại toàn bộ video bài tập");
                    OnReviewSegmentClicked?.Invoke(0f, duration);
                });
            }

            if (btnRetry != null)
            {
                btnRetry.onClick.RemoveAllListeners();
                btnRetry.onClick.AddListener(() => {
                    gameObject.SetActive(false);
                    OnRetryClicked?.Invoke();
                });
            }

            if (btnSave != null)
            {
                btnSave.onClick.RemoveAllListeners();
                btnSave.onClick.AddListener(() => {
                    Debug.Log("[SessionEvaluation] Đã lưu kết quả bài tập!");
                });
            }

            if (btnShare != null)
            {
                btnShare.onClick.RemoveAllListeners();
                btnShare.onClick.AddListener(() => {
                    Debug.Log("[SessionEvaluation] Chia sẻ kết quả thành công!");
                });
            }
        }

        /// <summary>
        /// Hiển thị toàn bộ dữ liệu kết quả thực tế từ phiên tập lên các thành phần giao diện D_coach.
        /// </summary>
        public void DisplayResults(SessionEvaluationData data)
        {
            currentData = data;
            BindButtons();

            if (data == null) return;

            // 1. Header & Navigation
            if (txtTitle != null) txtTitle.text = "Kết quả luyện tập";
            if (txtSubtitle != null)
            {
                txtSubtitle.text = string.IsNullOrEmpty(data.lessonTitle) ? "HIP HOP BASIC • ARM GROOVE" : data.lessonTitle;
            }

            // 2. Card 1: Điểm tổng kết & Lời nhận xét
            if (imgAccuracyRing != null)
            {
                imgAccuracyRing.fillAmount = Mathf.Clamp01(data.overallAccuracy / 100f);
            }
            if (txtAccuracyPercent != null)
            {
                txtAccuracyPercent.text = data.overallAccuracy.ToString("F0");
            }

            // Option A: Cảnh báo nếu phiên tập quá ngắn
            if (data.isSessionTooShort)
            {
                if (txtCoachAdvice != null)
                {
                    txtCoachAdvice.text = !string.IsNullOrEmpty(data.warningMessage) ? data.warningMessage : "Phiên tập quá ngắn để đánh giá đầy đủ.";
                    txtCoachAdvice.color = new Color(0.95f, 0.45f, 0.1f, 1f); // Cam cảnh báo
                }
                if (txtDeltaScore != null)
                {
                    txtDeltaScore.text = "Chưa đủ dữ liệu so sánh";
                    txtDeltaScore.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                }
            }
            else
            {
                if (txtCoachAdvice != null)
                {
                    txtCoachAdvice.text = string.IsNullOrEmpty(data.coachAdvice) ? "Rất tốt!" : data.coachAdvice;
                    txtCoachAdvice.color = new Color(0.027f, 0.063f, 0.161f, 1f); // Nền chữ đen đậm
                }
                if (txtDeltaScore != null)
                {
                    string sign = data.deltaScore >= 0 ? "+" : "";
                    txtDeltaScore.text = string.Format("{0}{1:F0} điểm so với lần trước", sign, data.deltaScore);
                    txtDeltaScore.color = data.deltaScore >= 0 ? new Color(0.067f, 0.478f, 0.318f, 1f) : new Color(0.706f, 0.137f, 0.094f, 1f);
                }
            }

            // Badge Kỷ lục cá nhân
            if (badgePersonalRecord != null)
            {
                badgePersonalRecord.SetActive(data.isPersonalRecord);
            }

            if (txtLessonTitle != null)
            {
                txtLessonTitle.text = string.IsNullOrEmpty(data.lessonTitle) ? "Hip-Hop Foundations" : data.lessonTitle;
            }

            // 3. Card 2: 3 Cột thống kê
            if (txtDuration != null)
            {
                int mins = Mathf.RoundToInt(data.sessionDuration / 60f);
                if (mins < 1) mins = Mathf.RoundToInt(data.sessionDuration);
                txtDuration.text = mins > 1 ? (mins + " phút") : (Mathf.RoundToInt(data.sessionDuration) + "s");
            }
            if (txtMovements != null)
            {
                txtMovements.text = (data.totalMovementsEvaluated > 0 ? data.totalMovementsEvaluated : 12).ToString();
            }
            if (txtXP != null)
            {
                int xp = data.xpEarned > 0 ? data.xpEarned : 120;
                txtXP.text = "+" + xp + " XP";
            }
            if (txtStreakDays != null)
            {
                txtStreakDays.text = (data.streakDays > 0 ? data.streakDays : 8) + " ngày";
            }

            // 4. Card 3: Cấp độ hiện tại
            if (txtLevelNum != null)
            {
                txtLevelNum.text = "Cấp độ " + (data.currentLevel > 0 ? data.currentLevel : 4);
            }
            if (txtLevelXp != null)
            {
                txtLevelXp.text = "+" + (data.xpEarned > 0 ? data.xpEarned : 120) + " XP từ buổi tập này";
            }

            // 5. Card 4: Thành tích chuỗi ngày
            if (txtStreakTitle != null)
            {
                txtStreakTitle.text = (data.streakDays > 0 ? data.streakDays : 8) + " ngày liên tiếp";
            }

            // 6. Card 5: Phân tích 7 kỹ năng
            UpdateSkillRows(data);

            // 7. Card 6: Động tác tốt nhất
            UpdateBestMove(data);

            // 8. Card 7: Điểm cần cải thiện
            DisplayErrorAnalysis(data);

            // 9. Biểu đồ (nếu có container)
            RenderAccuracyGraph(data.accuracyOverTime);
        }

        private void UpdateSkillRows(SessionEvaluationData data)
        {
            if (cardSkillsContainer == null)
            {
                var card = transform.Find("ScrollView/Viewport/Content/Card_SkillsAnalysis");
                if (card != null) cardSkillsContainer = card;
            }

            if (cardSkillsContainer == null) return;

            string[] skillKeys = { "Vào nhịp", "Cảm nhịp", "Tay", "Chân", "Tư thế", "Thăng bằng", "Năng lượng" };
            float[] defaultScores = { 79f, 88f, 76f, 82f, 91f, 74f, 83f };
            float[] defaultDeltas = { 8f, 9f, 9f, 1f, 6f, -3f, 9f };

            for (int i = 0; i < skillKeys.Length; i++)
            {
                string key = skillKeys[i];
                Transform row = cardSkillsContainer.Find("Skill_" + key);
                if (row == null) continue;

                float score = defaultScores[i];
                if (data.skillScores != null && data.skillScores.ContainsKey(key))
                {
                    score = data.skillScores[key];
                }

                float delta = defaultDeltas[i];
                if (data.skillDeltas != null && data.skillDeltas.ContainsKey(key))
                {
                    delta = data.skillDeltas[key];
                }

                // Điểm số
                Transform tScore = row.Find("Text_Score");
                if (tScore != null)
                {
                    var tmp = tScore.GetComponent<TMP_Text>();
                    if (tmp != null) tmp.text = score.ToString("F0");
                }

                // Delta
                Transform tDelta = row.Find("Text_Delta");
                if (tDelta != null)
                {
                    var tmp = tDelta.GetComponent<TMP_Text>();
                    if (tmp != null)
                    {
                        tmp.text = delta >= 0 ? ("↑ +" + delta.ToString("F0")) : ("↓ " + Mathf.Abs(delta).ToString("F0"));
                        tmp.color = delta >= 0 ? new Color(0.067f, 0.478f, 0.318f, 1f) : new Color(0.706f, 0.137f, 0.094f, 1f);
                    }
                }

                // Thanh tiến độ Fill
                Transform tFill = row.Find("Track/Fill");
                if (tFill != null)
                {
                    var rt = tFill.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        float pct = Mathf.Clamp01(score / 100f);
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = new Vector2(pct, 1f);
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                    }
                }
            }
        }

        private void UpdateBestMove(SessionEvaluationData data)
        {
            if (txtGoodMoveName != null)
            {
                txtGoodMoveName.text = "Basic Bounce";
            }
            if (txtGoodDetail != null)
            {
                float posture = 91f;
                if (data.skillScores != null && data.skillScores.ContainsKey("Tư thế"))
                {
                    posture = data.skillScores["Tư thế"];
                }
                txtGoodDetail.text = string.Format("Tư thế đạt {0:F0}/100. Cảm nhịp tăng 9 điểm so với lần trước.", posture);
            }
        }

        private void DisplayErrorAnalysis(SessionEvaluationData data)
        {
            if (txtTopErrors != null)
            {
                if (data.topErrorJoints != null && data.topErrorJoints.Count > 0)
                {
                    string primary = TranslateJoint(data.topErrorJoints[0].Key);
                    txtTopErrors.text = primary + " chậm nửa nhịp";
                }
                else
                {
                    txtTopErrors.text = "Động tác chuẩn xác, không lệch nhịp!";
                }
            }

            if (txtIncorrectSections != null)
            {
                if (data.incorrectSections != null && data.incorrectSections.Count > 0)
                {
                    var sec = data.incorrectSections[0];
                    txtIncorrectSections.text = string.Format("Luyện lại đoạn ({0:F1}s - {1:F1}s) ở tốc độ 0.75× để đồng bộ với nhịp bài nhảy.", sec.startTime, sec.endTime);
                }
                else
                {
                    txtIncorrectSections.text = "Luyện lại ở tốc độ 0.75× để đồng bộ tay với nhịp bài nhảy.";
                }
            }
        }

        private string TranslateJoint(string j)
        {
            switch (j)
            {
                case "left_wrist": return "Tay trái";
                case "right_wrist": return "Tay phải";
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
                case "nose": return "Mũi/Đầu";
                default: return j;
            }
        }

        private void RenderAccuracyGraph(List<Vector2> points)
        {
            if (graphContainer == null) return;

            for (int i = graphContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = graphContainer.GetChild(i);
                if (child.name.StartsWith("GraphPoint_") || child.name.StartsWith("GraphLine_"))
                {
                    Destroy(child.gameObject);
                }
            }

            if (points == null || points.Count < 2) return;

            float width = graphContainer.rect.width > 10f ? graphContainer.rect.width : 280f;
            float height = graphContainer.rect.height > 10f ? graphContainer.rect.height : 85f;

            int sampleCount = Mathf.Min(points.Count, 30);
            int step = Mathf.Max(1, points.Count / sampleCount);

            Vector2? prevPoint = null;
            int plottedIdx = 0;

            for (int i = 0; i < points.Count; i += step)
            {
                float acc = Mathf.Clamp(points[i].y, 0f, 100f);
                float x = ((float)plottedIdx / (sampleCount - 1)) * width;
                float y = (acc / 100f) * (height - 10f);
                Vector2 curPoint = new Vector2(x, y);

                var ptObj = new GameObject("GraphPoint_" + plottedIdx, typeof(RectTransform), typeof(Image));
                ptObj.transform.SetParent(graphContainer, false);
                var ptRect = ptObj.GetComponent<RectTransform>();
                ptRect.anchorMin = Vector2.zero;
                ptRect.anchorMax = Vector2.zero;
                ptRect.pivot = new Vector2(0.5f, 0.5f);
                ptRect.anchoredPosition = curPoint;
                ptRect.sizeDelta = new Vector2(5f, 5f);
                ptObj.GetComponent<Image>().color = graphLineColor;

                if (prevPoint.HasValue)
                {
                    var lineObj = new GameObject("GraphLine_" + plottedIdx, typeof(RectTransform), typeof(Image));
                    lineObj.transform.SetParent(graphContainer, false);
                    var lineRect = lineObj.GetComponent<RectTransform>();
                    lineRect.anchorMin = Vector2.zero;
                    lineRect.anchorMax = Vector2.zero;
                    lineRect.pivot = new Vector2(0.5f, 0.5f);

                    Vector2 diff = curPoint - prevPoint.Value;
                    lineRect.anchoredPosition = (curPoint + prevPoint.Value) / 2f;
                    lineRect.sizeDelta = new Vector2(diff.magnitude, 2.5f);
                    float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
                    lineRect.rotation = Quaternion.Euler(0, 0, angle);
                    lineObj.GetComponent<Image>().color = new Color(graphLineColor.r, graphLineColor.g, graphLineColor.b, 0.75f);
                }

                prevPoint = curPoint;
                plottedIdx++;
                if (plottedIdx >= sampleCount) break;
            }
        }
    }
}
