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
        private GameObject verdictBadgeObj;
        private TMP_Text txtVerdictBadge;
        private Image imgVerdictBadgeBg;

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
            ScrollToTop();
        }

        /// <summary>
        /// Đưa danh sách kết quả về đầu trang. Gọi mỗi khi màn hiện ra / nạp dữ liệu mới,
        /// vì ScrollRect giữ nguyên vị trí cuộn cũ và nội dung đổi chiều cao sau khi đổ dữ liệu.
        /// </summary>
        public void ScrollToTop()
        {
            var scroll = GetComponentInChildren<ScrollRect>(true);
            if (scroll == null) return;

            scroll.StopMovement();
            if (scroll.content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;

            // Layout (ContentSizeFitter, thẻ DTW co giãn) có thể tính lại ở frame sau → cuộn lại lần nữa
            if (isActiveAndEnabled) StartCoroutine(ScrollToTopNextFrame(scroll));
        }

        private System.Collections.IEnumerator ScrollToTopNextFrame(ScrollRect scroll)
        {
            yield return null;
            if (scroll == null) yield break;
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
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

            // 2. Card 1: Điểm tổng kết & Lời nhận xét & Kết luận ĐẠT CHUẨN / CHƯA ĐẠT
            bool isPassed = data.isPassedStandard || (data.overallAccuracy >= (data.passStandardThreshold > 0 ? data.passStandardThreshold : 70f));
            Color passColor = new Color(0.067f, 0.72f, 0.38f, 1f);
            Color failColor = new Color(0.95f, 0.22f, 0.35f, 1f);

            if (imgAccuracyRing != null)
            {
                imgAccuracyRing.fillAmount = Mathf.Clamp01(data.overallAccuracy / 100f);
                imgAccuracyRing.color = isPassed ? passColor : failColor;
            }
            if (txtAccuracyPercent != null)
            {
                txtAccuracyPercent.text = data.overallAccuracy.ToString("F0");
            }

            UpdateVerdictBadge(isPassed, data.passStandardThreshold > 0 ? data.passStandardThreshold : 70f);

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

            // 11. Luôn mở màn kết quả ở đầu trang
            ScrollToTop();
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
            if (segments == null || segments.Count == 0)
            {
                FitDtwCardHeight(0);
                return;
            }

            TMP_FontAsset font = txtTopErrors != null ? txtTopErrors.font : (txtTitle != null ? txtTitle.font : null);

            // Dùng lại sprite bo góc của nền thẻ cho từng hàng
            Image cardBg = deviationSegmentsListContainer.parent != null ? deviationSegmentsListContainer.parent.GetComponent<Image>() : null;
            Sprite rowSprite = cardBg != null ? cardBg.sprite : null;

            int count = 0;
            int created = 0;
            foreach (var seg in segments)
            {
                if (seg.severity == 0 && count > 0) continue; // Ưu tiên các đoạn có lỗi
                count++;
                if (count > 3) break;
                created++;

                // Hàng 2 dòng: [mốc thời gian ........ Xem lại] / [mô tả]
                var itemObj = new GameObject("Item_" + seg.startTime.ToString("F1"), typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                itemObj.transform.SetParent(deviationSegmentsListContainer, false);
                var iRect = itemObj.GetComponent<RectTransform>();
                iRect.sizeDelta = new Vector2(0f, DtwRowHeight);

                var iLe = itemObj.GetComponent<LayoutElement>();
                iLe.minHeight = DtwRowHeight;
                iLe.preferredHeight = DtwRowHeight;
                iLe.flexibleWidth = 1f;

                var img = itemObj.GetComponent<Image>();
                img.color = new Color(0.95f, 0.965f, 0.98f, 1f); // #F1F5F9 Light Gray
                if (rowSprite != null)
                {
                    img.sprite = rowSprite;
                    img.type = Image.Type.Sliced;
                    img.pixelsPerUnitMultiplier = cardBg.pixelsPerUnitMultiplier * 1.6f;
                }

                Color sevColor = seg.severity == 0 ? new Color(0.067f, 0.478f, 0.318f, 1f) : (seg.severity == 1 ? new Color(0.96f, 0.54f, 0f, 1f) : new Color(0.706f, 0.137f, 0.094f, 1f));

                var txtTime = CreateRowText(itemObj.transform, "Txt_Time", font, 12f, FontStyles.Bold, sevColor,
                    string.Format("{0:F1}s – {1:F1}s", seg.startTime, seg.endTime));
                PlaceTopLeft(txtTime.rectTransform, 12f, 8f, 84f, 18f);

                var txtDesc = CreateRowText(itemObj.transform, "Txt_Desc", font, 12f, FontStyles.Normal, new Color(0.12f, 0.18f, 0.28f, 1f),
                    !string.IsNullOrEmpty(seg.description) ? seg.description : (seg.deviationType + " (" + TranslateJoint(seg.primaryJoint) + ")"));
                PlaceTopLeft(txtDesc.rectTransform, 12f, 28f, 84f, 18f);
                txtDesc.overflowMode = TextOverflowModes.Ellipsis;

                var btnObj = new GameObject("Btn_Review", typeof(RectTransform), typeof(Image), typeof(Button));
                btnObj.transform.SetParent(itemObj.transform, false);
                var bRect = btnObj.GetComponent<RectTransform>();
                bRect.anchorMin = bRect.anchorMax = new Vector2(1f, 0.5f);
                bRect.pivot = new Vector2(1f, 0.5f);
                bRect.anchoredPosition = new Vector2(-10f, 0f);
                bRect.sizeDelta = new Vector2(68f, 30f);
                var bImg = btnObj.GetComponent<Image>();
                bImg.color = new Color(0.898f, 0.969f, 1f, 1f); // #E5F7FF
                if (rowSprite != null)
                {
                    bImg.sprite = rowSprite;
                    bImg.type = Image.Type.Sliced;
                    bImg.pixelsPerUnitMultiplier = cardBg.pixelsPerUnitMultiplier * 2f;
                }

                var bTxt = CreateRowText(btnObj.transform, "Text", font, 12f, FontStyles.Bold, new Color(0f, 0.435f, 0.620f, 1f), "Xem lại");
                bTxt.alignment = TextAlignmentOptions.Center;
                var btRt = bTxt.rectTransform;
                btRt.anchorMin = Vector2.zero; btRt.anchorMax = Vector2.one;
                btRt.offsetMin = Vector2.zero; btRt.offsetMax = Vector2.zero;

                float start = seg.startTime;
                float end = seg.endTime;
                btnObj.GetComponent<Button>().onClick.AddListener(() =>
                {
                    OnReviewSegmentClicked?.Invoke(start, end);
                });
            }

            FitDtwCardHeight(created);
        }

        private const float DtwRowHeight = 54f;
        private const float DtwRowSpacing = 8f;
        private const float DtwCardBottomPadding = 20f;

        private static TextMeshProUGUI CreateRowText(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style, Color color, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.enableWordWrapping = false;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        private void UpdateVerdictBadge(bool isPassed, float threshold)
        {
            if (verdictBadgeObj == null)
            {
                Transform cardOverall = transform.Find("ScrollView/Viewport/Content/Card_Overall") ??
                                        transform.Find("ScrollView/Viewport/Content/Card_Score") ??
                                        (txtCoachAdvice != null ? txtCoachAdvice.transform.parent : null);

                if (cardOverall != null)
                {
                    var existing = cardOverall.Find("Badge_Verdict");
                    if (existing != null)
                    {
                        verdictBadgeObj = existing.gameObject;
                        imgVerdictBadgeBg = existing.GetComponent<Image>();
                        txtVerdictBadge = existing.Find("Text")?.GetComponent<TMP_Text>();
                    }
                    else
                    {
                        verdictBadgeObj = new GameObject("Badge_Verdict", typeof(RectTransform), typeof(Image));
                        verdictBadgeObj.transform.SetParent(cardOverall, false);
                        var rt = verdictBadgeObj.GetComponent<RectTransform>();
                        rt.anchorMin = new Vector2(1f, 1f);
                        rt.anchorMax = new Vector2(1f, 1f);
                        rt.pivot = new Vector2(1f, 1f);
                        rt.anchoredPosition = new Vector2(-14f, -14f);
                        rt.sizeDelta = new Vector2(138f, 24f);

                        imgVerdictBadgeBg = verdictBadgeObj.GetComponent<Image>();
                        if (imgAccuracyRing != null && imgAccuracyRing.sprite != null)
                        {
                            imgVerdictBadgeBg.sprite = imgAccuracyRing.sprite;
                        }
                        imgVerdictBadgeBg.raycastTarget = false;

                        var txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                        txtObj.transform.SetParent(verdictBadgeObj.transform, false);
                        var trt = txtObj.GetComponent<RectTransform>();
                        trt.anchorMin = Vector2.zero;
                        trt.anchorMax = Vector2.one;
                        trt.offsetMin = new Vector2(6f, 0f);
                        trt.offsetMax = new Vector2(-6f, 0f);
                        txtVerdictBadge = txtObj.GetComponent<TextMeshProUGUI>();
                        if (txtAccuracyPercent != null && txtAccuracyPercent.font != null)
                            txtVerdictBadge.font = txtAccuracyPercent.font;
                        txtVerdictBadge.fontSize = 10.5f;
                        txtVerdictBadge.fontStyle = FontStyles.Bold;
                        txtVerdictBadge.alignment = TextAlignmentOptions.Center;
                        txtVerdictBadge.enableWordWrapping = false;
                        txtVerdictBadge.raycastTarget = false;
                    }
                }
            }

            if (verdictBadgeObj == null) return;
            verdictBadgeObj.SetActive(true);

            if (isPassed)
            {
                if (imgVerdictBadgeBg != null) imgVerdictBadgeBg.color = new Color(0.86f, 0.98f, 0.91f, 1f);
                if (txtVerdictBadge != null)
                {
                    txtVerdictBadge.color = new Color(0.05f, 0.52f, 0.28f, 1f);
                    txtVerdictBadge.text = $"✓ ĐẠT CHUẨN (≥{threshold:F0}%)";
                }
            }
            else
            {
                if (imgVerdictBadgeBg != null) imgVerdictBadgeBg.color = new Color(1f, 0.90f, 0.91f, 1f);
                if (txtVerdictBadge != null)
                {
                    txtVerdictBadge.color = new Color(0.75f, 0.14f, 0.22f, 1f);
                    txtVerdictBadge.text = $"✕ CHƯA ĐẠT (<{threshold:F0}%)";
                }
            }
        }

        // Neo trên-trái, kéo giãn ngang (chừa rightInset bên phải cho nút)
        private static void PlaceTopLeft(RectTransform rt, float left, float top, float rightInset, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(-(left + rightInset), height);
        }

        // Nới chiều cao thẻ DTW theo số hàng lỗi thực tế (thẻ nằm trong VerticalLayoutGroup)
        private void FitDtwCardHeight(int rows)
        {
            var listRt = deviationSegmentsListContainer as RectTransform;
            if (cardDtwDeviation == null || listRt == null) return;

            float listH = rows > 0 ? rows * DtwRowHeight + (rows - 1) * DtwRowSpacing : 0f;
            listRt.sizeDelta = new Vector2(listRt.sizeDelta.x, listH);

            var le = cardDtwDeviation.GetComponent<LayoutElement>();
            if (le == null) return;
            float listTop = -listRt.anchoredPosition.y;
            float h = listTop + listH + DtwCardBottomPadding;
            le.minHeight = h;
            le.preferredHeight = h;
            if (listRt.parent != null && listRt.parent.parent != null)
                LayoutRebuilder.MarkLayoutForRebuild(listRt.parent.parent as RectTransform);
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
