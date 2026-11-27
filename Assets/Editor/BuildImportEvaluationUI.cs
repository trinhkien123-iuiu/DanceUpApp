#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using DanceUp.Features.Classroom;
using DanceUp.Features.ImportEvaluation;
using DanceUp.Features.Result;

namespace DanceUp.Editor
{
    /// <summary>
    /// Script Editor tự động xây dựng và cấu hình toàn bộ giao diện:
    /// 1. Nút "Đánh giá video có sẵn" dưới nút "Tiếp tục" trong RoadmapCard (LessonsScreen).
    /// 2. Modal chọn/nhập video bài nhảy ngoại tuyến (VideoImportModal).
    /// 3. Màn hình phân tích MediaPipe & DTW đang chạy (VideoAnalysisLoadingScreen).
    /// 4. Thẻ đối sánh chuỗi động tác DTW & thanh trực quan hóa sai lệch trên D_coach (Card_DtwDeviation).
    /// 5. Nút xuất video kèm khung xương 2D (Button_ExportVideo) trên D_coach.
    /// </summary>
    public static class BuildImportEvaluationUI
    {
        private static readonly Color ColorBgDark = new Color(0.027f, 0.063f, 0.161f, 1f);     // #071029
        private static readonly Color ColorCardDark = new Color(0.051f, 0.106f, 0.165f, 1f);   // #0D1B2A
        private static readonly Color ColorCardBorder = new Color(0.12f, 0.22f, 0.35f, 1f);
        private static readonly Color ColorCyan = new Color(0f, 0.706f, 1f, 1f);               // #00B4FF
        private static readonly Color ColorCyanMuted = new Color(0.22f, 0.74f, 0.97f, 0.25f);
        private static readonly Color ColorGreen = new Color(0.067f, 0.72f, 0.38f, 1f);        // #11B861
        private static readonly Color ColorAmber = new Color(0.96f, 0.62f, 0.04f, 1f);        // #F59E0B
        private static readonly Color ColorRed = new Color(0.95f, 0.22f, 0.35f, 1f);          // #F43F5E
        private static readonly Color ColorTextMuted = new Color(0.58f, 0.64f, 0.72f, 1f);     // #94A3B8

        [MenuItem("DanceUp/Build Import Evaluation Feature UI", false, 60)]
        public static void BuildAll()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
                if (canvases.Length > 0) canvas = canvases[0].gameObject;
            }

            if (canvas == null)
            {
                Debug.LogError("[BuildImportEvaluationUI] Không tìm thấy Canvas trong Scene!");
                return;
            }

            // Load fonts & sprites
            var fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/SegoeUI-Bold SDF.asset");
            if (fontBold == null) fontBold = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold");
            if (fontBold == null) fontBold = UnityEngine.Object.FindObjectsOfType<TMP_FontAsset>()[0];

            var fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/SegoeUI-Regular SDF.asset");
            if (fontRegular == null) fontRegular = fontBold;

            var sprRounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Common/UI_Rounded.png");

            // 1. Dựng VideoImportModal
            var modalController = BuildVideoImportModal(canvas.transform, sprRounded, fontBold, fontRegular);

            // 2. Dựng VideoAnalysisLoadingScreen
            var loadingController = BuildVideoAnalysisLoadingScreen(canvas.transform, sprRounded, fontBold, fontRegular);

            // 3. Cập nhật D_coach với Card DTW & Nút Xuất Video
            BuildDCoachEnhancements(canvas.transform, sprRounded, fontBold, fontRegular);

            // 4. Thêm nút "Đánh giá video có sẵn" trong LessonsScreen
            BuildLessonsScreenButton(canvas.transform, sprRounded, fontBold, fontRegular, modalController, loadingController);

            // 5. Lưu Prefabs & Scene
            SaveAllPrefabs(canvas);

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=#00B4FF><b>[BuildImportEvaluationUI]</b> Hoàn thành xây dựng và tích hợp giao diện Đánh giá Video Ngoại tuyến!</color>");
        }

        #region 1. Video Import Modal

        private static VideoImportModalController BuildVideoImportModal(Transform canvasTrans, Sprite sprRounded, TMP_FontAsset fontBold, TMP_FontAsset fontRegular)
        {
            Transform existing = canvasTrans.Find("VideoImportModal");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            // Root Modal Backdrop
            GameObject modalObj = CreateUIObject("VideoImportModal", canvasTrans);
            var rootRt = modalObj.GetComponent<RectTransform>();
            SetAnchors(rootRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var bgImg = modalObj.AddComponent<Image>();
            bgImg.color = new Color(0.027f, 0.063f, 0.161f, 0.85f);
            bgImg.raycastTarget = true;

            var controller = modalObj.AddComponent<VideoImportModalController>();

            // Dialog Card
            GameObject cardObj = CreateImage("DialogCard", modalObj.transform, sprRounded, ColorCardDark, 2.5f);
            var cardRt = cardObj.GetComponent<RectTransform>();
            SetAnchors(cardRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            cardRt.sizeDelta = new Vector2(368f, 556f);

            // Viền card
            var border = CreateImage("Border", cardObj.transform, sprRounded, ColorCardBorder, 2.5f);
            var bRt = border.GetComponent<RectTransform>();
            SetAnchors(bRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;
            border.GetComponent<Image>().raycastTarget = false;

            // Header
            GameObject header = CreateUIObject("Header", cardObj.transform);
            var hRt = header.GetComponent<RectTransform>();
            SetAnchors(hRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            hRt.anchoredPosition = new Vector2(0f, -12f);
            hRt.sizeDelta = new Vector2(-32f, 52f);

            GameObject txtTitle = CreateText("Txt_Title", header.transform, "ĐÁNH GIÁ VIDEO BÀI NHẢY", fontBold, 15f, ColorCyan, TextAlignmentOptions.Left);
            var ttRt = txtTitle.GetComponent<RectTransform>();
            SetAnchors(ttRt, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0f, 0.5f));
            ttRt.offsetMin = Vector2.zero;
            ttRt.offsetMax = new Vector2(-40f, 0f);

            GameObject txtSub = CreateText("Txt_Subtitle", header.transform, "Chọn video để đối sánh chuyển động với huấn luyện viên", fontRegular, 10.5f, ColorTextMuted, TextAlignmentOptions.Left);
            var tsRt = txtSub.GetComponent<RectTransform>();
            SetAnchors(tsRt, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            tsRt.offsetMin = Vector2.zero;
            tsRt.offsetMax = new Vector2(-40f, 0f);

            // Nút Đóng (X)
            GameObject btnClose = CreateButtonWithText("Btn_Close", header.transform, sprRounded, new Color(0.12f, 0.18f, 0.28f, 1f), "✕", fontBold, 14f, Color.white, 3f);
            var bcRt = btnClose.GetComponent<RectTransform>();
            SetAnchors(bcRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            bcRt.anchoredPosition = Vector2.zero;
            bcRt.sizeDelta = new Vector2(32f, 32f);

            // Container nội dung các lựa chọn
            GameObject content = CreateUIObject("Content", cardObj.transform);
            var cRt = content.GetComponent<RectTransform>();
            SetAnchors(cRt, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            cRt.offsetMin = new Vector2(16f, 76f);
            cRt.offsetMax = new Vector2(-16f, -68f);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 10f;

            // Section 1: VIDEO MẪU ĐỐI CHIẾU
            GameObject refSection = CreateUIObject("RefVideoSection", content.transform);
            var refLe = refSection.AddComponent<LayoutElement>();
            refLe.minHeight = 114f;
            refLe.preferredHeight = 114f;
            var refVlg = refSection.AddComponent<VerticalLayoutGroup>();
            refVlg.spacing = 6f;
            refVlg.childControlWidth = true;
            refVlg.childControlHeight = false;
            refVlg.childForceExpandWidth = true;
            refVlg.childForceExpandHeight = false;

            var lblRefHdr = CreateText("Txt_RefHeader", refSection.transform, "1. VIDEO MẪU ĐỐI CHIẾU (GIÁO VIÊN)", fontBold, 10.5f, ColorCyan, TextAlignmentOptions.BottomLeft);
            lblRefHdr.AddComponent<LayoutElement>().preferredHeight = 16f;

            GameObject btnRow = CreateUIObject("BtnRow", refSection.transform);
            var brLe = btnRow.AddComponent<LayoutElement>();
            brLe.minHeight = 36f;
            brLe.preferredHeight = 36f;
            var brHlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            brHlg.spacing = 8f;
            brHlg.childControlWidth = true;
            brHlg.childControlHeight = true;
            brHlg.childForceExpandWidth = true;
            brHlg.childForceExpandHeight = true;

            GameObject btnDefRef = CreateButtonWithText("Btn_DefaultRef", btnRow.transform, sprRounded, new Color(0.08f, 0.15f, 0.25f, 1f), "Mẫu sẵn (1_teacher)", fontBold, 11f, Color.white, 3f);
            GameObject btnBrowseRef = CreateButtonWithText("Btn_BrowseRef", btnRow.transform, sprRounded, new Color(0.06f, 0.22f, 0.34f, 1f), "Chọn Video Mẫu...", fontBold, 11f, ColorCyan, 3f);

            GameObject refBox = CreateImage("RefSelectedBox", refSection.transform, sprRounded, new Color(0.04f, 0.08f, 0.14f, 1f), 3f);
            var rbxLe = refBox.AddComponent<LayoutElement>();
            rbxLe.minHeight = 46f;
            rbxLe.preferredHeight = 46f;
            GameObject txtRefName = CreateText("Txt_RefName", refBox.transform, "1_teacher.mp4 (Mẫu mặc định)", fontBold, 11f, ColorCyan, TextAlignmentOptions.Left);
            var trnRt = txtRefName.GetComponent<RectTransform>();
            trnRt.anchoredPosition = new Vector2(12f, 8f);
            trnRt.sizeDelta = new Vector2(310f, 18f);
            GameObject txtRefMeta = CreateText("Txt_RefMeta", refBox.transform, "12.3s • 720×1280 • Sẵn sàng đối chiếu", fontRegular, 9.5f, ColorGreen, TextAlignmentOptions.Left);
            var trmRt = txtRefMeta.GetComponent<RectTransform>();
            trmRt.anchoredPosition = new Vector2(12f, -9f);
            trmRt.sizeDelta = new Vector2(310f, 16f);

            // Section 2 Header
            var lblStuHdr = CreateText("Txt_StudentHeader", content.transform, "2. VIDEO BÀI NHẢY CẦN ĐÁNH GIÁ (HỌC VIÊN)", fontBold, 10.5f, ColorGreen, TextAlignmentOptions.BottomLeft);
            lblStuHdr.AddComponent<LayoutElement>().preferredHeight = 18f;

            // Option 1: Video thử nghiệm học viên
            GameObject btnSample = CreateButton("Btn_SelectSample", content.transform, sprRounded, new Color(0.08f, 0.15f, 0.25f, 1f), 3f);
            var bsRt = btnSample.GetComponent<RectTransform>();
            bsRt.sizeDelta = new Vector2(0f, 62f);
            var bsLe = btnSample.AddComponent<LayoutElement>();
            bsLe.minHeight = 62f;
            bsLe.preferredHeight = 62f;

            CreateText("Label", btnSample.transform, "Dùng Video Bài Nhảy Thử Nghiệm", fontBold, 13f, Color.white, TextAlignmentOptions.Left)
                .GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, 10f);
            CreateText("Meta", btnSample.transform, "1_student.mp4 / 2_student.mp4 • Sẵn trong project", fontRegular, 10.5f, ColorCyan, TextAlignmentOptions.Left)
                .GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, -12f);

            // Option 2: Chọn từ máy tính
            GameObject btnBrowse = CreateButton("Btn_BrowseFile", content.transform, sprRounded, new Color(0.08f, 0.15f, 0.25f, 1f), 3f);
            var bbRt = btnBrowse.GetComponent<RectTransform>();
            bbRt.sizeDelta = new Vector2(0f, 62f);
            var bbLe = btnBrowse.AddComponent<LayoutElement>();
            bbLe.minHeight = 62f;
            bbLe.preferredHeight = 62f;

            CreateText("Label", btnBrowse.transform, "📁  Tải Video Từ Máy Tính", fontBold, 13f, Color.white, TextAlignmentOptions.Left)
                .GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, 10f);
            CreateText("Meta", btnBrowse.transform, "Hỗ trợ định dạng .mp4, .mov, .avi", fontRegular, 10.5f, ColorTextMuted, TextAlignmentOptions.Left)
                .GetComponent<RectTransform>().anchoredPosition = new Vector2(16f, -12f);

            // Khung hiển thị video đang chọn
            GameObject selectedBox = CreateImage("SelectedBox", content.transform, sprRounded, new Color(0.04f, 0.08f, 0.14f, 1f), 3f);
            var sbRt = selectedBox.GetComponent<RectTransform>();
            sbRt.sizeDelta = new Vector2(0f, 54f);
            var sbLe = selectedBox.AddComponent<LayoutElement>();
            sbLe.minHeight = 54f;
            sbLe.preferredHeight = 54f;

            GameObject txtSelName = CreateText("Txt_SelectedName", selectedBox.transform, "1_teacher.mp4 (Video mẫu kiểm thử)", fontBold, 11.5f, ColorCyan, TextAlignmentOptions.Left);
            var tsnRt = txtSelName.GetComponent<RectTransform>();
            tsnRt.anchoredPosition = new Vector2(14f, 8f);
            tsnRt.sizeDelta = new Vector2(300f, 18f);

            GameObject txtSelMeta = CreateText("Txt_SelectedMeta", selectedBox.transform, "12.3s • 720×1280 • 30 FPS • Đã sẵn sàng", fontRegular, 10f, ColorGreen, TextAlignmentOptions.Left);
            var tsmRt = txtSelMeta.GetComponent<RectTransform>();
            tsmRt.anchoredPosition = new Vector2(14f, -10f);
            tsmRt.sizeDelta = new Vector2(300f, 16f);

            // Nút Bắt đầu phân tích
            GameObject btnStart = CreateButtonWithText("Btn_StartAnalysis", cardObj.transform, sprRounded, ColorCyan, "BẮT ĐẦU PHÂN TÍCH AI ►", fontBold, 13f, ColorBgDark, 3f);
            var bstRt = btnStart.GetComponent<RectTransform>();
            SetAnchors(bstRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            bstRt.anchoredPosition = new Vector2(0f, 16f);
            bstRt.sizeDelta = new Vector2(-32f, 46f);

            // Gán Serialized Fields
            var so = new SerializedObject(controller);
            so.FindProperty("btnClose").objectReferenceValue = btnClose.GetComponent<Button>();
            so.FindProperty("txtTitle").objectReferenceValue = txtTitle.GetComponent<TMP_Text>();
            so.FindProperty("btnSelectDefaultRef").objectReferenceValue = btnDefRef.GetComponent<Button>();
            so.FindProperty("btnBrowseRefFile").objectReferenceValue = btnBrowseRef.GetComponent<Button>();
            so.FindProperty("txtRefVideoName").objectReferenceValue = txtRefName.GetComponent<TMP_Text>();
            so.FindProperty("txtRefVideoMeta").objectReferenceValue = txtRefMeta.GetComponent<TMP_Text>();
            so.FindProperty("btnSelectSample").objectReferenceValue = btnSample.GetComponent<Button>();
            so.FindProperty("btnBrowseFile").objectReferenceValue = btnBrowse.GetComponent<Button>();
            so.FindProperty("txtSelectedVideoName").objectReferenceValue = txtSelName.GetComponent<TMP_Text>();
            so.FindProperty("txtSelectedVideoMeta").objectReferenceValue = txtSelMeta.GetComponent<TMP_Text>();
            so.FindProperty("btnStartAnalysis").objectReferenceValue = btnStart.GetComponent<Button>();
            so.FindProperty("txtBtnStart").objectReferenceValue = btnStart.transform.Find("Text").GetComponent<TMP_Text>();
            so.ApplyModifiedProperties();

            modalObj.SetActive(false);
            return controller;
        }

        #endregion

        #region 2. Video Analysis Loading Screen

        private static VideoAnalysisLoadingController BuildVideoAnalysisLoadingScreen(Transform canvasTrans, Sprite sprRounded, TMP_FontAsset fontBold, TMP_FontAsset fontRegular)
        {
            Transform existing = canvasTrans.Find("VideoAnalysisLoadingScreen");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject loadingObj = CreateUIObject("VideoAnalysisLoadingScreen", canvasTrans);
            var rootRt = loadingObj.GetComponent<RectTransform>();
            SetAnchors(rootRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var bgImg = loadingObj.AddComponent<Image>();
            bgImg.color = new Color(0.027f, 0.063f, 0.161f, 0.94f);
            bgImg.raycastTarget = true;

            var controller = loadingObj.AddComponent<VideoAnalysisLoadingController>();

            // Loading Card (350 x 480 px)
            GameObject cardObj = CreateImage("LoadingCard", loadingObj.transform, sprRounded, ColorCardDark, 2.5f);
            var cardRt = cardObj.GetComponent<RectTransform>();
            SetAnchors(cardRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            cardRt.sizeDelta = new Vector2(350f, 480f);

            // Viền card
            var border = CreateImage("Border", cardObj.transform, sprRounded, ColorCardBorder, 2.5f);
            var bRt = border.GetComponent<RectTransform>();
            SetAnchors(bRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;
            border.GetComponent<Image>().raycastTarget = false;

            // Header
            GameObject txtTitle = CreateText("Txt_Title", cardObj.transform, "ĐANG PHÂN TÍCH VŨ ĐẠO", fontBold, 15f, ColorCyan, TextAlignmentOptions.Center);
            var ttRt = txtTitle.GetComponent<RectTransform>();
            SetAnchors(ttRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            ttRt.anchoredPosition = new Vector2(0f, -24f);
            ttRt.sizeDelta = new Vector2(300f, 24f);

            GameObject txtSub = CreateText("Txt_Subtitle", cardObj.transform, "HIP HOP BASIC • ARM GROOVE", fontRegular, 11f, ColorTextMuted, TextAlignmentOptions.Center);
            var tsRt = txtSub.GetComponent<RectTransform>();
            SetAnchors(tsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            tsRt.anchoredPosition = new Vector2(0f, -48f);
            tsRt.sizeDelta = new Vector2(300f, 18f);

            // Phần trăm tiến độ lớn
            GameObject txtPercent = CreateText("Txt_Percent", cardObj.transform, "0%", fontBold, 38f, ColorCyan, TextAlignmentOptions.Center);
            var tpRt = txtPercent.GetComponent<RectTransform>();
            SetAnchors(tpRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            tpRt.anchoredPosition = new Vector2(0f, -95f);
            tpRt.sizeDelta = new Vector2(300f, 44f);

            // Thanh Slider tiến độ (Cố định 290px nằm gọn trong card)
            GameObject sliderObj = CreateUIObject("Slider_Progress", cardObj.transform);
            var slRt = sliderObj.GetComponent<RectTransform>();
            SetAnchors(slRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            slRt.anchoredPosition = new Vector2(0f, -145f);
            slRt.sizeDelta = new Vector2(290f, 10f);

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            var bgTrack = CreateImage("Background", sliderObj.transform, sprRounded, new Color(0.12f, 0.18f, 0.28f, 1f), 3f);
            var bgtRt = bgTrack.GetComponent<RectTransform>();
            SetAnchors(bgtRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            bgtRt.offsetMin = Vector2.zero;
            bgtRt.offsetMax = Vector2.zero;

            GameObject fillArea = CreateUIObject("Fill Area", sliderObj.transform);
            var faRt = fillArea.GetComponent<RectTransform>();
            SetAnchors(faRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            faRt.offsetMin = Vector2.zero;
            faRt.offsetMax = Vector2.zero;

            var fill = CreateImage("Fill", fillArea.transform, sprRounded, ColorCyan, 3f);
            var fRt = fill.GetComponent<RectTransform>();
            SetAnchors(fRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;

            slider.fillRect = fRt;
            slider.targetGraphic = fill.GetComponent<Image>();

            // Status message
            GameObject txtStatus = CreateText("Txt_Status", cardObj.transform, "Đang khởi tạo bộ phân tích MediaPipe AI...", fontRegular, 11f, new Color(0.85f, 0.9f, 0.95f, 1f), TextAlignmentOptions.Center);
            var tstRt = txtStatus.GetComponent<RectTransform>();
            SetAnchors(tstRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            tstRt.anchoredPosition = new Vector2(0f, -170f);
            tstRt.sizeDelta = new Vector2(300f, 20f);

            // Steps Checklist Box (Cố định gọn gàng bên trong card)
            GameObject stepsBox = CreateImage("StepsBox", cardObj.transform, sprRounded, new Color(0.04f, 0.08f, 0.14f, 1f), 3f);
            var stRt = stepsBox.GetComponent<RectTransform>();
            SetAnchors(stRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            stRt.anchoredPosition = new Vector2(0f, -200f);
            stRt.sizeDelta = new Vector2(300f, 170f);

            var svlg = stepsBox.AddComponent<VerticalLayoutGroup>();
            svlg.padding = new RectOffset(14, 14, 12, 12);
            svlg.spacing = 10f;
            svlg.childControlWidth = true;
            svlg.childControlHeight = false;

            GameObject txtStep1 = CreateText("Txt_Step1", stepsBox.transform, "● 1. Trích xuất 13 khớp qua MediaPipe 2D Pose", fontRegular, 11f, ColorTextMuted, TextAlignmentOptions.Left);
            txtStep1.GetComponent<RectTransform>().sizeDelta = new Vector2(272f, 24f);
            GameObject txtStep2 = CreateText("Txt_Step2", stepsBox.transform, "● 2. Đối sánh chuỗi động tác theo thời gian (DTW)", fontRegular, 11f, ColorTextMuted, TextAlignmentOptions.Left);
            txtStep2.GetComponent<RectTransform>().sizeDelta = new Vector2(272f, 24f);
            GameObject txtStep3 = CreateText("Txt_Step3", stepsBox.transform, "● 3. Đánh giá độ chính xác 13 khớp & 7 kỹ năng", fontRegular, 11f, ColorTextMuted, TextAlignmentOptions.Left);
            txtStep3.GetComponent<RectTransform>().sizeDelta = new Vector2(272f, 24f);
            GameObject txtStep4 = CreateText("Txt_Step4", stepsBox.transform, "● 4. Tổng hợp nhận xét và phân tích sai lệch", fontRegular, 11f, ColorTextMuted, TextAlignmentOptions.Left);
            txtStep4.GetComponent<RectTransform>().sizeDelta = new Vector2(272f, 24f);

            // Nút Hủy
            GameObject btnCancel = CreateButtonWithText("Btn_Cancel", cardObj.transform, sprRounded, new Color(0.12f, 0.18f, 0.28f, 1f), "Hủy bỏ phân tích", fontRegular, 12f, ColorTextMuted, 3f);
            var bcRt = btnCancel.GetComponent<RectTransform>();
            SetAnchors(bcRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            bcRt.anchoredPosition = new Vector2(0f, 26f);
            bcRt.sizeDelta = new Vector2(300f, 38f);

            // Gán Serialized Fields
            var so = new SerializedObject(controller);
            so.FindProperty("txtTitle").objectReferenceValue = txtTitle.GetComponent<TMP_Text>();
            so.FindProperty("txtSubtitle").objectReferenceValue = txtSub.GetComponent<TMP_Text>();
            so.FindProperty("btnCancel").objectReferenceValue = btnCancel.GetComponent<Button>();
            so.FindProperty("sliderProgress").objectReferenceValue = slider;
            so.FindProperty("txtProgressPercent").objectReferenceValue = txtPercent.GetComponent<TMP_Text>();
            so.FindProperty("txtStatusMessage").objectReferenceValue = txtStatus.GetComponent<TMP_Text>();
            so.FindProperty("txtStep1").objectReferenceValue = txtStep1.GetComponent<TMP_Text>();
            so.FindProperty("txtStep2").objectReferenceValue = txtStep2.GetComponent<TMP_Text>();
            so.FindProperty("txtStep3").objectReferenceValue = txtStep3.GetComponent<TMP_Text>();
            so.FindProperty("txtStep4").objectReferenceValue = txtStep4.GetComponent<TMP_Text>();
            so.ApplyModifiedProperties();

            loadingObj.SetActive(false);
            return controller;
        }

        #endregion

        #region 3. D_coach DTW Card & Export Video Button

        private static void BuildDCoachEnhancements(Transform canvasTrans, Sprite sprRounded, TMP_FontAsset fontBold, TMP_FontAsset fontRegular)
        {
            Transform dCoachTrans = canvasTrans.Find("D_coach");
            if (dCoachTrans == null) return;

            Transform contentTrans = dCoachTrans.Find("ScrollView/Viewport/Content");
            if (contentTrans == null) return;

            // Xoá card cũ nếu có
            Transform oldCard = contentTrans.Find("Card_DtwDeviation");
            if (oldCard != null) UnityEngine.Object.DestroyImmediate(oldCard.gameObject);

            Transform oldBtnExport = contentTrans.Find("Button_ExportVideoContainer");
            if (oldBtnExport != null) UnityEngine.Object.DestroyImmediate(oldBtnExport.gameObject);

            Color ColorDCoachBorder = new Color(0.847f, 0.886f, 0.922f, 1f); // #D8E2EB
            Color ColorDCoachDark = new Color(0.027f, 0.063f, 0.161f, 1f);   // #071029
            Color ColorDCoachMuted = new Color(0.337f, 0.396f, 0.471f, 1f);  // #566578
            Color ColorDCoachCyan = new Color(0f, 0.706f, 1f, 1f);            // #00B4FF
            Color ColorDCoachGreen = new Color(0.067f, 0.478f, 0.318f, 1f);   // #117A51
            Color ColorDCoachAmber = new Color(1f, 0.541f, 0f, 1f);           // #FF8A00

            // 1. Viền ngoài Card_DtwDeviation chuẩn theo Figma (#D8E2EB, 350px x 285px)
            GameObject cardOuter = CreateImage("Card_DtwDeviation", contentTrans, sprRounded, ColorDCoachBorder, 2.5f);
            var cardRt = cardOuter.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(350f, 285f);
            var le = cardOuter.AddComponent<LayoutElement>();
            le.minHeight = 285f;
            le.preferredHeight = 285f;

            // Nền trắng bên trong lùi vào 1.5px
            GameObject cardBg = CreateImage("CardBg", cardOuter.transform, sprRounded, Color.white, 2.5f);
            var cbRt = cardBg.GetComponent<RectTransform>();
            SetAnchors(cbRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            cbRt.offsetMin = new Vector2(1.5f, 1.5f);
            cbRt.offsetMax = new Vector2(-1.5f, -1.5f);

            // Header (Cố định 315px căn giữa)
            GameObject txtTitle = CreateText("Txt_Title", cardBg.transform, "SO SÁNH CHUỖI ĐỘNG TÁC (DTW)", fontBold, 13.5f, ColorDCoachDark, TextAlignmentOptions.Left);
            var ttRt = txtTitle.GetComponent<RectTransform>();
            SetAnchors(ttRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            ttRt.anchoredPosition = new Vector2(0f, -14f);
            ttRt.sizeDelta = new Vector2(315f, 20f);

            GameObject txtSub = CreateText("Txt_Subtitle", cardBg.transform, "Đối sánh căn chỉnh thời gian & phân tích sai lệch động tác", fontRegular, 10.5f, ColorDCoachMuted, TextAlignmentOptions.Left);
            var tsRt = txtSub.GetComponent<RectTransform>();
            SetAnchors(tsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            tsRt.anchoredPosition = new Vector2(0f, -34f);
            tsRt.sizeDelta = new Vector2(315f, 16f);

            // 3 Cột thống kê DTW (Pills Row - Cố định 315px căn giữa)
            GameObject statRow = CreateUIObject("StatRow", cardBg.transform);
            var srRt = statRow.GetComponent<RectTransform>();
            SetAnchors(srRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            srRt.anchoredPosition = new Vector2(0f, -54f);
            srRt.sizeDelta = new Vector2(315f, 54f);

            var hlg = statRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // Stat 1: DTW Match
            var p1 = CreateStatPillLight(statRow.transform, sprRounded, "Độ khớp DTW", "89%", ColorDCoachCyan, fontBold, fontRegular);
            // Stat 2: Frame Match
            var p2 = CreateStatPillLight(statRow.transform, sprRounded, "Khớp khung hình", "82%", ColorDCoachGreen, fontBold, fontRegular);
            // Stat 3: Time drift
            var p3 = CreateStatPillLight(statRow.transform, sprRounded, "Độ lệch nhịp", "-0.15s", ColorDCoachAmber, fontBold, fontRegular);

            // Timeline Strip (Thanh thời gian trực quan hóa - Cố định 315px căn giữa)
            GameObject timelineBar = CreateImage("TimelineBar", cardBg.transform, sprRounded, new Color(0.92f, 0.94f, 0.96f, 1f), 3f);
            var tlRt = timelineBar.GetComponent<RectTransform>();
            SetAnchors(tlRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            tlRt.anchoredPosition = new Vector2(0f, -118f);
            tlRt.sizeDelta = new Vector2(315f, 20f);

            CreateTimelineBlock(timelineBar.transform, sprRounded, ColorDCoachGreen, 0f, 0.28f);
            CreateTimelineBlock(timelineBar.transform, sprRounded, ColorDCoachAmber, 0.28f, 0.58f);
            CreateTimelineBlock(timelineBar.transform, sprRounded, ColorDCoachGreen, 0.58f, 1.0f);

            // Chú giải màu sắc Timeline (Cố định 315px căn giữa)
            GameObject legendRow = CreateText("Legend", cardBg.transform, "● Chuẩn nhịp (Xanh)   ● Lệch nhịp (Vàng)   ● Sai động tác (Đỏ)", fontRegular, 9.5f, ColorDCoachMuted, TextAlignmentOptions.Center);
            var lgRt = legendRow.GetComponent<RectTransform>();
            SetAnchors(lgRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            lgRt.anchoredPosition = new Vector2(0f, -144f);
            lgRt.sizeDelta = new Vector2(315f, 16f);

            // Tóm tắt phân đoạn sai lệch (Cố định 315px căn giữa)
            GameObject txtDevSummary = CreateText("Txt_DeviationSummary", cardBg.transform, "Phát hiện phân đoạn lệch nhịp & động tác cần điều chỉnh:", fontRegular, 10.5f, ColorDCoachDark, TextAlignmentOptions.Left);
            var tdsRt = txtDevSummary.GetComponent<RectTransform>();
            SetAnchors(tdsRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            tdsRt.anchoredPosition = new Vector2(0f, -166f);
            tdsRt.sizeDelta = new Vector2(315f, 18f);

            // Danh sách phân đoạn sai lệch (DeviationList - Cố định 315px căn giữa)
            GameObject devList = CreateUIObject("DeviationList", cardBg.transform);
            var dlRt = devList.GetComponent<RectTransform>();
            SetAnchors(dlRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            dlRt.anchoredPosition = new Vector2(0f, -188f);
            dlRt.sizeDelta = new Vector2(315f, 80f);

            var dlvlg = devList.AddComponent<VerticalLayoutGroup>();
            dlvlg.spacing = 6f;
            dlvlg.childControlWidth = true;
            dlvlg.childControlHeight = false;

            // Đặt vị trí Card_DtwDeviation ngay sau Card_SkillsAnalysis
            Transform skillsCard = contentTrans.Find("Card_SkillsAnalysis");
            if (skillsCard != null)
            {
                cardOuter.transform.SetSiblingIndex(skillsCard.GetSiblingIndex() + 1);
            }

            // 2. Dựng Button_ExportVideoContainer
            GameObject exportContainer = CreateUIObject("Button_ExportVideoContainer", contentTrans);
            var ecRt = exportContainer.GetComponent<RectTransform>();
            ecRt.sizeDelta = new Vector2(350f, 75f);
            var ecLe = exportContainer.AddComponent<LayoutElement>();
            ecLe.minHeight = 75f;
            ecLe.preferredHeight = 75f;

            // Nút Xuất Video: Màu Navy Đậm (#0D1B2A)
            GameObject btnExport = CreateButton("Btn_ExportVideo", exportContainer.transform, sprRounded, ColorDCoachDark, 3f);
            var beRt = btnExport.GetComponent<RectTransform>();
            SetAnchors(beRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            beRt.anchoredPosition = Vector2.zero;
            beRt.sizeDelta = new Vector2(350f, 48f);

            GameObject txtExportLabel = CreateText("Text", btnExport.transform, "🎬  XUẤT VIDEO KÈM KHUNG XƯƠNG 2D (.MP4)", fontBold, 12f, Color.white, TextAlignmentOptions.Center);
            var telRt = txtExportLabel.GetComponent<RectTransform>();
            SetAnchors(telRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            telRt.offsetMin = Vector2.zero;
            telRt.offsetMax = Vector2.zero;

            // Thanh tiến độ xuất video
            GameObject exportSlider = CreateUIObject("Slider_Export", exportContainer.transform);
            var esRt = exportSlider.GetComponent<RectTransform>();
            SetAnchors(esRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            esRt.anchoredPosition = new Vector2(0f, -50f);
            esRt.sizeDelta = new Vector2(350f, 6f);

            var slExport = exportSlider.AddComponent<Slider>();
            slExport.minValue = 0f;
            slExport.maxValue = 1f;
            slExport.value = 0f;

            var bgEs = CreateImage("Bg", exportSlider.transform, sprRounded, new Color(0.85f, 0.88f, 0.92f, 1f), 3f);
            SetAnchors(bgEs.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            bgEs.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            bgEs.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var fillAreaEs = CreateUIObject("Fill Area", exportSlider.transform);
            SetAnchors(fillAreaEs.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            fillAreaEs.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            fillAreaEs.GetComponent<RectTransform>().offsetMax = Vector2.zero;

            var fillEs = CreateImage("Fill", fillAreaEs.transform, sprRounded, ColorDCoachCyan, 3f);
            SetAnchors(fillEs.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            fillEs.GetComponent<RectTransform>().offsetMin = Vector2.zero;
            fillEs.GetComponent<RectTransform>().offsetMax = Vector2.zero;
            slExport.fillRect = fillEs.GetComponent<RectTransform>();
            slExport.targetGraphic = fillEs.GetComponent<Image>();
            exportSlider.SetActive(false);

            // Trạng thái xuất video
            GameObject txtExportStatus = CreateText("Txt_ExportStatus", exportContainer.transform, "Sẵn sàng xuất video bài nhảy với khung xương", fontRegular, 10.5f, ColorDCoachMuted, TextAlignmentOptions.Center);
            var tesRt = txtExportStatus.GetComponent<RectTransform>();
            SetAnchors(tesRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            tesRt.anchoredPosition = new Vector2(0f, -54f);
            tesRt.sizeDelta = new Vector2(350f, 20f);

            // Đặt Button_ExportVideoContainer ngay trước Button_BackToHome
            Transform btnHome = contentTrans.Find("Button_BackToHome");
            if (btnHome != null)
            {
                exportContainer.transform.SetSiblingIndex(btnHome.GetSiblingIndex());
            }

            // Gán Serialized Fields cho SessionEvaluationController trên D_coach
            var controller = dCoachTrans.GetComponent<SessionEvaluationController>();
            if (controller != null)
            {
                var so = new SerializedObject(controller);
                so.FindProperty("cardDtwDeviation").objectReferenceValue = cardOuter;
                so.FindProperty("txtDtwAccuracy").objectReferenceValue = p1.Find("Txt_Val").GetComponent<TMP_Text>();
                so.FindProperty("txtFrameAccuracy").objectReferenceValue = p2.Find("Txt_Val").GetComponent<TMP_Text>();
                so.FindProperty("txtTimeDrift").objectReferenceValue = p3.Find("Txt_Val").GetComponent<TMP_Text>();
                so.FindProperty("deviationTimelineContainer").objectReferenceValue = timelineBar.GetComponent<RectTransform>();
                so.FindProperty("deviationSegmentsListContainer").objectReferenceValue = devList.transform;
                so.FindProperty("txtDeviationSummary").objectReferenceValue = txtDevSummary.GetComponent<TMP_Text>();

                so.FindProperty("btnExportVideo").objectReferenceValue = btnExport.GetComponent<Button>();
                so.FindProperty("txtExportStatus").objectReferenceValue = txtExportStatus.GetComponent<TMP_Text>();
                so.FindProperty("sliderExportProgress").objectReferenceValue = slExport;
                so.ApplyModifiedProperties();
            }
        }

        private static Transform CreateStatPillLight(Transform parent, Sprite sprRounded, string label, string val, Color valColor, TMP_FontAsset fontBold, TMP_FontAsset fontRegular)
        {
            GameObject pill = CreateImage("Pill_" + label, parent, sprRounded, new Color(0.95f, 0.965f, 0.98f, 1f), 3f);
            
            var lblObj = CreateText("Txt_Lbl", pill.transform, label, fontRegular, 9f, new Color(0.337f, 0.396f, 0.471f, 1f), TextAlignmentOptions.Center);
            var lRt = lblObj.GetComponent<RectTransform>();
            SetAnchors(lRt, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            lRt.offsetMin = new Vector2(2f, 0f);
            lRt.offsetMax = new Vector2(-2f, -3f);

            var valObj = CreateText("Txt_Val", pill.transform, val, fontBold, 15f, valColor, TextAlignmentOptions.Center);
            var vRt = valObj.GetComponent<RectTransform>();
            SetAnchors(vRt, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f));
            vRt.offsetMin = new Vector2(2f, 3f);
            vRt.offsetMax = new Vector2(-2f, 0f);
            var tmpVal = valObj.GetComponent<TextMeshProUGUI>();
            if (tmpVal != null)
            {
                tmpVal.enableAutoSizing = true;
                tmpVal.fontSizeMin = 9f;
                tmpVal.fontSizeMax = 15f;
                tmpVal.enableWordWrapping = false;
            }

            return pill.transform;
        }

        private static void CreateTimelineBlock(Transform timelineBar, Sprite sprRounded, Color color, float startNorm, float endNorm)
        {
            GameObject seg = CreateImage("Seg_" + startNorm.ToString("F2"), timelineBar, sprRounded, color, 3f);
            var rt = seg.GetComponent<RectTransform>();
            SetAnchors(rt, new Vector2(startNorm, 0f), new Vector2(endNorm, 1f), new Vector2(0f, 0.5f));
            rt.offsetMin = new Vector2(1f, 1f);
            rt.offsetMax = new Vector2(-1f, -1f);
            seg.AddComponent<Button>();
        }

        #endregion

        #region 4. Lessons Screen Button

        private static void BuildLessonsScreenButton(Transform canvasTrans, Sprite sprRounded, TMP_FontAsset fontBold, TMP_FontAsset fontRegular, VideoImportModalController modalCtrl, VideoAnalysisLoadingController loadCtrl)
        {
            Transform lessonsTrans = canvasTrans.Find("LessonsScreen");
            if (lessonsTrans == null) return;

            if (PrefabUtility.IsPartOfPrefabInstance(lessonsTrans.gameObject))
            {
                PrefabUtility.UnpackPrefabInstance(lessonsTrans.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }

            // Xoá tất cả các nút Evaluate cũ trong LessonsScreen để làm sạch hoàn toàn
            var all = lessonsTrans.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (t != null && t.name.Contains("Evaluate"))
                {
                    UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
            }

            // Tìm thẻ bài học đầu tiên (LessonCard_HipHopBasic có nút "Tiếp tục")
            Transform lessonCard = lessonsTrans.Find("ScrollView/Viewport/Content/LessonList/LessonCard_HipHopBasic");
            if (lessonCard == null) lessonCard = lessonsTrans.Find("Scroll/Viewport/Content/LessonList/LessonCard_HipHopBasic");
            if (lessonCard == null)
            {
                foreach (var c in all)
                {
                    if (c.name.Contains("HipHopBasic")) { lessonCard = c; break; }
                }
            }

            if (lessonCard == null)
            {
                Debug.LogWarning("[BuildImportEvaluationUI] Không tìm thấy LessonCard_HipHopBasic trong LessonsScreen!");
                return;
            }

            Transform infoTrans = lessonCard.Find("Info");
            if (infoTrans == null) infoTrans = lessonCard;

            for (int i = infoTrans.childCount - 1; i >= 0; i--)
            {
                var child = infoTrans.GetChild(i);
                if (child.name.Contains("Evaluate"))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            // Thiết lập kích thước thẻ vừa vặn
            var lcRt = lessonCard.GetComponent<RectTransform>();
            if (lcRt != null) lcRt.sizeDelta = new Vector2(400f, 185f);

            var lcLe = lessonCard.GetComponent<LayoutElement>();
            if (lcLe == null) lcLe = lessonCard.gameObject.AddComponent<LayoutElement>();
            lcLe.minHeight = 185f;
            lcLe.preferredHeight = 185f;

            var thumb = lessonCard.Find("Thumb");
            if (thumb != null)
            {
                var tRt = thumb.GetComponent<RectTransform>();
                tRt.anchoredPosition = new Vector2(57f, -92.5f);
            }

            var infRt = infoTrans.GetComponent<RectTransform>();
            if (infRt != null)
            {
                infRt.anchoredPosition = new Vector2(250f, -92.5f);
                infRt.sizeDelta = new Vector2(274f, 162f);
            }

            var vlg = infoTrans.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.spacing = 5f;
                vlg.padding = new RectOffset(0, 0, 0, 0);
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
            }

            var btnSlot = infoTrans.Find("ButtonSlot");
            if (btnSlot != null)
            {
                var bsRt = btnSlot.GetComponent<RectTransform>();
                bsRt.sizeDelta = new Vector2(274f, 34f);
                var bsLe = btnSlot.GetComponent<LayoutElement>();
                if (bsLe != null)
                {
                    bsLe.minHeight = 34f;
                    bsLe.preferredHeight = 34f;
                }
                var startBtn = btnSlot.Find("StartButton");
                if (startBtn != null)
                {
                    var sbRt = startBtn.GetComponent<RectTransform>();
                    sbRt.sizeDelta = new Vector2(0f, 34f);
                }
            }

            // Nút "Đánh giá video có sẵn" đặt trực tiếp bên dưới nút "Tiếp tục" trong Info
            GameObject btnEval = CreateButtonWithText(
                "Button_EvaluateExistingVideo",
                infoTrans,
                sprRounded,
                new Color(0.035f, 0.12f, 0.22f, 1f),
                "►  ĐÁNH GIÁ VIDEO CÓ SẴN",
                fontBold,
                11f,
                ColorCyan,
                3f
            );

            var beRt = btnEval.GetComponent<RectTransform>();
            beRt.sizeDelta = new Vector2(274f, 34f);

            var le = btnEval.AddComponent<LayoutElement>();
            le.minHeight = 34f;
            le.preferredHeight = 34f;
            le.flexibleWidth = 1f;

            // KHÔNG tạo "Border" child gây tràn cyan 374x140px!

            // Đảm bảo LessonScreenController có trên LessonsScreen và liên kết đầy đủ
            var lessonCtrl = lessonsTrans.GetComponent<LessonScreenController>();
            if (lessonCtrl == null) lessonCtrl = lessonsTrans.gameObject.AddComponent<LessonScreenController>();

            var so = new SerializedObject(lessonCtrl);
            so.FindProperty("btnEvaluateExistingVideo").objectReferenceValue = btnEval.GetComponent<Button>();
            if (modalCtrl != null) so.FindProperty("importModalController").objectReferenceValue = modalCtrl;
            if (loadCtrl != null) so.FindProperty("loadingController").objectReferenceValue = loadCtrl;
            so.ApplyModifiedProperties();
            if (loadCtrl != null) so.FindProperty("loadingController").objectReferenceValue = loadCtrl;
            so.ApplyModifiedProperties();
        }

        #endregion

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
            var colors = btn.colors;
            colors.highlightedColor = color * 1.15f;
            colors.pressedColor = color * 0.85f;
            btn.colors = colors;
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

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
        }

        private static void SaveAllPrefabs(GameObject canvas)
        {
            Transform modal = canvas.transform.Find("VideoImportModal");
            if (modal != null)
            {
                PrefabUtility.SaveAsPrefabAsset(modal.gameObject, "Assets/_Project/Prefabs/UI/VideoImportModal.prefab");
            }

            Transform loading = canvas.transform.Find("VideoAnalysisLoadingScreen");
            if (loading != null)
            {
                PrefabUtility.SaveAsPrefabAsset(loading.gameObject, "Assets/_Project/Prefabs/UI/VideoAnalysisLoadingScreen.prefab");
            }

            Transform dCoach = canvas.transform.Find("D_coach");
            if (dCoach != null)
            {
                PrefabUtility.SaveAsPrefabAsset(dCoach.gameObject, "Assets/_Project/Prefabs/UI/D_coach.prefab");
                PrefabUtility.SaveAsPrefabAsset(dCoach.gameObject, "Assets/_Project/Prefabs/UI/SessionEvaluationScreen.prefab");
                PrefabUtility.SaveAsPrefabAsset(dCoach.gameObject, "Assets/Resources/SessionEvaluationScreen.prefab");
                PrefabUtility.SaveAsPrefabAsset(dCoach.gameObject, "Assets/Resources/D_coach.prefab");
            }

            Transform lessons = canvas.transform.Find("LessonsScreen");
            if (lessons != null)
            {
                PrefabUtility.SaveAsPrefabAsset(lessons.gameObject, "Assets/_Project/Prefabs/UI/LessonsScreen.prefab");
            }
        }

        #endregion
    }
}
#endif
