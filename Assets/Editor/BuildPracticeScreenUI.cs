using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using DanceUp.Features.Practice;
using DanceUp.CameraInput;

namespace DanceUp.Editor
{
    public static class BuildPracticeScreenUI
    {
        private static readonly Color ColorBgScreen = new Color(0.027f, 0.063f, 0.161f, 1f); // #071029
        private static readonly Color ColorDarkCard = new Color(0.051f, 0.106f, 0.165f, 1f); // #0D1B2A
        private static readonly Color ColorDarkCardTrans = new Color(0.051f, 0.106f, 0.165f, 0.90f);
        private static readonly Color ColorCyan = new Color(0f, 0.706f, 1f, 1f); // #00B4FF
        private static readonly Color ColorTextMuted = new Color(0.51f, 0.58f, 0.65f, 1f); // #8293A6
        private static readonly Color ColorGreen = new Color(0.067f, 0.478f, 0.318f, 1f); // #117A51
        private static readonly Color ColorGreySegment = new Color(0.337f, 0.396f, 0.471f, 1f); // #566578

        [MenuItem("DanceUp/Build Practice Screen UI")]
        public static void BuildUI()
        {
            var controller = UnityEngine.Object.FindObjectOfType<PracticeScreenController>(true);
            if (controller == null)
            {
                Debug.LogError("[BuildPracticeScreenUI] Không tìm thấy PracticeScreenController trong scene!");
                return;
            }

            GameObject root = controller.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(root, "Build Practice Screen UI");

            // Nạp assets
            Sprite sprRounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Common/UI_Rounded.png");
            Sprite sprClose = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/close.png");
            Sprite sprPause = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/pause.png");
            Sprite sprPlay = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/Practice/play.png");
            Sprite sprRewind = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/Lùi 10 giây.png");
            Sprite sprForward = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/Tiến 10 giây.png");
            Sprite sprMirror = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/mirror.png");
            Sprite sprVolume = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/volume.png");
            Sprite sprDancer = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/Ảnh dancer.png");

            TMP_FontAsset fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/SegoeUI-Bold SDF.asset");
            TMP_FontAsset fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/SegoeUI-Regular SDF.asset");

            // 1. Cấu hình Root PracticeScreen
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var rootImg = root.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.enabled = false;
                rootImg.color = Color.clear;
            }

            // Xóa UI cũ không cần thiết (giữ lại SkeletonOverlay và Background_Video)
            string[] toRemove = { "UI_Overlay", "BottomBar", "TopBar", "PerspectiveTabs", "CameraOverlays", "BottomPanel", "CountdownOverlay", "ReferenceVideo_Preview_3x4" };
            foreach (var name in toRemove)
            {
                var t = root.transform.Find(name);
                if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }

            // Đảm bảo Background_Video tồn tại và căn chỉnh chuẩn
            Transform bgVideoTrans = root.transform.Find("Background_Video");
            GameObject bgVideoObj;
            if (bgVideoTrans != null)
            {
                bgVideoObj = bgVideoTrans.gameObject;
            }
            else
            {
                bgVideoObj = new GameObject("Background_Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                bgVideoObj.transform.SetParent(root.transform, false);
            }
            bgVideoObj.transform.SetSiblingIndex(0);

            var bgVideoRt = bgVideoObj.GetComponent<RectTransform>();
            SetAnchors(bgVideoRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            bgVideoRt.offsetMin = Vector2.zero;
            bgVideoRt.offsetMax = Vector2.zero;

            var bgRawImg = bgVideoObj.GetComponent<RawImage>();
            bgRawImg.color = Color.white; // Màu trắng chuẩn để webcam feed hiển thị 100% sáng rõ tự nhiên, không bị phủ màu xanh đen
            var fitter = bgVideoObj.GetComponent<AspectRatioFitter>();
            if (fitter == null) fitter = bgVideoObj.AddComponent<AspectRatioFitter>();
            fitter.aspectRatio = 0.75f;
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            // Đảm bảo SkeletonOverlay tồn tại
            Transform skelTrans = root.transform.Find("SkeletonOverlay");
            GameObject skelObj;
            if (skelTrans != null)
            {
                skelObj = skelTrans.gameObject;
            }
            else
            {
                skelObj = new GameObject("SkeletonOverlay", typeof(RectTransform));
                skelObj.transform.SetParent(root.transform, false);
            }
            skelObj.transform.SetSiblingIndex(1);
            var skelRt = skelObj.GetComponent<RectTransform>();
            SetAnchors(skelRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            skelRt.offsetMin = Vector2.zero;
            skelRt.offsetMax = Vector2.zero;

            // 2. TẠO TOP BAR
            GameObject topBarObj = CreateUIObject("TopBar", root.transform);
            var topBarRt = topBarObj.GetComponent<RectTransform>();
            SetAnchors(topBarRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            topBarRt.anchoredPosition = new Vector2(0f, -38f);
            topBarRt.sizeDelta = new Vector2(-32f, 44f);

            // Nút Close
            GameObject btnCloseObj = CreateButton("Btn_Close", topBarObj.transform, sprRounded, ColorDarkCard, 4.5f);
            var btnCloseRt = btnCloseObj.GetComponent<RectTransform>();
            SetAnchors(btnCloseRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            btnCloseRt.anchoredPosition = Vector2.zero;
            btnCloseRt.sizeDelta = new Vector2(44f, 44f);

            GameObject iconCloseObj = CreateImage("Icon_Close", btnCloseObj.transform, sprClose, Color.white);
            var iconCloseRt = iconCloseObj.GetComponent<RectTransform>();
            SetAnchors(iconCloseRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            iconCloseRt.sizeDelta = new Vector2(24f, 24f);

            // Tiêu đề Header Center
            GameObject headerInfoObj = CreateUIObject("Header_Info", topBarObj.transform);
            var headerInfoRt = headerInfoObj.GetComponent<RectTransform>();
            SetAnchors(headerInfoRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            headerInfoRt.anchoredPosition = new Vector2(56f, 0f);
            headerInfoRt.sizeDelta = new Vector2(-124f, 44f);

            GameObject titleObj = CreateText("Text_Title", headerInfoObj.transform, "AI Coach", fontBold, 17f, Color.white, TextAlignmentOptions.Left);
            var titleRt = titleObj.GetComponent<RectTransform>();
            SetAnchors(titleRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            titleRt.anchoredPosition = new Vector2(0f, 10f);
            titleRt.sizeDelta = new Vector2(0f, 22f);

            GameObject subtitleObj = CreateText("Text_Subtitle", headerInfoObj.transform, "Hip-Hop Foundations · Phần 2", fontRegular, 11f, ColorTextMuted, TextAlignmentOptions.Left);
            var subtitleRt = subtitleObj.GetComponent<RectTransform>();
            SetAnchors(subtitleRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            subtitleRt.anchoredPosition = new Vector2(0f, -10f);
            subtitleRt.sizeDelta = new Vector2(0f, 18f);

            // Badge Score / Accuracy
            GameObject badgeScoreObj = CreateImage("Badge_Score", topBarObj.transform, sprRounded, ColorDarkCard, 4.5f);
            var badgeScoreRt = badgeScoreObj.GetComponent<RectTransform>();
            SetAnchors(badgeScoreRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            badgeScoreRt.anchoredPosition = Vector2.zero;
            badgeScoreRt.sizeDelta = new Vector2(56f, 44f);

            GameObject scoreValObj = CreateText("Text_ScoreVal", badgeScoreObj.transform, "84", fontBold, 14f, Color.white, TextAlignmentOptions.Center);
            var scoreValRt = scoreValObj.GetComponent<RectTransform>();
            SetAnchors(scoreValRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            scoreValRt.offsetMin = Vector2.zero;
            scoreValRt.offsetMax = Vector2.zero;

            // 3. TẠO PERSPECTIVE TABS (Góc nhìn)
            GameObject tabsObj = CreateUIObject("PerspectiveTabs", root.transform);
            var tabsRt = tabsObj.GetComponent<RectTransform>();
            SetAnchors(tabsRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            tabsRt.anchoredPosition = new Vector2(0f, -92f);
            tabsRt.sizeDelta = new Vector2(-32f, 44f);

            var hlg = tabsObj.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            GameObject btnFront = CreateButtonWithText("Btn_Front", tabsObj.transform, sprRounded, ColorDarkCard, "Trước", fontBold, 13f, Color.white, 4.5f);
            GameObject btnBack = CreateButtonWithText("Btn_Back", tabsObj.transform, sprRounded, ColorDarkCard, "Sau", fontBold, 13f, Color.white, 4.5f);
            GameObject btnCamera = CreateButtonWithText("Btn_Camera", tabsObj.transform, sprRounded, ColorCyan, "Camera", fontBold, 13f, ColorBgScreen, 4.5f);

            // 4. TẠO CAMERA OVERLAYS
            GameObject overlaysObj = CreateUIObject("CameraOverlays", root.transform);
            var overlaysRt = overlaysObj.GetComponent<RectTransform>();
            SetAnchors(overlaysRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            overlaysRt.offsetMin = new Vector2(16f, 290f);
            overlaysRt.offsetMax = new Vector2(-16f, -146f);

            // Badge Camera sẵn sàng (Bubble Coach / Lời khuyên AI)
            GameObject badgeStatusObj = CreateImage("Badge_CameraStatus", overlaysObj.transform, sprRounded, ColorDarkCardTrans, 4.5f);
            var badgeStatusRt = badgeStatusObj.GetComponent<RectTransform>();
            SetAnchors(badgeStatusRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            badgeStatusRt.anchoredPosition = new Vector2(0f, -8f);
            badgeStatusRt.sizeDelta = new Vector2(170f, 32f);

            GameObject adviceObj = CreateText("Text_Advice", badgeStatusObj.transform, "Camera sẵn sàng", fontBold, 12f, Color.white, TextAlignmentOptions.Center);
            var adviceRt = adviceObj.GetComponent<RectTransform>();
            SetAnchors(adviceRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            adviceRt.offsetMin = new Vector2(10f, 0f);
            adviceRt.offsetMax = new Vector2(-10f, 0f);

            // Video mẫu thu nhỏ (Top Right)
            GameObject refVideoContainer = CreateImage("ReferenceVideo_Container", overlaysObj.transform, sprRounded, ColorDarkCardTrans, 4.5f);
            var refVideoRt = refVideoContainer.GetComponent<RectTransform>();
            SetAnchors(refVideoRt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            refVideoRt.anchoredPosition = new Vector2(0f, -8f);
            refVideoRt.sizeDelta = new Vector2(100f, 130f);

            GameObject videoDisplayObj = CreateUIObject("VideoDisplay", refVideoContainer.transform);
            var videoDisplayRt = videoDisplayObj.GetComponent<RectTransform>();
            SetAnchors(videoDisplayRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            videoDisplayRt.anchoredPosition = new Vector2(0f, -4f);
            videoDisplayRt.sizeDelta = new Vector2(92f, 100f);

            // Thêm ảnh mẫu giáo viên làm placeholder khi chưa phát video
            if (sprDancer != null)
            {
                var placeholderImg = CreateImage("DancerPlaceholder", videoDisplayObj.transform, sprDancer, Color.white, 4.5f);
                var phRt = placeholderImg.GetComponent<RectTransform>();
                SetAnchors(phRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                phRt.offsetMin = Vector2.zero;
                phRt.offsetMax = Vector2.zero;
            }

            var rawImgVideo = videoDisplayObj.AddComponent<RawImage>();
            if (sprDancer != null) rawImgVideo.texture = sprDancer.texture;
            var vp = videoDisplayObj.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.waitForFirstFrame = true;
            vp.renderMode = VideoRenderMode.RenderTexture;
            var refRt = new RenderTexture(240, 320, 0);
            vp.targetTexture = refRt;
            rawImgVideo.texture = refRt;
            var refVidCtrl = videoDisplayObj.AddComponent<ReferenceVideoController>();
            var teacherClip = AssetDatabase.LoadAssetAtPath<VideoClip>("Assets/_Project/Video/1_teacher.mp4");
            if (teacherClip != null)
            {
                vp.clip = teacherClip;
                var soRef = new SerializedObject(refVidCtrl);
                var pClip = soRef.FindProperty("defaultReferenceClip");
                if (pClip != null) pClip.objectReferenceValue = teacherClip;
                var pVp = soRef.FindProperty("videoPlayer");
                if (pVp != null) pVp.objectReferenceValue = vp;
                var pRaw = soRef.FindProperty("videoDisplayImage");
                if (pRaw != null) pRaw.objectReferenceValue = rawImgVideo;
                soRef.ApplyModifiedProperties();
            }

            GameObject videoLabelObj = CreateText("Text_VideoLabel", refVideoContainer.transform, "Video mẫu", fontRegular, 10f, Color.white, TextAlignmentOptions.Center);
            var videoLabelRt = videoLabelObj.GetComponent<RectTransform>();
            SetAnchors(videoLabelRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            videoLabelRt.anchoredPosition = new Vector2(0f, 4f);
            videoLabelRt.sizeDelta = new Vector2(0f, 18f);

            // Nút bật/tắt Khung xương (Bottom Right của Camera Area)
            GameObject btnSkelObj = CreateButtonWithText("Btn_ToggleSkeleton", overlaysObj.transform, sprRounded, ColorDarkCardTrans, "Khung xương", fontBold, 12f, Color.white, 4.5f);
            var btnSkelRt = btnSkelObj.GetComponent<RectTransform>();
            SetAnchors(btnSkelRt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            btnSkelRt.anchoredPosition = new Vector2(0f, 8f);
            btnSkelRt.sizeDelta = new Vector2(126f, 42f);

            // 5. TẠO BOTTOM PANEL
            GameObject bottomPanelObj = CreateImage("BottomPanel", root.transform, null, ColorBgScreen);
            var bottomPanelRt = bottomPanelObj.GetComponent<RectTransform>();
            SetAnchors(bottomPanelRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            bottomPanelRt.anchoredPosition = Vector2.zero;
            bottomPanelRt.sizeDelta = new Vector2(0f, 286f);

            // Row 1: Động tác hiện tại
            GameObject currentMoveRow = CreateUIObject("CurrentMoveRow", bottomPanelObj.transform);
            var currentMoveRt = currentMoveRow.GetComponent<RectTransform>();
            SetAnchors(currentMoveRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            currentMoveRt.anchoredPosition = new Vector2(0f, -14f);
            currentMoveRt.sizeDelta = new Vector2(-32f, 44f);

            GameObject moveInfoObj = CreateUIObject("MoveInfo", currentMoveRow.transform);
            var moveInfoRt = moveInfoObj.GetComponent<RectTransform>();
            SetAnchors(moveInfoRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            moveInfoRt.anchoredPosition = Vector2.zero;
            moveInfoRt.sizeDelta = new Vector2(-104f, 44f);

            GameObject moveSubtitle = CreateText("Text_MoveSubtitle", moveInfoObj.transform, "ĐỘNG TÁC HIỆN TẠI", fontRegular, 10f, ColorTextMuted, TextAlignmentOptions.Left);
            var moveSubRt = moveSubtitle.GetComponent<RectTransform>();
            SetAnchors(moveSubRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            moveSubRt.anchoredPosition = new Vector2(0f, 10f);
            moveSubRt.sizeDelta = new Vector2(0f, 16f);

            GameObject moveTitle = CreateText("Text_MoveTitle", moveInfoObj.transform, "The Basic Bounce", fontBold, 17f, Color.white, TextAlignmentOptions.Left);
            var moveTitleRt = moveTitle.GetComponent<RectTransform>();
            SetAnchors(moveTitleRt, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            moveTitleRt.anchoredPosition = new Vector2(0f, -10f);
            moveTitleRt.sizeDelta = new Vector2(0f, 22f);

            GameObject badgeSectionObj = CreateButtonWithText("Badge_Section", currentMoveRow.transform, sprRounded, ColorDarkCard, "2 / 4  ▾", fontBold, 12f, Color.white, 4.5f);
            var badgeSectionRt = badgeSectionObj.GetComponent<RectTransform>();
            SetAnchors(badgeSectionRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            badgeSectionRt.anchoredPosition = Vector2.zero;
            badgeSectionRt.sizeDelta = new Vector2(96f, 44f);

            // Row 2: Timeline Section
            GameObject timelineSection = CreateUIObject("TimelineSection", bottomPanelObj.transform);
            var timelineRt = timelineSection.GetComponent<RectTransform>();
            SetAnchors(timelineRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            timelineRt.anchoredPosition = new Vector2(0f, -68f);
            timelineRt.sizeDelta = new Vector2(-32f, 32f);

            // 4 Segmented Track
            GameObject segmentedTrack = CreateUIObject("SegmentedTrack", timelineSection.transform);
            var segRt = segmentedTrack.GetComponent<RectTransform>();
            SetAnchors(segRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            segRt.anchoredPosition = new Vector2(0f, -3f);
            segRt.sizeDelta = new Vector2(0f, 6f);

            var segHlg = segmentedTrack.AddComponent<HorizontalLayoutGroup>();
            segHlg.spacing = 6f;
            segHlg.childControlWidth = true;
            segHlg.childControlHeight = true;
            segHlg.childForceExpandWidth = true;
            segHlg.childForceExpandHeight = true;

            CreateImage("Seg_Part1", segmentedTrack.transform, sprRounded, ColorGreen, 16f);
            CreateImage("Seg_Part2", segmentedTrack.transform, sprRounded, ColorCyan, 16f);
            CreateImage("Seg_Part3", segmentedTrack.transform, sprRounded, ColorGreySegment, 16f);
            CreateImage("Seg_Part4", segmentedTrack.transform, sprRounded, ColorGreySegment, 16f);

            // Slider Accuracy ẩn nhẹ trên thanh tiến độ để PracticeScreenController cập nhật giá trị
            GameObject sliderObj = CreateUIObject("Slider_Accuracy", timelineSection.transform);
            var sliderRt = sliderObj.GetComponent<RectTransform>();
            SetAnchors(sliderRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            sliderRt.anchoredPosition = new Vector2(0f, -3f);
            sliderRt.sizeDelta = new Vector2(0f, 6f);
            var sliderComp = sliderObj.AddComponent<Slider>();
            sliderComp.minValue = 0f;
            sliderComp.maxValue = 1f;
            sliderComp.value = 0.5f;

            // Timestamps row
            GameObject timestampsRow = CreateUIObject("TimestampsRow", timelineSection.transform);
            var tsRt = timestampsRow.GetComponent<RectTransform>();
            SetAnchors(tsRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            tsRt.anchoredPosition = new Vector2(0f, 2f);
            tsRt.sizeDelta = new Vector2(0f, 18f);

            GameObject timerObj = CreateText("Timer", timestampsRow.transform, "06:52", fontRegular, 11f, Color.white, TextAlignmentOptions.Left);
            var timerRt = timerObj.GetComponent<RectTransform>();
            SetAnchors(timerRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            timerRt.anchoredPosition = Vector2.zero;
            timerRt.sizeDelta = new Vector2(70f, 18f);

            GameObject totalDurationObj = CreateText("Text_TotalDuration", timestampsRow.transform, "18:00", fontRegular, 11f, ColorTextMuted, TextAlignmentOptions.Right);
            var totalDurRt = totalDurationObj.GetComponent<RectTransform>();
            SetAnchors(totalDurRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            totalDurRt.anchoredPosition = Vector2.zero;
            totalDurRt.sizeDelta = new Vector2(70f, 18f);

            // Row 3: Playback Controls (-10s, Pause/Play, +10s)
            GameObject playbackRow = CreateUIObject("PlaybackControlsRow", bottomPanelObj.transform);
            var playbackRt = playbackRow.GetComponent<RectTransform>();
            SetAnchors(playbackRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            playbackRt.anchoredPosition = new Vector2(0f, -120f);
            playbackRt.sizeDelta = new Vector2(-32f, 64f);

            // Nút -10s
            GameObject btnRewindObj = CreateButton("Btn_Rewind", playbackRow.transform, sprRounded, ColorDarkCard, 4.5f);
            var btnRewindRt = btnRewindObj.GetComponent<RectTransform>();
            SetAnchors(btnRewindRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            btnRewindRt.anchoredPosition = new Vector2(-88f, 0f);
            btnRewindRt.sizeDelta = new Vector2(64f, 44f);
            CreateText("Text_Rewind", btnRewindObj.transform, "−10s", fontBold, 12f, Color.white, TextAlignmentOptions.Center);

            // Nút Pause tròn Cyan 64x64
            GameObject btnPlayPauseObj = CreateButton("Btn_PlayPause", playbackRow.transform, sprRounded, ColorCyan, 2.0f);
            var btnPlayPauseRt = btnPlayPauseObj.GetComponent<RectTransform>();
            SetAnchors(btnPlayPauseRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            btnPlayPauseRt.anchoredPosition = Vector2.zero;
            btnPlayPauseRt.sizeDelta = new Vector2(64f, 64f);

            GameObject iconPlayPauseObj = CreateImage("Icon_PlayPause", btnPlayPauseObj.transform, sprPause, ColorBgScreen);
            var iconPPRt = iconPlayPauseObj.GetComponent<RectTransform>();
            SetAnchors(iconPPRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            iconPPRt.sizeDelta = new Vector2(28f, 28f);

            // Nút +10s
            GameObject btnForwardObj = CreateButton("Btn_Forward", playbackRow.transform, sprRounded, ColorDarkCard, 4.5f);
            var btnForwardRt = btnForwardObj.GetComponent<RectTransform>();
            SetAnchors(btnForwardRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            btnForwardRt.anchoredPosition = new Vector2(88f, 0f);
            btnForwardRt.sizeDelta = new Vector2(64f, 44f);
            CreateText("Text_Forward", btnForwardObj.transform, "+10s", fontBold, 12f, Color.white, TextAlignmentOptions.Center);

            // Row 4: Tools Row (1×, Lật gương, Lặp, Âm thanh)
            GameObject toolsRow = CreateUIObject("ToolsRow", bottomPanelObj.transform);
            var toolsRt = toolsRow.GetComponent<RectTransform>();
            SetAnchors(toolsRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            toolsRt.anchoredPosition = new Vector2(0f, 16f);
            toolsRt.sizeDelta = new Vector2(-32f, 44f);

            var toolsHlg = toolsRow.AddComponent<HorizontalLayoutGroup>();
            toolsHlg.spacing = 8f;
            toolsHlg.childControlWidth = true;
            toolsHlg.childControlHeight = true;
            toolsHlg.childForceExpandWidth = true;
            toolsHlg.childForceExpandHeight = true;

            GameObject btnSpeedObj = CreateButtonWithText("Btn_Speed", toolsRow.transform, sprRounded, ColorDarkCard, "1×", fontBold, 12f, Color.white, 4.5f);
            GameObject btnMirrorObj = CreateButtonWithText("Btn_Mirror", toolsRow.transform, sprRounded, ColorDarkCard, "Lật gương", fontBold, 12f, Color.white, 4.5f);
            GameObject btnLoopObj = CreateButtonWithText("Btn_Loop", toolsRow.transform, sprRounded, ColorDarkCard, "Lặp", fontBold, 12f, Color.white, 4.5f);
            GameObject btnAudioObj = CreateButtonWithText("Btn_Audio", toolsRow.transform, sprRounded, ColorDarkCard, "Âm thanh", fontBold, 12f, Color.white, 4.5f);

            // 6. COUNTDOWN OVERLAY
            GameObject cdObj = CreateUIObject("CountdownOverlay", root.transform);
            var cdRt = cdObj.GetComponent<RectTransform>();
            SetAnchors(cdRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            cdRt.anchoredPosition = new Vector2(0f, 30f);
            cdRt.sizeDelta = new Vector2(240f, 240f);

            GameObject cdBgObj = CreateImage("BgCircle", cdObj.transform, sprRounded, new Color(0.04f, 0.04f, 0.08f, 0.85f), 1.0f);
            var cdBgRt = cdBgObj.GetComponent<RectTransform>();
            SetAnchors(cdBgRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            cdBgRt.offsetMin = Vector2.zero;
            cdBgRt.offsetMax = Vector2.zero;

            GameObject cdTextObj = CreateText("Text_Countdown", cdObj.transform, "3", fontBold, 90f, new Color(1f, 0.85f, 0.1f, 1f), TextAlignmentOptions.Center);
            var cdTextRt = cdTextObj.GetComponent<RectTransform>();
            SetAnchors(cdTextRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            cdTextRt.offsetMin = Vector2.zero;
            cdTextRt.offsetMax = Vector2.zero;
            cdObj.SetActive(false);

            // 7. GÁN THAM CHIẾU VÀO PracticeScreenController
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("btnClose").objectReferenceValue = btnCloseObj.GetComponent<Button>();
            so.FindProperty("btnPlayPause").objectReferenceValue = btnPlayPauseObj.GetComponent<Button>();
            so.FindProperty("imgPlayPauseIcon").objectReferenceValue = iconPlayPauseObj.GetComponent<Image>();
            so.FindProperty("iconPlay").objectReferenceValue = sprPlay != null ? sprPlay : sprPause;
            so.FindProperty("iconPause").objectReferenceValue = sprPause;
            so.FindProperty("txtTimer").objectReferenceValue = timerObj.GetComponent<TMP_Text>();
            so.FindProperty("txtScore").objectReferenceValue = scoreValObj.GetComponent<TMP_Text>();
            so.FindProperty("txtAccuracyPercent").objectReferenceValue = scoreValObj.GetComponent<TMP_Text>();
            so.FindProperty("sliderAccuracy").objectReferenceValue = sliderComp;
            so.FindProperty("bubbleCoach").objectReferenceValue = badgeStatusObj;
            so.FindProperty("txtCoachAdvice").objectReferenceValue = adviceObj.GetComponent<TMP_Text>();
            so.FindProperty("skeletonContainer").objectReferenceValue = skelRt;
            so.FindProperty("referenceVideoController").objectReferenceValue = refVidCtrl;
            so.FindProperty("countdownContainer").objectReferenceValue = cdObj;
            so.FindProperty("txtCountdown").objectReferenceValue = cdTextObj.GetComponent<TMP_Text>();

            // Gán các nút mở rộng Figma
            var pRewind = so.FindProperty("btnRewind10s");
            if (pRewind != null) pRewind.objectReferenceValue = btnRewindObj.GetComponent<Button>();
            var pForward = so.FindProperty("btnForward10s");
            if (pForward != null) pForward.objectReferenceValue = btnForwardObj.GetComponent<Button>();
            var pSkel = so.FindProperty("btnToggleSkeleton");
            if (pSkel != null) pSkel.objectReferenceValue = btnSkelObj.GetComponent<Button>();
            var pMirror = so.FindProperty("btnMirror");
            if (pMirror != null) pMirror.objectReferenceValue = btnMirrorObj.GetComponent<Button>();
            var pPFront = so.FindProperty("btnPerspectiveFront");
            if (pPFront != null) pPFront.objectReferenceValue = btnFront.GetComponent<Button>();
            var pPBack = so.FindProperty("btnPerspectiveBack");
            if (pPBack != null) pPBack.objectReferenceValue = btnBack.GetComponent<Button>();
            var pPCam = so.FindProperty("btnPerspectiveCamera");
            if (pPCam != null) pPCam.objectReferenceValue = btnCamera.GetComponent<Button>();
            var pSpeed = so.FindProperty("btnSpeed");
            if (pSpeed != null) pSpeed.objectReferenceValue = btnSpeedObj.GetComponent<Button>();
            var pLoop = so.FindProperty("btnLoop");
            if (pLoop != null) pLoop.objectReferenceValue = btnLoopObj.GetComponent<Button>();
            var pAudio = so.FindProperty("btnAudio");
            if (pAudio != null) pAudio.objectReferenceValue = btnAudioObj.GetComponent<Button>();
            var pTxtSpeed = so.FindProperty("txtSpeed");
            if (pTxtSpeed != null) pTxtSpeed.objectReferenceValue = btnSpeedObj.GetComponentInChildren<TMP_Text>();
            var pTxtAccBadge = so.FindProperty("txtAccuracyBadge");
            if (pTxtAccBadge != null) pTxtAccBadge.objectReferenceValue = scoreValObj.GetComponent<TMP_Text>();

            // Chỉnh màu skeleton mặc định theo Figma (Cyan #00B4FF khi bình thường / chuẩn)
            var pCorrectColor = so.FindProperty("correctColor");
            if (pCorrectColor != null) pCorrectColor.colorValue = ColorCyan;

            so.ApplyModifiedProperties();

            // Cập nhật lại Prefab nếu có
            string prefabPath = "Assets/_Project/Prefabs/UI/PracticeScreen.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);

            // Lưu Scene
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);

            Debug.Log("[BuildPracticeScreenUI] Đã cập nhật thành công Practice Screen UI theo chuẩn thiết kế Figma!");
        }

        #region Helper Methods

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateImage(string name, Transform parent, Sprite sprite, Color color, float pixelsPerUnitMultiplier = 1f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            if (sprite != null)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            }
            img.color = color;
            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, Sprite sprite, Color color, float pixelsPerUnitMultiplier = 1f)
        {
            var go = CreateImage(name, parent, sprite, color, pixelsPerUnitMultiplier);
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = color * 1.15f;
            colors.pressedColor = color * 0.85f;
            btn.colors = colors;
            return go;
        }

        private static GameObject CreateButtonWithText(string name, Transform parent, Sprite sprite, Color bgColor, string text, TMP_FontAsset font, float fontSize, Color textColor, float pixelsPerUnitMultiplier = 1f)
        {
            var btnGo = CreateButton(name, parent, sprite, bgColor, pixelsPerUnitMultiplier);
            var txtGo = CreateText("Text", btnGo.transform, text, font, fontSize, textColor, TextAlignmentOptions.Center);
            var txtRt = txtGo.GetComponent<RectTransform>();
            SetAnchors(txtRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
            return btnGo;
        }

        private static GameObject CreateText(string name, Transform parent, string content, TMP_FontAsset font, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return go;
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
