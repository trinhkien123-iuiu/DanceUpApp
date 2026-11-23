#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DanceUp.Features.Result;

namespace DanceUp.Editor
{
    public static class SessionEvaluationPrefabBuilder
    {
        private const string PrefabDir = "Assets/_Project/Prefabs/UI";
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/SessionEvaluationScreen.prefab";
        private const string ResourcesPath = "Assets/Resources/SessionEvaluationScreen.prefab";

        [MenuItem("Tools/DanceUp/Build SessionEvaluationScreen Prefab")]
        // [InitializeOnLoadMethod] – đã tắt: tự dựng lại prefab mỗi lần compile làm chậm editor và tạo thay đổi rác trong git. Chạy tay qua menu khi cần.
        public static void GeneratePrefab()
        {
            // Kiểm tra nếu prefab đã tồn tại và hợp lệ thì không cần tạo lại liên tục
            if (File.Exists(PrefabPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (existing != null && existing.GetComponent<SessionEvaluationController>() != null)
                {
                    var ctrl = existing.GetComponent<SessionEvaluationController>();
                    var so = new SerializedObject(ctrl);
                    if (so.FindProperty("btnRetry").objectReferenceValue != null)
                    {
                        return; // Đã hoàn chỉnh và hợp lệ!
                    }
                }
            }

            BuildAndSave();
        }

        public static void BuildAndSave()
        {
            if (!Directory.Exists(PrefabDir)) Directory.CreateDirectory(PrefabDir);
            if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");

            // Tải font & sprites chuẩn của dự án
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/SegoeUI-Bold SDF.asset");
            Sprite roundedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Common/UI_Rounded.png");
            Sprite ringTrackSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Profile/score_ring_track.png");
            Sprite ringBlueSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Profile/score_ring_blue.png");

            // 1. Root GameObject
            GameObject rootGo = new GameObject("SessionEvaluationScreen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(SessionEvaluationController));
            var rootRt = rootGo.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootGo.GetComponent<Image>().color = new Color(0.031f, 0.078f, 0.122f, 1f); // #08141F Navy
            var controller = rootGo.GetComponent<SessionEvaluationController>();

            // 2. Header
            GameObject headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(rootRt, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -40f);
            headerRt.sizeDelta = new Vector2(0f, 80f);

            // Nút Back
            GameObject backBtnGo = new GameObject("Button_Back", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            backBtnGo.transform.SetParent(headerRt, false);
            var backBtnRt = backBtnGo.GetComponent<RectTransform>();
            backBtnRt.anchorMin = new Vector2(0f, 0.5f);
            backBtnRt.anchorMax = new Vector2(0f, 0.5f);
            backBtnRt.pivot = new Vector2(0.5f, 0.5f);
            backBtnRt.anchoredPosition = new Vector2(30f, 0f);
            backBtnRt.sizeDelta = new Vector2(36f, 36f);
            var backImg = backBtnGo.GetComponent<Image>();
            if (roundedSprite != null) { backImg.sprite = roundedSprite; backImg.type = Image.Type.Sliced; }
            backImg.color = new Color(1f, 1f, 1f, 0.1f);
            var btnBack = backBtnGo.GetComponent<Button>();

            // Text mũi tên Back
            GameObject backTxtGo = new GameObject("Text_BackArrow", typeof(RectTransform), typeof(TextMeshProUGUI));
            backTxtGo.transform.SetParent(backBtnRt, false);
            var backTxtTmp = backTxtGo.GetComponent<TextMeshProUGUI>();
            if (font != null) backTxtTmp.font = font;
            backTxtTmp.text = "←";
            backTxtTmp.fontSize = 20;
            backTxtTmp.fontStyle = FontStyles.Bold;
            backTxtTmp.color = Color.white;
            backTxtTmp.alignment = TextAlignmentOptions.Center;
            var backTxtRt = backTxtGo.GetComponent<RectTransform>();
            backTxtRt.anchorMin = Vector2.zero;
            backTxtRt.anchorMax = Vector2.one;
            backTxtRt.offsetMin = Vector2.zero;
            backTxtRt.offsetMax = Vector2.zero;

            // Subtitle
            GameObject subGo = new GameObject("Text_Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(headerRt, false);
            var txtSubtitle = subGo.GetComponent<TextMeshProUGUI>();
            if (font != null) txtSubtitle.font = font;
            txtSubtitle.text = "HIP HOP BASIC • ARM GROOVE";
            txtSubtitle.fontSize = 11;
            txtSubtitle.fontStyle = FontStyles.Bold;
            txtSubtitle.color = new Color(0f, 0.76f, 1f, 1f);
            var subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0f, 0.5f);
            subRt.anchorMax = new Vector2(1f, 0.5f);
            subRt.pivot = new Vector2(0f, 0.5f);
            subRt.anchoredPosition = new Vector2(58f, 12f);
            subRt.sizeDelta = new Vector2(-70f, 20f);

            // Title
            GameObject titleGo = new GameObject("Text_Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(headerRt, false);
            var txtTitle = titleGo.GetComponent<TextMeshProUGUI>();
            if (font != null) txtTitle.font = font;
            txtTitle.text = "Kết quả luyện tập";
            txtTitle.fontSize = 22;
            txtTitle.fontStyle = FontStyles.Bold;
            txtTitle.color = Color.white;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(1f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(58f, -14f);
            titleRt.sizeDelta = new Vector2(-70f, 28f);

            // 3. Content Area
            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(rootRt, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0.5f, 1f);
            contentRt.anchorMax = new Vector2(0.5f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = new Vector2(0f, -95f);
            contentRt.sizeDelta = new Vector2(350f, 600f);

            // CARD 1: OVERALL
            GameObject card1Go = new GameObject("Card_Overall", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card1Go.transform.SetParent(contentRt, false);
            var card1Rt = card1Go.GetComponent<RectTransform>();
            card1Rt.anchorMin = new Vector2(0.5f, 1f);
            card1Rt.anchorMax = new Vector2(0.5f, 1f);
            card1Rt.pivot = new Vector2(0.5f, 1f);
            card1Rt.anchoredPosition = new Vector2(0f, -10f);
            card1Rt.sizeDelta = new Vector2(340f, 230f);
            var card1Img = card1Go.GetComponent<Image>();
            if (roundedSprite != null) { card1Img.sprite = roundedSprite; card1Img.type = Image.Type.Sliced; }
            card1Img.color = Color.white;

            // Ring Container
            GameObject ringContGo = new GameObject("RingContainer", typeof(RectTransform));
            ringContGo.transform.SetParent(card1Rt, false);
            var ringContRt = ringContGo.GetComponent<RectTransform>();
            ringContRt.anchorMin = new Vector2(0.5f, 1f);
            ringContRt.anchorMax = new Vector2(0.5f, 1f);
            ringContRt.pivot = new Vector2(0.5f, 0.5f);
            ringContRt.anchoredPosition = new Vector2(0f, -58f);
            ringContRt.sizeDelta = new Vector2(96f, 96f);

            // Ring Track
            GameObject ringTrackGo = new GameObject("Ring_Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringTrackGo.transform.SetParent(ringContRt, false);
            var ringTrackRt = ringTrackGo.GetComponent<RectTransform>();
            ringTrackRt.anchorMin = Vector2.zero; ringTrackRt.anchorMax = Vector2.one;
            ringTrackRt.offsetMin = Vector2.zero; ringTrackRt.offsetMax = Vector2.zero;
            var ringTrackImg = ringTrackGo.GetComponent<Image>();
            if (ringTrackSprite != null) ringTrackImg.sprite = ringTrackSprite;
            ringTrackImg.color = new Color(0.88f, 0.94f, 0.97f, 1f);

            // Ring Fill
            GameObject ringFillGo = new GameObject("Ring_Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringFillGo.transform.SetParent(ringContRt, false);
            var ringFillRt = ringFillGo.GetComponent<RectTransform>();
            ringFillRt.anchorMin = Vector2.zero; ringFillRt.anchorMax = Vector2.one;
            ringFillRt.offsetMin = Vector2.zero; ringFillRt.offsetMax = Vector2.zero;
            var imgAccuracyRing = ringFillGo.GetComponent<Image>();
            if (ringBlueSprite != null)
            {
                imgAccuracyRing.sprite = ringBlueSprite;
                imgAccuracyRing.type = Image.Type.Filled;
                imgAccuracyRing.fillMethod = Image.FillMethod.Radial360;
                imgAccuracyRing.fillOrigin = (int)Image.Origin360.Top;
                imgAccuracyRing.fillAmount = 0.87f;
            }
            imgAccuracyRing.color = new Color(0f, 0.76f, 1f, 1f);

            // Accuracy % Text
            GameObject accPctGo = new GameObject("Text_AccuracyPercent", typeof(RectTransform), typeof(TextMeshProUGUI));
            accPctGo.transform.SetParent(ringContRt, false);
            var txtAccuracyPercent = accPctGo.GetComponent<TextMeshProUGUI>();
            if (font != null) txtAccuracyPercent.font = font;
            txtAccuracyPercent.text = "87%";
            txtAccuracyPercent.fontSize = 24;
            txtAccuracyPercent.fontStyle = FontStyles.Bold;
            txtAccuracyPercent.color = new Color(0.03f, 0.08f, 0.12f, 1f);
            txtAccuracyPercent.alignment = TextAlignmentOptions.Center;
            var accPctRt = accPctGo.GetComponent<RectTransform>();
            accPctRt.anchorMin = new Vector2(0f, 0.5f); accPctRt.anchorMax = new Vector2(1f, 0.5f);
            accPctRt.anchoredPosition = new Vector2(0f, 5f); accPctRt.sizeDelta = new Vector2(0f, 32f);

            // Accuracy Label
            GameObject accLblGo = new GameObject("Text_AccuracyLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            accLblGo.transform.SetParent(ringContRt, false);
            var accLblTmp = accLblGo.GetComponent<TextMeshProUGUI>();
            if (font != null) accLblTmp.font = font;
            accLblTmp.text = "Độ chính xác";
            accLblTmp.fontSize = 9;
            accLblTmp.color = new Color(0.53f, 0.6f, 0.66f, 1f);
            accLblTmp.alignment = TextAlignmentOptions.Center;
            var accLblRt = accLblGo.GetComponent<RectTransform>();
            accLblRt.anchorMin = new Vector2(0f, 0.5f); accLblRt.anchorMax = new Vector2(1f, 0.5f);
            accLblRt.anchoredPosition = new Vector2(0f, -14f); accLblRt.sizeDelta = new Vector2(0f, 16f);

            // Bubble Coach
            GameObject bubbleGo = new GameObject("Bubble_Coach", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bubbleGo.transform.SetParent(card1Rt, false);
            var bubbleRt = bubbleGo.GetComponent<RectTransform>();
            bubbleRt.anchorMin = new Vector2(0.5f, 1f); bubbleRt.anchorMax = new Vector2(0.5f, 1f);
            bubbleRt.pivot = new Vector2(0.5f, 0.5f);
            bubbleRt.anchoredPosition = new Vector2(0f, -125f);
            bubbleRt.sizeDelta = new Vector2(308f, 34f);
            var bubbleImg = bubbleGo.GetComponent<Image>();
            if (roundedSprite != null) { bubbleImg.sprite = roundedSprite; bubbleImg.type = Image.Type.Sliced; }
            bubbleImg.color = new Color(0.92f, 0.97f, 1f, 1f);

            GameObject coachTxtGo = new GameObject("Text_CoachAdvice", typeof(RectTransform), typeof(TextMeshProUGUI));
            coachTxtGo.transform.SetParent(bubbleRt, false);
            var txtCoachAdvice = coachTxtGo.GetComponent<TextMeshProUGUI>();
            if (font != null) txtCoachAdvice.font = font;
            txtCoachAdvice.text = "Bạn đang tiến bộ rất tốt! 🎉";
            txtCoachAdvice.fontSize = 12;
            txtCoachAdvice.fontStyle = FontStyles.Bold;
            txtCoachAdvice.color = new Color(0.01f, 0.52f, 0.78f, 1f);
            txtCoachAdvice.alignment = TextAlignmentOptions.Center;
            var coachTxtRt = coachTxtGo.GetComponent<RectTransform>();
            coachTxtRt.anchorMin = Vector2.zero; coachTxtRt.anchorMax = Vector2.one;
            coachTxtRt.offsetMin = Vector2.zero; coachTxtRt.offsetMax = Vector2.zero;

            // Stats Row
            GameObject statsGo = new GameObject("StatsRow", typeof(RectTransform));
            statsGo.transform.SetParent(card1Rt, false);
            var statsRt = statsGo.GetComponent<RectTransform>();
            statsRt.anchorMin = new Vector2(0.5f, 0f); statsRt.anchorMax = new Vector2(0.5f, 0f);
            statsRt.pivot = new Vector2(0.5f, 0.5f);
            statsRt.anchoredPosition = new Vector2(0f, 34f);
            statsRt.sizeDelta = new Vector2(308f, 52f);

            // Duration Col
            GameObject colDur = new GameObject("Col_Duration", typeof(RectTransform));
            colDur.transform.SetParent(statsRt, false);
            var colDurRt = colDur.GetComponent<RectTransform>();
            colDurRt.anchorMin = new Vector2(0f, 0f); colDurRt.anchorMax = new Vector2(0.33f, 1f);
            colDurRt.offsetMin = Vector2.zero; colDurRt.offsetMax = Vector2.zero;
            var txtDuration = CreateStatColumn(colDurRt, "30s", "Thời gian", font, new Color(0.03f, 0.08f, 0.12f, 1f));

            // Movements Col
            GameObject colMov = new GameObject("Col_Movements", typeof(RectTransform));
            colMov.transform.SetParent(statsRt, false);
            var colMovRt = colMov.GetComponent<RectTransform>();
            colMovRt.anchorMin = new Vector2(0.33f, 0f); colMovRt.anchorMax = new Vector2(0.66f, 1f);
            colMovRt.offsetMin = Vector2.zero; colMovRt.offsetMax = Vector2.zero;
            var txtMovements = CreateStatColumn(colMovRt, "12", "Động tác", font, new Color(0.03f, 0.08f, 0.12f, 1f));

            // XP Col
            GameObject colXP = new GameObject("Col_XP", typeof(RectTransform));
            colXP.transform.SetParent(statsRt, false);
            var colXpRt = colXP.GetComponent<RectTransform>();
            colXpRt.anchorMin = new Vector2(0.66f, 0f); colXpRt.anchorMax = new Vector2(1f, 1f);
            colXpRt.offsetMin = Vector2.zero; colXpRt.offsetMax = Vector2.zero;
            var txtXP = CreateStatColumn(colXpRt, "+15 XP", "Điểm nhận", font, new Color(0f, 0.76f, 1f, 1f));

            // CARD 2: GRAPH
            GameObject card2Go = new GameObject("Card_Graph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card2Go.transform.SetParent(contentRt, false);
            var card2Rt = card2Go.GetComponent<RectTransform>();
            card2Rt.anchorMin = new Vector2(0.5f, 1f); card2Rt.anchorMax = new Vector2(0.5f, 1f);
            card2Rt.pivot = new Vector2(0.5f, 1f);
            card2Rt.anchoredPosition = new Vector2(0f, -255f);
            card2Rt.sizeDelta = new Vector2(340f, 165f);
            var card2Img = card2Go.GetComponent<Image>();
            if (roundedSprite != null) { card2Img.sprite = roundedSprite; card2Img.type = Image.Type.Sliced; }
            card2Img.color = Color.white;

            GameObject graphTitleGo = new GameObject("Text_GraphTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            graphTitleGo.transform.SetParent(card2Rt, false);
            var txtGraphTitle = graphTitleGo.GetComponent<TextMeshProUGUI>();
            if (font != null) txtGraphTitle.font = font;
            txtGraphTitle.text = "Biểu đồ chính xác";
            txtGraphTitle.fontSize = 14;
            txtGraphTitle.fontStyle = FontStyles.Bold;
            txtGraphTitle.color = new Color(0.03f, 0.08f, 0.12f, 1f);
            var graphTitleRt = graphTitleGo.GetComponent<RectTransform>();
            graphTitleRt.anchorMin = new Vector2(0f, 1f); graphTitleRt.anchorMax = new Vector2(1f, 1f);
            graphTitleRt.pivot = new Vector2(0f, 1f);
            graphTitleRt.anchoredPosition = new Vector2(20f, -14f);
            graphTitleRt.sizeDelta = new Vector2(-40f, 24f);

            GameObject graphContGo = new GameObject("GraphContainer", typeof(RectTransform));
            graphContGo.transform.SetParent(card2Rt, false);
            var graphContainer = graphContGo.GetComponent<RectTransform>();
            graphContainer.anchorMin = new Vector2(0.5f, 0.5f); graphContainer.anchorMax = new Vector2(0.5f, 0.5f);
            graphContainer.pivot = new Vector2(0.5f, 0.5f);
            graphContainer.anchoredPosition = new Vector2(0f, -5f);
            graphContainer.sizeDelta = new Vector2(300f, 80f);

            GameObject timelineGo = new GameObject("TimelineLabels", typeof(RectTransform), typeof(TextMeshProUGUI));
            timelineGo.transform.SetParent(card2Rt, false);
            var timelineTmp = timelineGo.GetComponent<TextMeshProUGUI>();
            if (font != null) timelineTmp.font = font;
            timelineTmp.text = "5s          10s          15s          20s          25s          30s";
            timelineTmp.fontSize = 10;
            timelineTmp.color = new Color(0.53f, 0.6f, 0.66f, 1f);
            timelineTmp.alignment = TextAlignmentOptions.Center;
            var timelineRt = timelineGo.GetComponent<RectTransform>();
            timelineRt.anchorMin = new Vector2(0.5f, 0f); timelineRt.anchorMax = new Vector2(0.5f, 0f);
            timelineRt.pivot = new Vector2(0.5f, 0.5f);
            timelineRt.anchoredPosition = new Vector2(0f, 14f);
            timelineRt.sizeDelta = new Vector2(300f, 18f);

            // CARD 3: ERRORS
            GameObject card3Go = new GameObject("Card_Errors", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card3Go.transform.SetParent(contentRt, false);
            var card3Rt = card3Go.GetComponent<RectTransform>();
            card3Rt.anchorMin = new Vector2(0.5f, 1f); card3Rt.anchorMax = new Vector2(0.5f, 1f);
            card3Rt.pivot = new Vector2(0.5f, 1f);
            card3Rt.anchoredPosition = new Vector2(0f, -435f);
            card3Rt.sizeDelta = new Vector2(340f, 115f);
            var card3Img = card3Go.GetComponent<Image>();
            if (roundedSprite != null) { card3Img.sprite = roundedSprite; card3Img.type = Image.Type.Sliced; }
            card3Img.color = Color.white;

            GameObject errHeaderGo = new GameObject("Text_ErrorsHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            errHeaderGo.transform.SetParent(card3Rt, false);
            var errHeaderTmp = errHeaderGo.GetComponent<TextMeshProUGUI>();
            if (font != null) errHeaderTmp.font = font;
            errHeaderTmp.text = "Phân tích động tác cần cải thiện";
            errHeaderTmp.fontSize = 13;
            errHeaderTmp.fontStyle = FontStyles.Bold;
            errHeaderTmp.color = new Color(0.03f, 0.08f, 0.12f, 1f);
            var errHeaderRt = errHeaderGo.GetComponent<RectTransform>();
            errHeaderRt.anchorMin = new Vector2(0f, 1f); errHeaderRt.anchorMax = new Vector2(1f, 1f);
            errHeaderRt.pivot = new Vector2(0f, 1f);
            errHeaderRt.anchoredPosition = new Vector2(20f, -14f);
            errHeaderRt.sizeDelta = new Vector2(-40f, 20f);

            GameObject errTopGo = new GameObject("Text_TopErrors", typeof(RectTransform), typeof(TextMeshProUGUI));
            errTopGo.transform.SetParent(card3Rt, false);
            var txtTopErrors = errTopGo.GetComponent<TextMeshProUGUI>();
            if (font != null) txtTopErrors.font = font;
            txtTopErrors.text = "• Cổ tay trái: 12 lần lỗi\n• Đầu gối phải: 5 lần lỗi";
            txtTopErrors.fontSize = 11;
            txtTopErrors.color = new Color(0.29f, 0.33f, 0.41f, 1f);
            var errTopRt = errTopGo.GetComponent<RectTransform>();
            errTopRt.anchorMin = new Vector2(0f, 0.5f); errTopRt.anchorMax = new Vector2(1f, 0.5f);
            errTopRt.pivot = new Vector2(0f, 0.5f);
            errTopRt.anchoredPosition = new Vector2(20f, 0f);
            errTopRt.sizeDelta = new Vector2(-40f, 32f);

            GameObject errSecGo = new GameObject("Text_IncorrectSections", typeof(RectTransform), typeof(TextMeshProUGUI));
            errSecGo.transform.SetParent(card3Rt, false);
            var txtIncorrectSections = errSecGo.GetComponent<TextMeshProUGUI>();
            if (font != null) txtIncorrectSections.font = font;
            txtIncorrectSections.text = "• 00:08 - 00:12: Biên độ tay chưa đạt";
            txtIncorrectSections.fontSize = 11;
            txtIncorrectSections.color = new Color(0.9f, 0.24f, 0.24f, 1f);
            var errSecRt = errSecGo.GetComponent<RectTransform>();
            errSecRt.anchorMin = new Vector2(0f, 0f); errSecRt.anchorMax = new Vector2(1f, 0f);
            errSecRt.pivot = new Vector2(0f, 0f);
            errSecRt.anchoredPosition = new Vector2(20f, 16f);
            errSecRt.sizeDelta = new Vector2(-40f, 20f);

            // 4. Bottom Bar
            GameObject bottomGo = new GameObject("BottomBar", typeof(RectTransform));
            bottomGo.transform.SetParent(rootRt, false);
            var bottomRt = bottomGo.GetComponent<RectTransform>();
            bottomRt.anchorMin = new Vector2(0.5f, 0f); bottomRt.anchorMax = new Vector2(0.5f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0.5f);
            bottomRt.anchoredPosition = new Vector2(0f, 35f);
            bottomRt.sizeDelta = new Vector2(340f, 55f);

            var btnRetry = CreateActionButton(bottomRt, "Button_Retry", "Luyện lại", new Vector2(50f, 0f), new Vector2(95f, 44f),
                                              new Color(1f, 1f, 1f, 0.12f), Color.white, roundedSprite, font);

            var btnSave = CreateActionButton(bottomRt, "Button_Save", "Lưu", new Vector2(0f, 0f), new Vector2(95f, 44f),
                                             new Color(1f, 1f, 1f, 0.12f), Color.white, roundedSprite, font, new Vector2(0.5f, 0.5f));

            var btnShare = CreateActionButton(bottomRt, "Button_Share", "Chia sẻ", new Vector2(-55f, 0f), new Vector2(105f, 44f),
                                              new Color(0f, 0.76f, 1f, 1f), new Color(0.03f, 0.08f, 0.12f, 1f), roundedSprite, font, new Vector2(1f, 0.5f));

            // Configure all Controller references
            controller.ConfigureReferences(
                btnBack, txtSubtitle, txtTitle,
                imgAccuracyRing, txtAccuracyPercent, txtCoachAdvice,
                txtDuration, txtMovements, txtXP,
                graphContainer, txtGraphTitle,
                txtTopErrors, txtIncorrectSections,
                btnRetry, btnSave, btnShare
            );

            // Save Prefab using Unity's official PrefabUtility
            PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            PrefabUtility.SaveAsPrefabAsset(rootGo, ResourcesPath);
            Debug.Log("[SessionEvaluationPrefabBuilder] Prefab đã được tạo và lưu thành công tại " + PrefabPath);

            Object.DestroyImmediate(rootGo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static TMP_Text CreateStatColumn(RectTransform parent, string val, string label, TMP_FontAsset font, Color valColor)
        {
            GameObject valGo = new GameObject("Text_Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            valGo.transform.SetParent(parent, false);
            var valTmp = valGo.GetComponent<TextMeshProUGUI>();
            if (font != null) valTmp.font = font;
            valTmp.text = val;
            valTmp.fontSize = 17;
            valTmp.fontStyle = FontStyles.Bold;
            valTmp.color = valColor;
            valTmp.alignment = TextAlignmentOptions.Center;
            var valRt = valGo.GetComponent<RectTransform>();
            valRt.anchorMin = new Vector2(0f, 0.5f); valRt.anchorMax = new Vector2(1f, 1f);
            valRt.offsetMin = Vector2.zero; valRt.offsetMax = Vector2.zero;

            GameObject lblGo = new GameObject("Text_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblGo.transform.SetParent(parent, false);
            var lblTmp = lblGo.GetComponent<TextMeshProUGUI>();
            if (font != null) lblTmp.font = font;
            lblTmp.text = label;
            lblTmp.fontSize = 11;
            lblTmp.color = new Color(0.53f, 0.6f, 0.66f, 1f);
            lblTmp.alignment = TextAlignmentOptions.Center;
            var lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0f); lblRt.anchorMax = new Vector2(1f, 0.5f);
            lblRt.offsetMin = Vector2.zero; lblRt.offsetMax = Vector2.zero;

            return valTmp;
        }

        private static Button CreateActionButton(RectTransform parent, string name, string text, Vector2 pos, Vector2 size, Color btnColor, Color txtColor, Sprite roundedSprite, TMP_FontAsset font, Vector2? anchor = null)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            Vector2 a = anchor.HasValue ? anchor.Value : new Vector2(0f, 0.5f);
            btnRt.anchorMin = a; btnRt.anchorMax = a;
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = pos;
            btnRt.sizeDelta = size;

            var img = btnGo.GetComponent<Image>();
            if (roundedSprite != null) { img.sprite = roundedSprite; img.type = Image.Type.Sliced; }
            img.color = btnColor;

            var btn = btnGo.GetComponent<Button>();

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnRt, false);
            var txtTmp = txtGo.GetComponent<TextMeshProUGUI>();
            if (font != null) txtTmp.font = font;
            txtTmp.text = text;
            txtTmp.fontSize = 13;
            txtTmp.fontStyle = FontStyles.Bold;
            txtTmp.color = txtColor;
            txtTmp.alignment = TextAlignmentOptions.Center;
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;

            return btn;
        }
    }
}
#endif
