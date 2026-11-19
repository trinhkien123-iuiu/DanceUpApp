using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DanceUp.Features.Result;

namespace DanceUp.Editor
{
    public static class BuildDCoachScreenUI
    {
        private static readonly Color ColorBgScreen = new Color(0.969f, 0.980f, 0.988f, 1f); // #F7FAFC
        private static readonly Color ColorHeader = new Color(0.051f, 0.106f, 0.165f, 1f);   // #0D1B2A
        private static readonly Color ColorCardBorder = new Color(0.847f, 0.886f, 0.922f, 1f); // #D8E2EB
        private static readonly Color ColorTextDark = new Color(0.027f, 0.063f, 0.161f, 1f);   // #071029
        private static readonly Color ColorTextMuted = new Color(0.337f, 0.396f, 0.471f, 1f);  // #566578
        private static readonly Color ColorCyan = new Color(0f, 0.706f, 1f, 1f);               // #00B4FF
        private static readonly Color ColorCyanLight = new Color(0.898f, 0.969f, 1f, 1f);       // #E5F7FF
        private static readonly Color ColorCyanDark = new Color(0f, 0.435f, 0.620f, 1f);        // #006F9E
        private static readonly Color ColorGreen = new Color(0.067f, 0.478f, 0.318f, 1f);      // #117A51
        private static readonly Color ColorRed = new Color(0.706f, 0.137f, 0.094f, 1f);        // #B42318
        private static readonly Color ColorOrange = new Color(1f, 0.541f, 0f, 1f);              // #FF8A00
        private static readonly Color ColorBtnBorder = new Color(0.510f, 0.576f, 0.651f, 1f);  // #8293A6

        [MenuItem("DanceUp/Build D_coach Screen UI")]
        public static void BuildUI()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                Debug.LogError("[BuildDCoachScreenUI] Không tìm thấy Canvas trong Scene!");
                return;
            }

            Transform dCoachTrans = canvas.transform.Find("D_coach");
            GameObject dCoachObj;
            if (dCoachTrans != null)
            {
                dCoachObj = dCoachTrans.gameObject;
            }
            else
            {
                dCoachObj = new GameObject("D_coach", typeof(RectTransform), typeof(Image), typeof(SessionEvaluationController));
                dCoachObj.transform.SetParent(canvas.transform, false);
            }

            if (PrefabUtility.IsPartOfPrefabInstance(dCoachObj))
            {
                PrefabUtility.UnpackPrefabInstance(dCoachObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            Undo.RegisterFullObjectHierarchyUndo(dCoachObj, "Build D_coach Screen UI");

            // Nạp sprites và fonts
            Sprite sprRounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Common/UI_Rounded.png");
            Sprite sprBack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/back.png");
            Sprite sprHome = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/home.png");
            Sprite sprStreak = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/streak-hip-hop.png");
            Sprite sprDuration = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/duration.png");
            Sprite sprAchievement = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/achievement.png");
            Sprite sprRingTrack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Profile/score_ring_track.png");
            Sprite sprRingBlue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Profile/score_ring_blue.png");

            TMP_FontAsset fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/SegoeUI-Bold SDF.asset");
            TMP_FontAsset fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/SegoeUI-Regular SDF.asset");

            // 1. Root D_coach setup
            var rootRt = dCoachObj.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var rootImg = dCoachObj.GetComponent<Image>();
            if (rootImg == null) rootImg = dCoachObj.AddComponent<Image>();
            rootImg.color = ColorBgScreen;
            rootImg.raycastTarget = true;

            // Xóa sạch children cũ để dựng lại chính xác 100% theo Figma
            while (dCoachObj.transform.childCount > 0)
            {
                UnityEngine.Object.DestroyImmediate(dCoachObj.transform.GetChild(0).gameObject);
            }

            // ==========================================
            // 2. HEADER (Top 88px, dark navy #0D1B2A)
            // ==========================================
            GameObject headerObj = CreateUIObject("Header", dCoachObj.transform);
            var headerRt = headerObj.GetComponent<RectTransform>();
            SetAnchors(headerRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            headerRt.anchoredPosition = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0f, 88f);

            var headerImg = headerObj.AddComponent<Image>();
            headerImg.color = ColorHeader;

            // Nút Back
            GameObject btnBackObj = CreateButton("Button_Back", headerObj.transform, sprRounded, ColorHeader, 4.5f);
            var btnBackRt = btnBackObj.GetComponent<RectTransform>();
            SetAnchors(btnBackRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            btnBackRt.anchoredPosition = new Vector2(16f, 10f);
            btnBackRt.sizeDelta = new Vector2(44f, 44f);

            GameObject iconBackObj = CreateImage("Icon_Back", btnBackObj.transform, sprBack, Color.white);
            var iconBackRt = iconBackObj.GetComponent<RectTransform>();
            SetAnchors(iconBackRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            iconBackRt.sizeDelta = new Vector2(28f, 28f);

            // Tiêu đề Header "Kết quả luyện tập"
            GameObject titleObj = CreateText("Text_Title", headerObj.transform, "Kết quả luyện tập", fontBold, 18f, Color.white, TextAlignmentOptions.Center);
            var titleRt = titleObj.GetComponent<RectTransform>();
            SetAnchors(titleRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f));
            titleRt.anchoredPosition = new Vector2(0f, 32f);
            titleRt.sizeDelta = new Vector2(-120f, 44f);

            // Nút Home
            GameObject btnHomeObj = CreateButton("Button_Home", headerObj.transform, sprRounded, ColorHeader, 4.5f);
            var btnHomeRt = btnHomeObj.GetComponent<RectTransform>();
            SetAnchors(btnHomeRt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            btnHomeRt.anchoredPosition = new Vector2(-16f, 10f);
            btnHomeRt.sizeDelta = new Vector2(44f, 44f);

            GameObject iconHomeObj = CreateImage("Icon_Home", btnHomeObj.transform, sprHome, Color.white);
            var iconHomeRt = iconHomeObj.GetComponent<RectTransform>();
            SetAnchors(iconHomeRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            iconHomeRt.sizeDelta = new Vector2(28f, 28f);

            // ==========================================
            // 3. FIXED BOTTOM ACTIONS PANEL (Bottom 136px)
            // ==========================================
            GameObject bottomPanelObj = CreateUIObject("BottomPanel", dCoachObj.transform);
            var bottomPanelRt = bottomPanelObj.GetComponent<RectTransform>();
            SetAnchors(bottomPanelRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            bottomPanelRt.anchoredPosition = Vector2.zero;
            bottomPanelRt.sizeDelta = new Vector2(0f, 136f);

            var bottomPanelImg = bottomPanelObj.AddComponent<Image>();
            bottomPanelImg.color = Color.white;

            // Đường kẻ ngăn cách trên đáy (Top border)
            GameObject bottomDivider = CreateImage("TopDivider", bottomPanelObj.transform, null, ColorCardBorder);
            var dividerRt = bottomDivider.GetComponent<RectTransform>();
            SetAnchors(dividerRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            dividerRt.anchoredPosition = Vector2.zero;
            dividerRt.sizeDelta = new Vector2(0f, 1f);

            // Nút "Bài tiếp theo" (Primary Cyan)
            GameObject btnNextObj = CreateButtonWithText("Button_Next", bottomPanelObj.transform, sprRounded, ColorCyan, "Bài tiếp theo", fontBold, 15f, ColorTextDark, 3.5f);
            var btnNextRt = btnNextObj.GetComponent<RectTransform>();
            SetAnchors(btnNextRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            btnNextRt.anchoredPosition = new Vector2(0f, 74f);
            btnNextRt.sizeDelta = new Vector2(350f, 46f);

            // Hàng 3 nút thao tác: Tập lại, Lưu, Chia sẻ
            GameObject actionsRowObj = CreateUIObject("ActionsRow", bottomPanelObj.transform);
            var actionsRowRt = actionsRowObj.GetComponent<RectTransform>();
            SetAnchors(actionsRowRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            actionsRowRt.anchoredPosition = new Vector2(0f, 18f);
            actionsRowRt.sizeDelta = new Vector2(350f, 44f);

            var hlgActions = actionsRowObj.AddComponent<HorizontalLayoutGroup>();
            hlgActions.spacing = 10f;
            hlgActions.childControlWidth = true;
            hlgActions.childControlHeight = true;
            hlgActions.childForceExpandWidth = true;
            hlgActions.childForceExpandHeight = true;

            GameObject btnRetryObj = CreateOutlinedButton("Button_Retry", actionsRowObj.transform, sprRounded, "Tập lại", fontBold, 13f, ColorTextDark, ColorBtnBorder, 3.5f);
            GameObject btnSaveObj = CreateOutlinedButton("Button_Save", actionsRowObj.transform, sprRounded, "Lưu", fontBold, 13f, ColorTextDark, ColorBtnBorder, 3.5f);
            GameObject btnShareObj = CreateOutlinedButton("Button_Share", actionsRowObj.transform, sprRounded, "Chia sẻ", fontBold, 13f, ColorTextDark, ColorBtnBorder, 3.5f);

            // ==========================================
            // 4. SCROLLABLE CONTENT AREA
            // ==========================================
            GameObject scrollObj = CreateUIObject("ScrollView", dCoachObj.transform);
            var scrollRt = scrollObj.GetComponent<RectTransform>();
            SetAnchors(scrollRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            scrollRt.offsetMin = new Vector2(0f, 136f);
            scrollRt.offsetMax = new Vector2(0f, -88f);

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.scrollSensitivity = 25f;

            // Viewport
            GameObject viewportObj = CreateUIObject("Viewport", scrollObj.transform);
            var viewportRt = viewportObj.GetComponent<RectTransform>();
            SetAnchors(viewportRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;

            var mask = viewportObj.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRt;

            // Content Container
            GameObject contentObj = CreateUIObject("Content", viewportObj.transform);
            var contentRt = contentObj.GetComponent<RectTransform>();
            SetAnchors(contentRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(350f, 0f);

            scrollRect.content = contentRt;

            var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f;
            vlg.padding = new RectOffset(0, 0, 16, 40);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true; // Bắt buộc true để các thẻ co giãn đúng chiều cao thiết kế
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ------------------------------------------
            // 4.1 LỜI CHÚC MỪNG HOÀN THÀNH BUỔI TẬP (350x58)
            // ------------------------------------------
            GameObject greetingObj = CreateUIObject("GreetingSection", contentObj.transform);
            var grRt = greetingObj.GetComponent<RectTransform>();
            grRt.sizeDelta = new Vector2(350f, 58f);
            var grLe = greetingObj.AddComponent<LayoutElement>();
            grLe.minHeight = 58f;
            grLe.preferredHeight = 58f;

            GameObject greetTitle = CreateText("Text_GreetTitle", greetingObj.transform, "Một buổi tập thật tốt!", fontBold, 22f, ColorTextDark, TextAlignmentOptions.Left);
            var gtRt = greetTitle.GetComponent<RectTransform>();
            SetAnchors(gtRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            gtRt.anchoredPosition = Vector2.zero;
            gtRt.sizeDelta = new Vector2(0f, 32f);

            GameObject greetSub = CreateText("Text_GreetSubtitle", greetingObj.transform, "Cùng nhìn lại những tiến bộ của bạn.", fontRegular, 14f, ColorTextMuted, TextAlignmentOptions.Left);
            var gsRt = greetSub.GetComponent<RectTransform>();
            SetAnchors(gsRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            gsRt.anchoredPosition = Vector2.zero;
            gsRt.sizeDelta = new Vector2(0f, 22f);

            // ------------------------------------------
            // 4.2 CARD 1: ĐIỂM TỔNG KẾT (350x377)
            // ------------------------------------------
            GameObject cardScore = CreateCardContainer("Card_OverallScore", contentObj.transform, sprRounded, 377f);

            // Badge "KỶ LỤC CÁ NHÂN"
            GameObject badgeRecord = CreateBadge("Badge_PersonalRecord", cardScore.transform, sprRounded, ColorCyanLight, "KỶ LỤC CÁ NHÂN", fontBold, 12f, ColorCyanDark);
            var brRt = badgeRecord.GetComponent<RectTransform>();
            SetAnchors(brRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            brRt.anchoredPosition = new Vector2(0f, -20f);
            brRt.sizeDelta = new Vector2(136f, 30f);

            // Vòng tròn điểm (176x176)
            GameObject ringHolder = CreateUIObject("Ring_ScoreHolder", cardScore.transform);
            var ringHolderRt = ringHolder.GetComponent<RectTransform>();
            SetAnchors(ringHolderRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            ringHolderRt.anchoredPosition = new Vector2(0f, -66f);
            ringHolderRt.sizeDelta = new Vector2(176f, 176f);

            // Track nền xám
            GameObject ringTrackObj = CreateImage("Ring_Track", ringHolder.transform, sprRingTrack, ColorCardBorder);
            var ringTrackRt = ringTrackObj.GetComponent<RectTransform>();
            SetAnchors(ringTrackRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            ringTrackRt.offsetMin = Vector2.zero;
            ringTrackRt.offsetMax = Vector2.zero;

            // Fill xanh dương tròn (radial fill 84%)
            GameObject ringFillObj = CreateImage("Ring_Fill", ringHolder.transform, sprRingBlue, ColorCyan);
            var ringFillRt = ringFillObj.GetComponent<RectTransform>();
            SetAnchors(ringFillRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            ringFillRt.offsetMin = Vector2.zero;
            ringFillRt.offsetMax = Vector2.zero;
            var fillImg = ringFillObj.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Radial360;
            fillImg.fillOrigin = (int)Image.Origin360.Top;
            fillImg.fillClockwise = true;
            fillImg.fillAmount = 0.84f;

            // Số điểm trung tâm "84"
            GameObject scoreNumObj = CreateText("Text_AccuracyScore", ringHolder.transform, "84", fontBold, 54f, ColorTextDark, TextAlignmentOptions.Center);
            var scoreNumRt = scoreNumObj.GetComponent<RectTransform>();
            SetAnchors(scoreNumRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            scoreNumRt.anchoredPosition = new Vector2(0f, 12f);
            scoreNumRt.sizeDelta = new Vector2(140f, 60f);

            // Nhãn "/ 100"
            GameObject scoreMaxObj = CreateText("Text_ScoreMax", ringHolder.transform, "/ 100", fontRegular, 14f, ColorTextMuted, TextAlignmentOptions.Center);
            var scoreMaxRt = scoreMaxObj.GetComponent<RectTransform>();
            SetAnchors(scoreMaxRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            scoreMaxRt.anchoredPosition = new Vector2(0f, -26f);
            scoreMaxRt.sizeDelta = new Vector2(100f, 20f);

            // Khung chứa Lời đánh giá, Delta điểm, Tên bài học (PraiseSection - Tự động xếp chồng bằng VerticalLayoutGroup)
            GameObject praiseSection = CreateUIObject("PraiseSection", cardScore.transform);
            var psRt = praiseSection.GetComponent<RectTransform>();
            SetAnchors(psRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            psRt.anchoredPosition = new Vector2(0f, 16f);
            psRt.sizeDelta = new Vector2(310f, 114f);

            var vlgPraise = praiseSection.AddComponent<VerticalLayoutGroup>();
            vlgPraise.spacing = 6f;
            vlgPraise.childControlWidth = true;
            vlgPraise.childControlHeight = false;
            vlgPraise.childForceExpandWidth = true;
            vlgPraise.childForceExpandHeight = false;
            vlgPraise.childAlignment = TextAnchor.MiddleCenter;

            // Đánh giá "Rất tốt!"
            GameObject statusTextObj = CreateText("Text_StatusAdvice", praiseSection.transform, "Rất tốt!", fontBold, 22f, ColorTextDark, TextAlignmentOptions.Center);
            statusTextObj.AddComponent<LayoutElement>().preferredHeight = 34f;

            // Delta điểm "+6 điểm so với lần trước" (Green)
            GameObject deltaTextObj = CreateText("Text_DeltaScore", praiseSection.transform, "+6 điểm so với lần trước", fontBold, 14f, ColorGreen, TextAlignmentOptions.Center);
            deltaTextObj.AddComponent<LayoutElement>().preferredHeight = 22f;

            // Tên bài học "Hip-Hop Foundations"
            GameObject lessonTextObj = CreateText("Text_LessonTitle", praiseSection.transform, "Hip-Hop Foundations", fontBold, 16f, ColorTextDark, TextAlignmentOptions.Center);
            lessonTextObj.AddComponent<LayoutElement>().preferredHeight = 24f;

            // ------------------------------------------
            // 4.3 CARD 2: 3 CỘT THỐNG KÊ (Thời gian, XP, Chuỗi ngày) (350x107)
            // ------------------------------------------
            GameObject statsRowObj = CreateUIObject("StatsRow", contentObj.transform);
            var srRt = statsRowObj.GetComponent<RectTransform>();
            srRt.sizeDelta = new Vector2(350f, 107f);
            var srLe = statsRowObj.AddComponent<LayoutElement>();
            srLe.minHeight = 107f;
            srLe.preferredHeight = 107f;

            var hlgStats = statsRowObj.AddComponent<HorizontalLayoutGroup>();
            hlgStats.spacing = 10f;
            hlgStats.childControlWidth = true;
            hlgStats.childControlHeight = true;
            hlgStats.childForceExpandWidth = true;
            hlgStats.childForceExpandHeight = true;

            CreateStatCard(statsRowObj.transform, sprRounded, sprDuration, "18 phút", "Thời gian", fontBold, fontRegular, "Col_Duration");
            CreateStatCard(statsRowObj.transform, sprRounded, sprAchievement, "120 XP", "Kinh nghiệm", fontBold, fontRegular, "Col_XP");
            CreateStatCard(statsRowObj.transform, sprRounded, sprStreak, "8 ngày", "Chuỗi ngày", fontBold, fontRegular, "Col_Streak");

            // ------------------------------------------
            // 4.4 CARD 3: CẤP ĐỘ HIỆN TẠI (350x107)
            // ------------------------------------------
            GameObject cardLevel = CreateCardContainer("Card_Level", contentObj.transform, sprRounded, 107f);

            GameObject iconLevel = CreateImage("Icon_Level", cardLevel.transform, sprAchievement, Color.white);
            var iconLevelRt = iconLevel.GetComponent<RectTransform>();
            SetAnchors(iconLevelRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            iconLevelRt.anchoredPosition = new Vector2(20f, 10f);
            iconLevelRt.sizeDelta = new Vector2(32f, 32f);

            GameObject txtLevelNum = CreateText("Text_LevelNum", cardLevel.transform, "Cấp độ 4", fontBold, 20f, ColorTextDark, TextAlignmentOptions.Left);
            var tlnRt = txtLevelNum.GetComponent<RectTransform>();
            SetAnchors(tlnRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            tlnRt.anchoredPosition = new Vector2(62f, 10f);
            tlnRt.sizeDelta = new Vector2(-78f, 28f);

            GameObject txtLevelXp = CreateText("Text_LevelXp", cardLevel.transform, "+120 XP từ buổi tập này", fontRegular, 14f, ColorTextMuted, TextAlignmentOptions.Left);
            var tlxRt = txtLevelXp.GetComponent<RectTransform>();
            SetAnchors(tlxRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            tlxRt.anchoredPosition = new Vector2(20f, -20f);
            tlxRt.sizeDelta = new Vector2(-40f, 22f);

            // ------------------------------------------
            // 4.5 CARD 4: THÀNH TÍCH CHUỖI NGÀY (350x150)
            // ------------------------------------------
            GameObject cardStreak = CreateCardContainer("Card_StreakAchievement", contentObj.transform, sprRounded, 150f);

            GameObject iconStreakAch = CreateImage("Icon_StreakLarge", cardStreak.transform, sprStreak, Color.white);
            var iconStreakAchRt = iconStreakAch.GetComponent<RectTransform>();
            SetAnchors(iconStreakAchRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            iconStreakAchRt.anchoredPosition = new Vector2(20f, -20f);
            iconStreakAchRt.sizeDelta = new Vector2(44f, 44f);

            GameObject txtStreakTitle = CreateText("Text_StreakTitle", cardStreak.transform, "8 ngày liên tiếp", fontBold, 20f, ColorTextDark, TextAlignmentOptions.Left);
            var tstRt = txtStreakTitle.GetComponent<RectTransform>();
            SetAnchors(tstRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tstRt.anchoredPosition = new Vector2(76f, -22f);
            tstRt.sizeDelta = new Vector2(-96f, 26f);

            GameObject txtStreakBadge = CreateText("Text_StreakBadge", cardStreak.transform, "Huy hiệu kiên trì", fontBold, 13f, ColorCyanDark, TextAlignmentOptions.Left);
            var tsbRt = txtStreakBadge.GetComponent<RectTransform>();
            SetAnchors(tsbRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tsbRt.anchoredPosition = new Vector2(76f, -48f);
            tsbRt.sizeDelta = new Vector2(-96f, 20f);

            GameObject txtStreakDesc = CreateText("Text_StreakDesc", cardStreak.transform, "Bạn đang xây dựng một thói quen đáng tự hào.", fontRegular, 14f, ColorTextMuted, TextAlignmentOptions.Left);
            var tsdRt = txtStreakDesc.GetComponent<RectTransform>();
            SetAnchors(tsdRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            tsdRt.anchoredPosition = new Vector2(20f, 18f);
            tsdRt.sizeDelta = new Vector2(-40f, 40f);

            // ------------------------------------------
            // 4.6 CARD 5: PHÂN TÍCH 7 KỸ NĂNG (350x498)
            // ------------------------------------------
            GameObject cardSkills = CreateCardContainer("Card_SkillsAnalysis", contentObj.transform, sprRounded, 498f);

            GameObject txtSkillsTitle = CreateText("Text_SkillsTitle", cardSkills.transform, "7 kỹ năng của bạn", fontBold, 20f, ColorTextDark, TextAlignmentOptions.Left);
            var tskRt = txtSkillsTitle.GetComponent<RectTransform>();
            SetAnchors(tskRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tskRt.anchoredPosition = new Vector2(20f, -20f);
            tskRt.sizeDelta = new Vector2(-40f, 28f);

            GameObject txtSkillsSub = CreateText("Text_SkillsSubtitle", cardSkills.transform, "Thay đổi so với lần luyện tập trước", fontRegular, 12f, ColorTextMuted, TextAlignmentOptions.Left);
            var tssRt = txtSkillsSub.GetComponent<RectTransform>();
            SetAnchors(tssRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tssRt.anchoredPosition = new Vector2(20f, -48f);
            tssRt.sizeDelta = new Vector2(-40f, 18f);

            // Danh sách 7 kỹ năng
            string[] skillNames = { "Vào nhịp", "Cảm nhịp", "Tay", "Chân", "Tư thế", "Thăng bằng", "Năng lượng" };
            int[] skillScores = { 79, 88, 76, 82, 91, 74, 83 };
            string[] skillDeltas = { "↑ +8", "↑ +9", "↑ +9", "↑ +1", "↑ +6", "↓ 3", "↑ +9" };
            Color[] skillDeltaColors = { ColorGreen, ColorGreen, ColorGreen, ColorGreen, ColorGreen, ColorRed, ColorGreen };

            float startY = -76f;
            float rowHeight = 56f;
            for (int k = 0; k < 7; k++)
            {
                CreateSkillRow(cardSkills.transform, sprRounded, skillNames[k], skillScores[k], skillDeltas[k], skillDeltaColors[k], startY - (k * rowHeight), fontBold, fontRegular);
            }

            // ------------------------------------------
            // 4.7 CARD 6: ĐIỂM LÀM TỐT (350x186)
            // ------------------------------------------
            GameObject cardHighlights = CreateCardContainer("Card_Highlights", contentObj.transform, sprRounded, 186f);

            GameObject txtGoodTitle = CreateText("Text_GoodTitle", cardHighlights.transform, "Bạn đã làm tốt", fontBold, 20f, ColorTextDark, TextAlignmentOptions.Left);
            var tgtRt = txtGoodTitle.GetComponent<RectTransform>();
            SetAnchors(tgtRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tgtRt.anchoredPosition = new Vector2(20f, -20f);
            tgtRt.sizeDelta = new Vector2(-40f, 28f);

            GameObject iconGoodMove = CreateImage("Icon_GoodMove", cardHighlights.transform, sprAchievement, Color.white);
            var igmRt = iconGoodMove.GetComponent<RectTransform>();
            SetAnchors(igmRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            igmRt.anchoredPosition = new Vector2(20f, -60f);
            igmRt.sizeDelta = new Vector2(32f, 32f);

            GameObject txtGoodMoveName = CreateText("Text_GoodMoveName", cardHighlights.transform, "Basic Bounce", fontBold, 17f, ColorTextDark, TextAlignmentOptions.Left);
            var tgmnRt = txtGoodMoveName.GetComponent<RectTransform>();
            SetAnchors(tgmnRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tgmnRt.anchoredPosition = new Vector2(62f, -56f);
            tgmnRt.sizeDelta = new Vector2(-82f, 24f);

            GameObject txtGoodMoveDesc = CreateText("Text_GoodMoveDesc", cardHighlights.transform, "Động tác nổi bật trong buổi tập", fontRegular, 12f, ColorTextMuted, TextAlignmentOptions.Left);
            var tgmdRt = txtGoodMoveDesc.GetComponent<RectTransform>();
            SetAnchors(tgmdRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tgmdRt.anchoredPosition = new Vector2(62f, -80f);
            tgmdRt.sizeDelta = new Vector2(-82f, 18f);

            GameObject txtGoodDetail = CreateText("Text_GoodDetail", cardHighlights.transform, "Tư thế đạt 91/100. Cảm nhịp tăng 9 điểm so với lần trước.", fontRegular, 14f, ColorTextMuted, TextAlignmentOptions.Left);
            var tgdtRt = txtGoodDetail.GetComponent<RectTransform>();
            SetAnchors(tgdtRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f));
            tgdtRt.anchoredPosition = new Vector2(20f, 18f);
            tgdtRt.sizeDelta = new Vector2(-40f, 40f);

            // ------------------------------------------
            // 4.8 CARD 7: ĐIỂM CẦN CẢI THIỆN (350x222)
            // ------------------------------------------
            GameObject cardNeedsWork = CreateCardContainer("Card_NeedsWork", contentObj.transform, sprRounded, 222f);

            GameObject txtImproveTitle = CreateText("Text_ImproveTitle", cardNeedsWork.transform, "Luyện thêm một chút", fontBold, 20f, ColorTextDark, TextAlignmentOptions.Left);
            var titRt = txtImproveTitle.GetComponent<RectTransform>();
            SetAnchors(titRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            titRt.anchoredPosition = new Vector2(20f, -20f);
            titRt.sizeDelta = new Vector2(-40f, 28f);

            GameObject txtIssue = CreateText("Text_TopErrors", cardNeedsWork.transform, "Tay trái chậm nửa nhịp", fontBold, 16f, ColorTextDark, TextAlignmentOptions.Left);
            var tissRt = txtIssue.GetComponent<RectTransform>();
            SetAnchors(tissRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tissRt.anchoredPosition = new Vector2(20f, -54f);
            tissRt.sizeDelta = new Vector2(-40f, 24f);

            GameObject txtAdvice = CreateText("Text_IncorrectSections", cardNeedsWork.transform, "Luyện lại phần 2 ở tốc độ 0.75× để đồng bộ tay với nhịp bài nhảy.", fontRegular, 14f, ColorTextMuted, TextAlignmentOptions.Left);
            var tadvRt = txtAdvice.GetComponent<RectTransform>();
            SetAnchors(tadvRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tadvRt.anchoredPosition = new Vector2(20f, -84f);
            tadvRt.sizeDelta = new Vector2(-40f, 42f);

            GameObject btnReview = CreateOutlinedButton("Button_ReviewSnippet", cardNeedsWork.transform, sprRounded, "Xem lại đoạn cần chỉnh", fontBold, 14f, ColorTextDark, ColorBtnBorder, 3.5f);
            var brvRt = btnReview.GetComponent<RectTransform>();
            SetAnchors(brvRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            brvRt.anchoredPosition = new Vector2(0f, 18f);
            brvRt.sizeDelta = new Vector2(310f, 46f);

            // ------------------------------------------
            // 4.9 CARD 8: XEM LẠI THEO GIAI ĐOẠN (350x339)
            // ------------------------------------------
            GameObject cardPhases = CreateCardContainer("Card_PhasedReview", contentObj.transform, sprRounded, 339f);

            GameObject txtPhaseTitle = CreateText("Text_PhasesTitle", cardPhases.transform, "Nhìn lại buổi tập", fontBold, 20f, ColorTextDark, TextAlignmentOptions.Left);
            var tptRt = txtPhaseTitle.GetComponent<RectTransform>();
            SetAnchors(tptRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            tptRt.anchoredPosition = new Vector2(20f, -20f);
            tptRt.sizeDelta = new Vector2(-40f, 28f);

            // 4 Phase step indicators
            GameObject phaseRow = CreateUIObject("PhaseStepsRow", cardPhases.transform);
            var prRt = phaseRow.GetComponent<RectTransform>();
            SetAnchors(prRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            prRt.anchoredPosition = new Vector2(0f, -54f);
            prRt.sizeDelta = new Vector2(-40f, 40f);

            var hlgPhases = phaseRow.AddComponent<HorizontalLayoutGroup>();
            hlgPhases.spacing = 6f;
            hlgPhases.childControlWidth = true;
            hlgPhases.childControlHeight = true;
            hlgPhases.childForceExpandWidth = true;
            hlgPhases.childForceExpandHeight = true;

            CreatePhaseStep(phaseRow.transform, sprRounded, "Khởi động & bắt nhịp", false, fontRegular);
            CreatePhaseStep(phaseRow.transform, sprRounded, "The Basic Bounce", true, fontBold);
            CreatePhaseStep(phaseRow.transform, sprRounded, "Chuyển trọng tâm", false, fontRegular);
            CreatePhaseStep(phaseRow.transform, sprRounded, "Ghép bài", false, fontRegular);

            // Tiêu đề thanh diễn biến
            GameObject txtTimelineLabel = CreateText("Text_TimelineLabel", cardPhases.transform, "Diễn biến chuyển động", fontBold, 13f, ColorTextDark, TextAlignmentOptions.Left);
            var ttlRt = txtTimelineLabel.GetComponent<RectTransform>();
            SetAnchors(ttlRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            ttlRt.anchoredPosition = new Vector2(20f, -108f);
            ttlRt.sizeDelta = new Vector2(-40f, 20f);

            // Thanh phân đoạn thời gian đa màu sắc (Đúng, Bỏ lỡ, Chậm nhịp, Đúng)
            GameObject multiBar = CreateImage("MultiSegmentBar", cardPhases.transform, sprRounded, ColorCardBorder, 20f);
            var mbRt = multiBar.GetComponent<RectTransform>();
            SetAnchors(mbRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            mbRt.anchoredPosition = new Vector2(0f, -134f);
            mbRt.sizeDelta = new Vector2(-40f, 18f);

            CreateSegment(multiBar.transform, sprRounded, ColorCyan, 0f, 0.35f);
            CreateSegment(multiBar.transform, sprRounded, ColorRed, 0.35f, 0.48f);
            CreateSegment(multiBar.transform, sprRounded, ColorOrange, 0.48f, 0.60f);
            CreateSegment(multiBar.transform, sprRounded, ColorCyan, 0.60f, 1.0f);

            // Thời lượng 00:00 -> 18:00
            GameObject txtTimeStart = CreateText("Text_TimeStart", cardPhases.transform, "00:00", fontRegular, 12f, ColorTextMuted, TextAlignmentOptions.Left);
            var ttsRt = txtTimeStart.GetComponent<RectTransform>();
            SetAnchors(ttsRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            ttsRt.anchoredPosition = new Vector2(20f, -156f);
            ttsRt.sizeDelta = new Vector2(60f, 18f);

            GameObject txtTimeEnd = CreateText("Text_TimeEnd", cardPhases.transform, "18:00", fontRegular, 12f, ColorTextMuted, TextAlignmentOptions.Right);
            var tteRt = txtTimeEnd.GetComponent<RectTransform>();
            SetAnchors(tteRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            tteRt.anchoredPosition = new Vector2(-20f, -156f);
            tteRt.sizeDelta = new Vector2(60f, 18f);

            // Chú giải màu (Legend: Đúng, Chậm nhịp, Bỏ lỡ)
            GameObject legendRow = CreateUIObject("LegendRow", cardPhases.transform);
            var legRt = legendRow.GetComponent<RectTransform>();
            SetAnchors(legRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            legRt.anchoredPosition = new Vector2(0f, -182f);
            legRt.sizeDelta = new Vector2(-40f, 20f);

            var hlgLegend = legendRow.AddComponent<HorizontalLayoutGroup>();
            hlgLegend.spacing = 16f;
            hlgLegend.childControlWidth = false;
            hlgLegend.childControlHeight = true;
            hlgLegend.childForceExpandWidth = false;
            hlgLegend.childForceExpandHeight = true;

            CreateLegendItem(legendRow.transform, sprRounded, ColorCyan, "Đúng", fontRegular, 60f);
            CreateLegendItem(legendRow.transform, sprRounded, ColorOrange, "Chậm nhịp", fontRegular, 86f);
            CreateLegendItem(legendRow.transform, sprRounded, ColorRed, "Bỏ lỡ", fontRegular, 70f);

            // Nút "Xem video luyện tập"
            GameObject btnWatch = CreateOutlinedButton("Button_WatchVideo", cardPhases.transform, sprRounded, "Xem video luyện tập", fontBold, 14f, ColorTextDark, ColorBtnBorder, 3.5f);
            var bwRt = btnWatch.GetComponent<RectTransform>();
            SetAnchors(bwRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            bwRt.anchoredPosition = new Vector2(0f, 18f);
            bwRt.sizeDelta = new Vector2(310f, 46f);

            // ------------------------------------------
            // 4.10 NÚT VỀ TRANG CHỦ (350x48)
            // ------------------------------------------
            GameObject btnBackHome = CreateButtonWithText("Button_BackToHome", contentObj.transform, sprRounded, ColorCyanLight, "Về trang chủ", fontBold, 14f, ColorCyanDark, 3.5f);
            var bbhRt = btnBackHome.GetComponent<RectTransform>();
            bbhRt.sizeDelta = new Vector2(350f, 48f);
            var bbhLe = btnBackHome.AddComponent<LayoutElement>();
            bbhLe.minHeight = 48f;
            bbhLe.preferredHeight = 48f;

            // Đệm khoảng trắng đáy để nội dung cuộn không bị Fixed Bottom Bar che lấp
            GameObject bottomSpacer = CreateUIObject("BottomSpacer", contentObj.transform);
            var bsRt = bottomSpacer.GetComponent<RectTransform>();
            bsRt.sizeDelta = new Vector2(350f, 32f);
            var bsLe = bottomSpacer.AddComponent<LayoutElement>();
            bsLe.minHeight = 32f;
            bsLe.preferredHeight = 32f;

            // ==========================================
            // 5. GÁN CÁC SERIALIZED FIELD CHO CONTROLLER
            // ==========================================
            var controller = dCoachObj.GetComponent<SessionEvaluationController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("btnBack").objectReferenceValue = btnBackObj.GetComponent<Button>();
                so.FindProperty("btnHome").objectReferenceValue = btnHomeObj.GetComponent<Button>();
                so.FindProperty("txtTitle").objectReferenceValue = titleObj.GetComponent<TMP_Text>();
                so.FindProperty("txtSubtitle").objectReferenceValue = lessonTextObj.GetComponent<TMP_Text>();

                so.FindProperty("badgePersonalRecord").objectReferenceValue = badgeRecord;
                so.FindProperty("imgAccuracyRing").objectReferenceValue = fillImg;
                so.FindProperty("txtAccuracyPercent").objectReferenceValue = scoreNumObj.GetComponent<TMP_Text>();
                so.FindProperty("txtCoachAdvice").objectReferenceValue = statusTextObj.GetComponent<TMP_Text>();
                so.FindProperty("txtDeltaScore").objectReferenceValue = deltaTextObj.GetComponent<TMP_Text>();
                so.FindProperty("txtLessonTitle").objectReferenceValue = lessonTextObj.GetComponent<TMP_Text>();

                so.FindProperty("txtDuration").objectReferenceValue = statsRowObj.transform.Find("Col_Duration/Text_Value").GetComponent<TMP_Text>();
                so.FindProperty("txtXP").objectReferenceValue = statsRowObj.transform.Find("Col_XP/Text_Value").GetComponent<TMP_Text>();
                so.FindProperty("txtStreakDays").objectReferenceValue = statsRowObj.transform.Find("Col_Streak/Text_Value").GetComponent<TMP_Text>();

                so.FindProperty("txtLevelNum").objectReferenceValue = txtLevelNum.GetComponent<TMP_Text>();
                so.FindProperty("txtLevelXp").objectReferenceValue = txtLevelXp.GetComponent<TMP_Text>();
                so.FindProperty("txtStreakTitle").objectReferenceValue = txtStreakTitle.GetComponent<TMP_Text>();
                so.FindProperty("txtStreakBadge").objectReferenceValue = txtStreakBadge.GetComponent<TMP_Text>();

                so.FindProperty("cardSkillsContainer").objectReferenceValue = cardSkills.transform;
                so.FindProperty("txtGoodMoveName").objectReferenceValue = txtGoodMoveName.GetComponent<TMP_Text>();
                so.FindProperty("txtGoodDetail").objectReferenceValue = txtGoodDetail.GetComponent<TMP_Text>();

                so.FindProperty("txtTopErrors").objectReferenceValue = txtIssue.GetComponent<TMP_Text>();
                so.FindProperty("txtIncorrectSections").objectReferenceValue = txtAdvice.GetComponent<TMP_Text>();

                so.FindProperty("btnNext").objectReferenceValue = btnNextObj.GetComponent<Button>();
                so.FindProperty("btnRetry").objectReferenceValue = btnRetryObj.GetComponent<Button>();
                so.FindProperty("btnSave").objectReferenceValue = btnSaveObj.GetComponent<Button>();
                so.FindProperty("btnShare").objectReferenceValue = btnShareObj.GetComponent<Button>();
                so.FindProperty("btnBackToHome").objectReferenceValue = btnBackHome.GetComponent<Button>();
                so.FindProperty("btnReviewSnippet").objectReferenceValue = btnReview.GetComponent<Button>();
                so.FindProperty("btnWatchVideo").objectReferenceValue = btnWatch.GetComponent<Button>();

                so.ApplyModifiedProperties();
            }

            // ==========================================
            // 6. LƯU PREFABS VÀ SCENE
            // ==========================================
            dCoachObj.SetActive(false); // Inactive mặc định để không che màn hình khi chưa hoàn thành tập

            PrefabUtility.SaveAsPrefabAsset(dCoachObj, "Assets/_Project/Prefabs/UI/D_coach.prefab");
            PrefabUtility.SaveAsPrefabAsset(dCoachObj, "Assets/Resources/D_coach.prefab");
            PrefabUtility.SaveAsPrefabAsset(dCoachObj, "Assets/_Project/Prefabs/UI/SessionEvaluationScreen.prefab");
            PrefabUtility.SaveAsPrefabAsset(dCoachObj, "Assets/Resources/SessionEvaluationScreen.prefab");

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("[BuildDCoachScreenUI] Đã tái tạo thành công màn hình D_coach chuẩn xác 100% theo Figma và lưu Prefabs!");
        }

        #region Helper Creation Methods

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateImage(string name, Transform parent, Sprite sprite, Color color, float ppu = 1f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite != null)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = ppu;
            }
            return go;
        }

        private static GameObject CreateText(string name, Transform parent, string text, TMP_FontAsset font, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            if (font != null) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, Sprite sprite, Color color, float ppu = 1f)
        {
            var go = CreateImage(name, parent, sprite, color, ppu);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            return go;
        }

        private static GameObject CreateButtonWithText(string name, Transform parent, Sprite sprite, Color bgColor, string label, TMP_FontAsset font, float fontSize, Color textColor, float ppu = 1f)
        {
            var go = CreateButton(name, parent, sprite, bgColor, ppu);
            GameObject textObj = CreateText("Text", go.transform, label, font, fontSize, textColor, TextAlignmentOptions.Center);
            var tRt = textObj.GetComponent<RectTransform>();
            SetAnchors(tRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject CreateOutlinedButton(string name, Transform parent, Sprite sprite, string label, TMP_FontAsset font, float fontSize, Color textColor, Color borderColor, float ppu = 1f)
        {
            var outer = CreateButton(name, parent, sprite, borderColor, ppu);
            var inner = CreateImage("InnerBg", outer.transform, sprite, Color.white, ppu);
            var inRt = inner.GetComponent<RectTransform>();
            SetAnchors(inRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            inRt.offsetMin = new Vector2(1f, 1f);
            inRt.offsetMax = new Vector2(-1f, -1f);
            inner.GetComponent<Image>().raycastTarget = false;

            GameObject textObj = CreateText("Text", outer.transform, label, font, fontSize, textColor, TextAlignmentOptions.Center);
            var tRt = textObj.GetComponent<RectTransform>();
            SetAnchors(tRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            return outer;
        }

        private static GameObject CreateBadge(string name, Transform parent, Sprite sprite, Color bgColor, string label, TMP_FontAsset font, float fontSize, Color textColor)
        {
            var go = CreateImage(name, parent, sprite, bgColor, 4f);
            GameObject textObj = CreateText("Text", go.transform, label, font, fontSize, textColor, TextAlignmentOptions.Center);
            var tRt = textObj.GetComponent<RectTransform>();
            SetAnchors(tRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            return go;
        }

        private static GameObject CreateCardContainer(string name, Transform parent, Sprite sprite, float height)
        {
            // Viền card ngoài với màu #D8E2EB
            var outer = CreateImage(name, parent, sprite, ColorCardBorder, 2.5f);
            var outerRt = outer.GetComponent<RectTransform>();
            outerRt.sizeDelta = new Vector2(350f, height);
            var le = outer.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;

            // Nền trắng bên trong lùi vào 1.5px
            var inner = CreateImage("CardBg", outer.transform, sprite, Color.white, 2.5f);
            var innerRt = inner.GetComponent<RectTransform>();
            SetAnchors(innerRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            innerRt.offsetMin = new Vector2(1.5f, 1.5f);
            innerRt.offsetMax = new Vector2(-1.5f, -1.5f);

            return outer;
        }

        private static void CreateStatCard(Transform parent, Sprite sprite, Sprite icon, string value, string label, TMP_FontAsset fontBold, TMP_FontAsset fontRegular, string name)
        {
            var card = CreateCardContainer(name, parent, sprite, 107f);

            // Icon
            GameObject iconObj = CreateImage("Icon", card.transform, icon, Color.white);
            var iconRt = iconObj.GetComponent<RectTransform>();
            SetAnchors(iconRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            iconRt.anchoredPosition = new Vector2(0f, -14f);
            iconRt.sizeDelta = new Vector2(26f, 26f);

            // Giá trị
            GameObject txtVal = CreateText("Text_Value", card.transform, value, fontBold, 17f, ColorTextDark, TextAlignmentOptions.Center);
            var valRt = txtVal.GetComponent<RectTransform>();
            SetAnchors(valRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            valRt.anchoredPosition = new Vector2(0f, -6f);
            valRt.sizeDelta = new Vector2(100f, 24f);

            // Nhãn
            GameObject txtLbl = CreateText("Text_Label", card.transform, label, fontRegular, 11f, ColorTextMuted, TextAlignmentOptions.Center);
            var lblRt = txtLbl.GetComponent<RectTransform>();
            SetAnchors(lblRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            lblRt.anchoredPosition = new Vector2(0f, 12f);
            lblRt.sizeDelta = new Vector2(100f, 18f);
        }

        private static void CreateSkillRow(Transform parent, Sprite sprite, string skillName, int score, string delta, Color deltaColor, float yPos, TMP_FontAsset fontBold, TMP_FontAsset fontRegular)
        {
            GameObject row = CreateUIObject("Skill_" + skillName, parent);
            var rowRt = row.GetComponent<RectTransform>();
            SetAnchors(rowRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            rowRt.anchoredPosition = new Vector2(0f, yPos);
            rowRt.sizeDelta = new Vector2(-40f, 48f);

            // Tên kỹ năng
            GameObject txtName = CreateText("Text_Name", row.transform, skillName, fontBold, 14f, ColorTextDark, TextAlignmentOptions.Left);
            var tnRt = txtName.GetComponent<RectTransform>();
            SetAnchors(tnRt, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f));
            tnRt.anchoredPosition = Vector2.zero;
            tnRt.sizeDelta = new Vector2(0f, 22f);

            // Điểm số
            GameObject txtScore = CreateText("Text_Score", row.transform, score.ToString(), fontBold, 16f, ColorTextDark, TextAlignmentOptions.Right);
            var tsRt = txtScore.GetComponent<RectTransform>();
            SetAnchors(tsRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            tsRt.anchoredPosition = new Vector2(-60f, 0f);
            tsRt.sizeDelta = new Vector2(40f, 22f);

            // Delta
            GameObject txtDelta = CreateText("Text_Delta", row.transform, delta, fontBold, 12f, deltaColor, TextAlignmentOptions.Right);
            var tdRt = txtDelta.GetComponent<RectTransform>();
            SetAnchors(tdRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            tdRt.anchoredPosition = new Vector2(0f, 0f);
            tdRt.sizeDelta = new Vector2(56f, 22f);

            // Thanh tiến độ track
            GameObject track = CreateImage("Track", row.transform, sprite, ColorCardBorder, 10f);
            track.AddComponent<RectMask2D>();
            var trkRt = track.GetComponent<RectTransform>();
            SetAnchors(trkRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            trkRt.anchoredPosition = new Vector2(0f, 6f);
            trkRt.sizeDelta = new Vector2(0f, 8f);

            // Thanh tiến độ fill
            GameObject fill = CreateImage("Fill", track.transform, sprite, ColorCyan, 10f);
            var fillRt = fill.GetComponent<RectTransform>();
            float pct = Mathf.Clamp01(score / 100f);
            SetAnchors(fillRt, Vector2.zero, new Vector2(pct, 1f), new Vector2(0f, 0.5f));
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
        }

        private static void CreatePhaseStep(Transform parent, Sprite sprite, string label, bool isActive, TMP_FontAsset font)
        {
            GameObject step = CreateUIObject("Step", parent);

            // Vạch chỉ báo
            Color barColor = isActive ? ColorCyan : ColorCardBorder;
            GameObject bar = CreateImage("Bar", step.transform, sprite, barColor, 10f);
            var barRt = bar.GetComponent<RectTransform>();
            SetAnchors(barRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            barRt.anchoredPosition = Vector2.zero;
            barRt.sizeDelta = new Vector2(0f, 4f);

            // Tên phase
            Color txtColor = isActive ? ColorTextDark : ColorTextMuted;
            GameObject txt = CreateText("Text", step.transform, label, font, 10f, txtColor, TextAlignmentOptions.Center);
            var txtRt = txt.GetComponent<RectTransform>();
            SetAnchors(txtRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            txtRt.offsetMin = new Vector2(0f, 0f);
            txtRt.offsetMax = new Vector2(0f, -8f);
        }

        private static void CreateSegment(Transform parent, Sprite sprite, Color color, float startNorm, float endNorm)
        {
            GameObject seg = CreateImage("Segment", parent, sprite, color, 10f);
            var rt = seg.GetComponent<RectTransform>();
            SetAnchors(rt, new Vector2(startNorm, 0f), new Vector2(endNorm, 1f), new Vector2(0f, 0.5f));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void CreateLegendItem(Transform parent, Sprite sprite, Color color, string label, TMP_FontAsset font, float width)
        {
            GameObject item = CreateUIObject("Item_" + label, parent);
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.sizeDelta = new Vector2(width, 20f);

            GameObject dot = CreateImage("Dot", item.transform, sprite, color, 10f);
            var dotRt = dot.GetComponent<RectTransform>();
            SetAnchors(dotRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            dotRt.anchoredPosition = new Vector2(5f, 0f);
            dotRt.sizeDelta = new Vector2(10f, 10f);

            GameObject txt = CreateText("Text", item.transform, label, font, 11f, ColorTextMuted, TextAlignmentOptions.Left);
            var txtRt = txt.GetComponent<RectTransform>();
            SetAnchors(txtRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f));
            txtRt.offsetMin = new Vector2(20f, 0f);
            txtRt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
        }

        #endregion
    }
}
