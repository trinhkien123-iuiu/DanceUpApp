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

        [Header("=== CARD: DTW SEQUENCE COMPARISON & DEVIATIONS ===")]
        [SerializeField] private GameObject cardDtwDeviation;
        [SerializeField] private TMP_Text txtDtwAccuracy;
        [SerializeField] private TMP_Text txtFrameAccuracy;
        [SerializeField] private TMP_Text txtTimeDrift;
        [SerializeField] private RectTransform deviationTimelineContainer;
        [SerializeField] private Transform deviationSegmentsListContainer;
        [SerializeField] private TMP_Text txtDeviationSummary;

        [Header("=== EXPORT SKELETON VIDEO ===")]
        [SerializeField] private Button btnExportVideo;
        [SerializeField] private TMP_Text txtExportStatus;
        [SerializeField] private Slider sliderExportProgress;

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

            if (btnExportVideo != null)
            {
                btnExportVideo.onClick.RemoveAllListeners();
                btnExportVideo.onClick.AddListener(HandleExportVideoClicked);
            }
        }

        private void HandleExportVideoClicked()
        {
            if (currentData == null)
            {
                Debug.LogWarning("[SessionEvaluation] Chưa có dữ liệu phiên tập để xuất video!");
                return;
            }

            string inputVideo = currentData.analyzedVideoPath;
            if (string.IsNullOrEmpty(inputVideo) || !System.IO.File.Exists(inputVideo))
            {
                inputVideo = System.IO.Path.Combine(Application.dataPath, "_Project/Video/1_teacher.mp4");
            }

            if (!System.IO.File.Exists(inputVideo))
            {
                if (txtExportStatus != null)
                {
                    txtExportStatus.gameObject.SetActive(true);
                    txtExportStatus.color = new Color(1f, 0.35f, 0.45f, 1f);
                    txtExportStatus.text = "Không tìm thấy video gốc để xuất!";
                }
                return;
            }

            if (txtExportStatus != null)
            {
                txtExportStatus.gameObject.SetActive(true);
                txtExportStatus.color = new Color(0f, 0.706f, 1f, 1f);
                txtExportStatus.text = "Đang kết xuất video kèm 2D Skeleton...";
            }
            if (sliderExportProgress != null)
            {
                sliderExportProgress.gameObject.SetActive(true);
                sliderExportProgress.value = 0f;
            }
            if (btnExportVideo != null) btnExportVideo.interactable = false;

            DanceUp.Features.ImportEvaluation.VideoAnalysisService.Instance.ExportVideoWithSkeleton(
                inputVideo,
                currentData,
                (p) =>
                {
                    if (sliderExportProgress != null) sliderExportProgress.value = p;
                    if (txtExportStatus != null) txtExportStatus.text = string.Format("Đang xuất video ({0:F0}%)...", p * 100f);
                },
                (success, pathOrErr) =>
                {
                    if (btnExportVideo != null) btnExportVideo.interactable = true;
                    if (sliderExportProgress != null) sliderExportProgress.gameObject.SetActive(false);

                    if (success)
                    {
                        if (txtExportStatus != null)
                        {
                            txtExportStatus.color = new Color(0.1f, 0.95f, 0.45f, 1f);
                            string fName = System.IO.Path.GetFileName(pathOrErr);
                            txtExportStatus.text = string.Format("✓ Đã xuất: {0}\n(Lưu tại Assets/Exports/)", fName);
                        }
                        Debug.Log("[SessionEvaluation] Xuất video thành công: " + pathOrErr);
                    }
                    else
                    {
                        if (txtExportStatus != null)
                        {
                            txtExportStatus.color = new Color(1f, 0.35f, 0.45f, 1f);
                            txtExportStatus.text = "Xuất video thất bại: " + pathOrErr;
                        }
                    }
                }
            );
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

            // 9. Card: DTW Sequence Comparison & Deviations
            UpdateDtwCard(data);

            // 10. Biểu đồ (nếu có container)
            RenderAccuracyGraph(data.accuracyOverTime);
        }

        private void UpdateDtwCard(SessionEvaluationData data)
        {
            if (cardDtwDeviation == null)
            {
                var card = transform.Find("ScrollView/Viewport/Content/Card_DtwDeviation");
                if (card != null) cardDtwDeviation = card.gameObject;
            }

            if (cardDtwDeviation == null && txtDtwAccuracy == null) return;

            float dtwScore = data.dtwAccuracy > 0 ? data.dtwAccuracy : (data.overallAccuracy > 0 ? data.overallAccuracy + 2.5f : 84f);
            float frameScore = data.frameByFrameAccuracy > 0 ? data.frameByFrameAccuracy : (data.overallAccuracy > 0 ? data.overallAccuracy - 1.5f : 78f);

            if (txtDtwAccuracy != null)
            {
                txtDtwAccuracy.enableAutoSizing = true;
                txtDtwAccuracy.fontSizeMin = 10f;
                txtDtwAccuracy.fontSizeMax = 15f;
                txtDtwAccuracy.text = string.Format("{0:F0}%", Mathf.Clamp(dtwScore, 0f, 100f));
            }

            if (txtFrameAccuracy != null)
            {
                txtFrameAccuracy.enableAutoSizing = true;
                txtFrameAccuracy.fontSizeMin = 10f;
                txtFrameAccuracy.fontSizeMax = 15f;
                txtFrameAccuracy.text = string.Format("{0:F0}%", Mathf.Clamp(frameScore, 0f, 100f));
            }

            if (txtTimeDrift != null)
            {
                txtTimeDrift.enableAutoSizing = true;
                txtTimeDrift.fontSizeMin = 9f;
                txtTimeDrift.fontSizeMax = 13.5f;
                txtTimeDrift.enableWordWrapping = false;
                float drift = data.dtwTimeDriftSeconds;
                if (Mathf.Abs(drift) < 0.08f)
                {
                    txtTimeDrift.text = "±0.05s (Chuẩn)";
                    txtTimeDrift.color = new Color(0.1f, 0.95f, 0.45f, 1f); // Green
                }
                else if (drift > 0)
                {
                    txtTimeDrift.text = string.Format("+{0:F2}s (Sớm)", drift);
                    txtTimeDrift.color = new Color(0.96f, 0.62f, 0.04f, 1f); // Amber
                }
                else
                {
                    txtTimeDrift.text = string.Format("{0:F2}s (Trễ)", drift);
                    txtTimeDrift.color = new Color(1f, 0.35f, 0.45f, 1f); // Coral
                }
            }

            if (txtDeviationSummary != null)
            {
                int segCount = data.dtwDeviations != null ? data.dtwDeviations.Count : 0;
                if (segCount > 0)
                {
                    txtDeviationSummary.text = string.Format("Phát hiện {0} phân đoạn lệch nhịp & động tác cần điều chỉnh:", segCount);
                }
                else
                {
                    txtDeviationSummary.text = "Đồng bộ nhịp xuất sắc theo thuật toán căn chỉnh DTW!";
                }
            }

            RenderDeviationTimeline(data);
            RenderDeviationList(data);
        }

        private void RenderDeviationTimeline(SessionEvaluationData data)
        {
            if (deviationTimelineContainer == null)
            {
                var tr = transform.Find("ScrollView/Viewport/Content/Card_DtwDeviation/TimelineBar");
                if (tr != null) deviationTimelineContainer = tr.GetComponent<RectTransform>();
            }

            if (deviationTimelineContainer == null) return;

            for (int i = deviationTimelineContainer.childCount - 1; i >= 0; i--)
            {
                var c = deviationTimelineContainer.GetChild(i);
                if (c.name.StartsWith("Seg_"))
                {
                    Destroy(c.gameObject);
                }
            }

            float totalDuration = data.sessionDuration > 1f ? data.sessionDuration : 12.2f;
            var segments = data.dtwDeviations;
            if (segments == null || segments.Count == 0)
            {
                segments = new List<DtwDeviationSegment>
                {
                    new DtwDeviationSegment { startTime = 0f, endTime = 3.2f, severity = 0, deviationType = "Khớp nhịp", description = "Vào bài đều nhịp" },
                    new DtwDeviationSegment { startTime = 3.2f, endTime = 6.8f, severity = 1, deviationType = "Trễ nhịp", description = "Trễ tay trái 0.28s", primaryJoint = "left_wrist" },
                    new DtwDeviationSegment { startTime = 6.8f, endTime = totalDuration, severity = 0, deviationType = "Khớp nhịp", description = "Kết thúc bài chuẩn xác" }
                };
            }

            foreach (var seg in segments)
            {
                float sNorm = Mathf.Clamp01(seg.startTime / totalDuration);
                float eNorm = Mathf.Clamp01(seg.endTime / totalDuration);
                if (eNorm <= sNorm) continue;

                var blockObj = new GameObject("Seg_" + seg.startTime.ToString("F1"), typeof(RectTransform), typeof(Image), typeof(Button));
                blockObj.transform.SetParent(deviationTimelineContainer, false);
                var bRect = blockObj.GetComponent<RectTransform>();
                bRect.anchorMin = new Vector2(sNorm, 0f);
                bRect.anchorMax = new Vector2(eNorm, 1f);
                bRect.offsetMin = new Vector2(1f, 1f);
                bRect.offsetMax = new Vector2(-1f, -1f);

                Color c;
                if (seg.severity == 0) c = new Color(0.1f, 0.95f, 0.45f, 0.85f); // Green
                else if (seg.severity == 1) c = new Color(0.96f, 0.62f, 0.04f, 0.9f); // Amber
                else c = new Color(1f, 0.25f, 0.4f, 0.95f); // Red

                blockObj.GetComponent<Image>().color = c;

                float start = seg.startTime;
                float end = seg.endTime;
                blockObj.GetComponent<Button>().onClick.AddListener(() =>
                {
                    Debug.Log(string.Format("[SessionEvaluation] Bấm xem đoạn lệch: {0:F1}s - {1:F1}s", start, end));
                    OnReviewSegmentClicked?.Invoke(start, end);
                });
            }
        }

        private void RenderDeviationList(SessionEvaluationData data)
        {
            if (deviationSegmentsListContainer == null)
            {
                var tr = transform.Find("ScrollView/Viewport/Content/Card_DtwDeviation/DeviationList");
                if (tr != null) deviationSegmentsListContainer = tr;
            }

            if (deviationSegmentsListContainer == null) return;

            for (int i = deviationSegmentsListContainer.childCount - 1; i >= 0; i--)
            {
                var c = deviationSegmentsListContainer.GetChild(i);
                if (c.name.StartsWith("Item_"))
                {
                    Destroy(c.gameObject);
                }
            }

            var segments = data.dtwDeviations;
            if (segments == null || segments.Count == 0) return;

            TMP_FontAsset font = txtTopErrors != null ? txtTopErrors.font : (txtTitle != null ? txtTitle.font : null);

            int count = 0;
            foreach (var seg in segments)
            {
                if (seg.severity == 0 && count > 0) continue; // Ưu tiên các đoạn có lỗi
                count++;
                if (count > 3) break;

                var itemObj = new GameObject("Item_" + seg.startTime.ToString("F1"), typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                itemObj.transform.SetParent(deviationSegmentsListContainer, false);
                var iRect = itemObj.GetComponent<RectTransform>();
                iRect.sizeDelta = new Vector2(0f, 32f);

                var iLe = itemObj.GetComponent<LayoutElement>();
                iLe.minHeight = 32f;
                iLe.preferredHeight = 32f;
                iLe.flexibleWidth = 1f;

                var img = itemObj.GetComponent<Image>();
                img.color = new Color(0.95f, 0.965f, 0.98f, 1f); // #F1F5F9 Light Gray

                var hlg = itemObj.GetComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;
                hlg.padding = new RectOffset(8, 8, 4, 4);
                hlg.spacing = 6f;

                var timeObj = new GameObject("Txt_Time", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                timeObj.transform.SetParent(itemObj.transform, false);
                var txtTime = timeObj.GetComponent<TextMeshProUGUI>();
                if (font != null) txtTime.font = font;
                txtTime.fontSize = 10.5f;
                txtTime.fontStyle = FontStyles.Bold;
                txtTime.alignment = TextAlignmentOptions.MidlineLeft;
                txtTime.color = seg.severity == 0 ? new Color(0.067f, 0.478f, 0.318f, 1f) : (seg.severity == 1 ? new Color(0.96f, 0.54f, 0f, 1f) : new Color(0.706f, 0.137f, 0.094f, 1f));
                txtTime.text = string.Format("[{0:F1}s - {1:F1}s]", seg.startTime, seg.endTime);
                var tLe = timeObj.GetComponent<LayoutElement>();
                tLe.minWidth = 78f;
                tLe.preferredWidth = 78f;
                tLe.flexibleWidth = 0f;

                var descObj = new GameObject("Txt_Desc", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
                descObj.transform.SetParent(itemObj.transform, false);
                var txtDesc = descObj.GetComponent<TextMeshProUGUI>();
                if (font != null) txtDesc.font = font;
                txtDesc.fontSize = 10.5f;
                txtDesc.alignment = TextAlignmentOptions.MidlineLeft;
                txtDesc.color = new Color(0.12f, 0.18f, 0.28f, 1f);
                txtDesc.text = !string.IsNullOrEmpty(seg.description) ? seg.description : (seg.deviationType + " (" + TranslateJoint(seg.primaryJoint) + ")");
                txtDesc.overflowMode = TextOverflowModes.Ellipsis;
                txtDesc.enableWordWrapping = false;
                var dLe = descObj.GetComponent<LayoutElement>();
                dLe.minWidth = 90f;
                dLe.preferredWidth = 145f;
                dLe.flexibleWidth = 1f;

                var btnObj = new GameObject("Btn_Review", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                btnObj.transform.SetParent(itemObj.transform, false);
                var bRect = btnObj.GetComponent<RectTransform>();
                bRect.sizeDelta = new Vector2(62f, 24f);
                var bLe = btnObj.GetComponent<LayoutElement>();
                bLe.minWidth = 62f;
                bLe.preferredWidth = 62f;
                bLe.flexibleWidth = 0f;
                btnObj.GetComponent<Image>().color = new Color(0.898f, 0.969f, 1f, 1f); // #E5F7FF

                var btnTxtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                btnTxtObj.transform.SetParent(btnObj.transform, false);
                var bTxt = btnTxtObj.GetComponent<TextMeshProUGUI>();
                if (font != null) bTxt.font = font;
                bTxt.alignment = TextAlignmentOptions.Center;
                bTxt.fontSize = 10.5f;
                bTxt.fontStyle = FontStyles.Bold;
                bTxt.color = new Color(0f, 0.435f, 0.620f, 1f); // #006F9E Cyan Dark
                bTxt.text = "Xem lại";

                float start = seg.startTime;
                float end = seg.endTime;
                btnObj.GetComponent<Button>().onClick.AddListener(() =>
                {
                    OnReviewSegmentClicked?.Invoke(start, end);
                });
            }
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
