#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using DanceUp.Features.Result;

namespace DanceUp.Editor
{
    public static class BuildCameraRecapUI
    {
        [MenuItem("DanceUp/Build Camera Recap UI Screen", false, 52)]
        public static void BuildScreen()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null)
            {
                var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
                if (canvases.Length > 0) canvas = canvases[0].gameObject;
            }

            if (canvas == null)
            {
                Debug.LogError("[BuildCameraRecapUI] Canvas không tìm thấy trong scene!");
                return;
            }

            // Xóa màn hình cũ nếu đã tồn tại
            var existing = canvas.transform.Find("CameraRecapScreen");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Fonts/SegoeUI-Bold SDF.asset");
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold");
            if (font == null) font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()[0];

            // 1. Tạo Root
            var recapObj = new GameObject("CameraRecapScreen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CameraRecapController));
            recapObj.transform.SetParent(canvas.transform, false);

            var rootRt = recapObj.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var rootImg = recapObj.GetComponent<Image>();
            rootImg.color = new Color(0.027f, 0.063f, 0.161f, 1f); // #071029

            var controller = recapObj.GetComponent<CameraRecapController>();

            // 2. Header
            var headerObj = CreateUIObject("Header", recapObj.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-32f, 56f));

            var btnCloseObj = CreateButton("Btn_Back", headerObj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Color(0.051f, 0.106f, 0.165f, 1f));
            var txtClose = CreateTMPText("Text", btnCloseObj.transform, "<", 20, Color.white, font, TextAlignmentOptions.Center);
            txtClose.rectTransform.anchorMin = Vector2.zero;
            txtClose.rectTransform.anchorMax = Vector2.one;
            txtClose.rectTransform.sizeDelta = Vector2.zero;

            var titleCont = CreateUIObject("TitleContainer", headerObj.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(24f, 0f), new Vector2(-100f, 0f));
            var txtTitle = CreateTMPText("Text_Title", titleCont.transform, "Camera Recap", 18, Color.white, font, TextAlignmentOptions.Left);
            txtTitle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            txtTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            txtTitle.rectTransform.offsetMin = Vector2.zero;
            txtTitle.rectTransform.offsetMax = Vector2.zero;

            var txtSubtitle = CreateTMPText("Text_Subtitle", titleCont.transform, "HIP HOP BASIC • ARM GROOVE", 11, new Color(0f, 0.706f, 1f, 1f), font, TextAlignmentOptions.Left);
            txtSubtitle.rectTransform.anchorMin = new Vector2(0f, 0f);
            txtSubtitle.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            txtSubtitle.rectTransform.offsetMin = Vector2.zero;
            txtSubtitle.rectTransform.offsetMax = Vector2.zero;

            // 3. Dual Viewport Container (Side-by-side or stacked)
            // Kích thước chuẩn: Trên điện thoại di động dọc, 2 màn hình nằm ngang cạnh nhau (width mỗi bên ~175px, height ~245px)
            var viewportsCont = CreateUIObject("ViewportsContainer", recapObj.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(-32f, 290f));

            // 3A. Teacher Viewport (Trái)
            var vpTeacherObj = CreateUIObject("Viewport_Teacher", viewportsCont.transform, new Vector2(0f, 0f), new Vector2(0.485f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bgTeacher = vpTeacherObj.AddComponent<Image>();
            bgTeacher.color = new Color(0.051f, 0.106f, 0.165f, 1f); // #0D1B2A
            var maskTeacher = vpTeacherObj.AddComponent<RectMask2D>();

            var labelTeacherCont = CreateUIObject("HeaderTag", vpTeacherObj.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 26f));
            var txtTeacherLabel = CreateTMPText("Text_Label", labelTeacherCont.transform, "GIÁO VIÊN MẪU", 10, new Color(0f, 0.706f, 1f, 1f), font, TextAlignmentOptions.Center);
            txtTeacherLabel.rectTransform.anchorMin = Vector2.zero;
            txtTeacherLabel.rectTransform.anchorMax = Vector2.one;
            txtTeacherLabel.rectTransform.sizeDelta = Vector2.zero;

            var rawTeacherObj = new GameObject("RawImage_Teacher", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(VideoPlayer));
            rawTeacherObj.transform.SetParent(vpTeacherObj.transform, false);
            var rawTeacherRt = rawTeacherObj.GetComponent<RectTransform>();
            rawTeacherRt.anchorMin = new Vector2(0f, 0f);
            rawTeacherRt.anchorMax = new Vector2(1f, 1f);
            rawTeacherRt.offsetMin = new Vector2(4f, 4f);
            rawTeacherRt.offsetMax = new Vector2(-4f, -28f);

            var rawTeacherImg = rawTeacherObj.GetComponent<RawImage>();
            var vpTeacherComp = rawTeacherObj.GetComponent<VideoPlayer>();
            vpTeacherComp.playOnAwake = false;
            vpTeacherComp.waitForFirstFrame = true;

            // 3B. Student Viewport (Phải)
            var vpStudentObj = CreateUIObject("Viewport_Student", viewportsCont.transform, new Vector2(0.515f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bgStudent = vpStudentObj.AddComponent<Image>();
            bgStudent.color = new Color(0.051f, 0.106f, 0.165f, 1f);
            var maskStudent = vpStudentObj.AddComponent<RectMask2D>();

            var labelStudentCont = CreateUIObject("HeaderTag", vpStudentObj.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 26f));
            var txtStudentLabel = CreateTMPText("Text_Label", labelStudentCont.transform, "HỌC VIÊN SKELETON", 10, new Color(0.1f, 0.95f, 0.45f, 1f), font, TextAlignmentOptions.Center);
            txtStudentLabel.rectTransform.anchorMin = Vector2.zero;
            txtStudentLabel.rectTransform.anchorMax = Vector2.one;
            txtStudentLabel.rectTransform.sizeDelta = Vector2.zero;

            // Accuracy badge on top-right of student viewport
            var badgeCont = CreateUIObject("Badge_Accuracy", vpStudentObj.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -32f), new Vector2(75f, 20f));
            var badgeImg = badgeCont.AddComponent<Image>();
            badgeImg.color = new Color(0.1f, 0.95f, 0.45f, 0.2f);
            var txtAccBadge = CreateTMPText("Text_Acc", badgeCont.transform, "87% ĐẠT", 10, new Color(0.1f, 0.95f, 0.45f, 1f), font, TextAlignmentOptions.Center);
            txtAccBadge.rectTransform.anchorMin = Vector2.zero;
            txtAccBadge.rectTransform.anchorMax = Vector2.one;
            txtAccBadge.rectTransform.sizeDelta = Vector2.zero;

            var skelContObj = new GameObject("SkeletonContainer", typeof(RectTransform));
            skelContObj.transform.SetParent(vpStudentObj.transform, false);
            var skelRt = skelContObj.GetComponent<RectTransform>();
            skelRt.anchorMin = new Vector2(0f, 0f);
            skelRt.anchorMax = new Vector2(1f, 1f);
            skelRt.offsetMin = new Vector2(4f, 4f);
            skelRt.offsetMax = new Vector2(-4f, -28f);

            // 4. Coach Advice Banner
            var bannerObj = CreateUIObject("Banner_Coach", recapObj.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -382f), new Vector2(-32f, 75f));
            var bannerImg = bannerObj.AddComponent<Image>();
            bannerImg.color = new Color(0.051f, 0.106f, 0.165f, 1f);

            var segBadgeObj = CreateUIObject("Badge_Segment", bannerObj.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(210f, 20f));
            var segBadgeImg = segBadgeObj.AddComponent<Image>();
            segBadgeImg.color = new Color(0.96f, 0.62f, 0.04f, 0.25f); // Amber glow
            var txtSegBadge = CreateTMPText("Text_Badge", segBadgeObj.transform, "ĐOẠN CẦN CHỈNH (00:03 - 00:07)", 9, new Color(0.96f, 0.62f, 0.04f, 1f), font, TextAlignmentOptions.Center);
            txtSegBadge.rectTransform.anchorMin = Vector2.zero;
            txtSegBadge.rectTransform.anchorMax = Vector2.one;
            txtSegBadge.rectTransform.sizeDelta = Vector2.zero;

            var txtCoach = CreateTMPText("Text_CoachAdvice", bannerObj.transform, "Cổ tay trái: Chậm nửa nhịp so với giáo viên. Cần nâng cao góc 15°.", 11, Color.white, font, TextAlignmentOptions.Left);
            txtCoach.rectTransform.anchorMin = new Vector2(0f, 0f);
            txtCoach.rectTransform.anchorMax = new Vector2(1f, 1f);
            txtCoach.rectTransform.offsetMin = new Vector2(12f, 8f);
            txtCoach.rectTransform.offsetMax = new Vector2(-12f, -34f);

            // 5. Timeline Scrubber Section
            var timelineCont = CreateUIObject("TimelineSection", recapObj.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -470f), new Vector2(-32f, 60f));

            // Timestamps row
            var txtCur = CreateTMPText("Text_CurrentTime", timelineCont.transform, "00:00", 12, new Color(0f, 0.706f, 1f, 1f), font, TextAlignmentOptions.Left);
            txtCur.rectTransform.anchorMin = new Vector2(0f, 1f);
            txtCur.rectTransform.anchorMax = new Vector2(0f, 1f);
            txtCur.rectTransform.pivot = new Vector2(0f, 1f);
            txtCur.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            txtCur.rectTransform.sizeDelta = new Vector2(60f, 18f);

            var txtTot = CreateTMPText("Text_TotalTime", timelineCont.transform, "00:30", 12, new Color(0.6f, 0.7f, 0.8f, 1f), font, TextAlignmentOptions.Right);
            txtTot.rectTransform.anchorMin = new Vector2(1f, 1f);
            txtTot.rectTransform.anchorMax = new Vector2(1f, 1f);
            txtTot.rectTransform.pivot = new Vector2(1f, 1f);
            txtTot.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            txtTot.rectTransform.sizeDelta = new Vector2(60f, 18f);

            // Slider
            var sliderObj = new GameObject("Slider_Timeline", typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(timelineCont.transform, false);
            var sliderRt = sliderObj.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0f, 0f);
            sliderRt.anchorMax = new Vector2(1f, 0f);
            sliderRt.pivot = new Vector2(0.5f, 0f);
            sliderRt.anchoredPosition = new Vector2(0f, 6f);
            sliderRt.sizeDelta = new Vector2(0f, 26f);

            var sliderComp = sliderObj.GetComponent<Slider>();
            sliderComp.minValue = 0f;
            sliderComp.maxValue = 1f;
            sliderComp.value = 0f;

            // Background track
            var bgTrack = CreateUIObject("BackgroundTrack", sliderObj.transform, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var bgTrackImg = bgTrack.AddComponent<Image>();
            bgTrackImg.color = new Color(0.12f, 0.16f, 0.24f, 1f);

            // Problem Marker
            var probMarker = CreateUIObject("ProblemMarker", bgTrack.transform, new Vector2(0.1f, 0f), new Vector2(0.35f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var probMarkerImg = probMarker.AddComponent<Image>();
            probMarkerImg.color = new Color(0.96f, 0.62f, 0.04f, 0.85f);
            var probRt = probMarker.GetComponent<RectTransform>();

            // Fill Area
            var fillArea = CreateUIObject("Fill Area", sliderObj.transform, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var fill = CreateUIObject("Fill", fillArea.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0f, 0.706f, 1f, 1f);
            sliderComp.fillRect = fill.GetComponent<RectTransform>();

            // Handle Slide Area
            var handleArea = CreateUIObject("Handle Slide Area", sliderObj.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var handle = CreateUIObject("Handle", handleArea.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;
            sliderComp.handleRect = handle.GetComponent<RectTransform>();
            sliderComp.targetGraphic = handleImg;

            // 6. Playback Controls Row
            var controlsRow = CreateUIObject("ControlsRow", recapObj.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -545f), new Vector2(-32f, 65f));

            // Nút Rewind 5s
            var btnRewind = CreateButton("Btn_Rewind5s", controlsRow.transform, new Vector2(0.18f, 0.5f), new Vector2(0.18f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f), new Color(0.051f, 0.106f, 0.165f, 1f));
            var txtRew = CreateTMPText("Text", btnRewind.transform, "-5s", 13, Color.white, font, TextAlignmentOptions.Center);
            txtRew.rectTransform.anchorMin = Vector2.zero;
            txtRew.rectTransform.anchorMax = Vector2.one;
            txtRew.rectTransform.sizeDelta = Vector2.zero;

            // Nút Play/Pause chính giữa (56x56)
            var btnPlay = CreateButton("Btn_PlayPause", controlsRow.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f), new Color(0f, 0.706f, 1f, 1f));
            var imgPlayIcon = btnPlay.GetComponent<Image>();
            imgPlayIcon.color = new Color(0f, 0.706f, 1f, 1f);
            var txtPlayPause = CreateTMPText("Text_Icon", btnPlay.transform, "||", 16, new Color(0.027f, 0.063f, 0.161f, 1f), font, TextAlignmentOptions.Center);
            txtPlayPause.rectTransform.anchorMin = Vector2.zero;
            txtPlayPause.rectTransform.anchorMax = Vector2.one;
            txtPlayPause.rectTransform.sizeDelta = Vector2.zero;

            // Nút Forward 5s
            var btnFwd = CreateButton("Btn_Forward5s", controlsRow.transform, new Vector2(0.82f, 0.5f), new Vector2(0.82f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f), new Color(0.051f, 0.106f, 0.165f, 1f));
            var txtFwd = CreateTMPText("Text", btnFwd.transform, "+5s", 13, Color.white, font, TextAlignmentOptions.Center);
            txtFwd.rectTransform.anchorMin = Vector2.zero;
            txtFwd.rectTransform.anchorMax = Vector2.one;
            txtFwd.rectTransform.sizeDelta = Vector2.zero;

            // Nút Tốc độ Speed (Góc trái)
            var btnSpeed = CreateButton("Btn_Speed", controlsRow.transform, new Vector2(0.02f, 0.5f), new Vector2(0.02f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(42f, 36f), new Color(0.051f, 0.106f, 0.165f, 1f));
            var txtSpd = CreateTMPText("Text_Speed", btnSpeed.transform, "1×", 12, new Color(0f, 0.706f, 1f, 1f), font, TextAlignmentOptions.Center);
            txtSpd.rectTransform.anchorMin = Vector2.zero;
            txtSpd.rectTransform.anchorMax = Vector2.one;
            txtSpd.rectTransform.sizeDelta = Vector2.zero;

            // Nút Loop (Góc phải)
            var btnLoop = CreateButton("Btn_Loop", controlsRow.transform, new Vector2(0.98f, 0.5f), new Vector2(0.98f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(42f, 36f), new Color(0.051f, 0.106f, 0.165f, 1f));
            var txtLoop = CreateTMPText("Text_Loop", btnLoop.transform, "Lặp", 11, new Color(0f, 0.706f, 1f, 1f), font, TextAlignmentOptions.Center);
            txtLoop.rectTransform.anchorMin = Vector2.zero;
            txtLoop.rectTransform.anchorMax = Vector2.one;
            txtLoop.rectTransform.sizeDelta = Vector2.zero;

            // 7. Bottom Action Button ("Tập lại bài này")
            var btnRetryObj = CreateButton("Btn_RetryPractice", recapObj.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(-32f, 50f), new Color(0f, 0.706f, 1f, 1f));
            var txtRetry = CreateTMPText("Text", btnRetryObj.transform, "Luyện tập lại bài này", 14, new Color(0.027f, 0.063f, 0.161f, 1f), font, TextAlignmentOptions.Center);
            txtRetry.rectTransform.anchorMin = Vector2.zero;
            txtRetry.rectTransform.anchorMax = Vector2.one;
            txtRetry.rectTransform.sizeDelta = Vector2.zero;

            // Serialized Field Binding
            var so = new SerializedObject(controller);
            so.FindProperty("btnClose").objectReferenceValue = btnCloseObj.GetComponent<Button>();
            so.FindProperty("txtTitle").objectReferenceValue = txtTitle;
            so.FindProperty("txtSubtitle").objectReferenceValue = txtSubtitle;

            so.FindProperty("vpTeacher").objectReferenceValue = vpTeacherComp;
            so.FindProperty("imgTeacherVideo").objectReferenceValue = rawTeacherImg;
            so.FindProperty("txtTeacherLabel").objectReferenceValue = txtTeacherLabel;

            so.FindProperty("studentSkeletonContainer").objectReferenceValue = skelRt;
            so.FindProperty("txtStudentLabel").objectReferenceValue = txtStudentLabel;
            so.FindProperty("txtAccuracyBadge").objectReferenceValue = txtAccBadge;

            so.FindProperty("bannerCoach").objectReferenceValue = bannerObj;
            so.FindProperty("txtCoachAdvice").objectReferenceValue = txtCoach;
            so.FindProperty("txtSegmentBadge").objectReferenceValue = txtSegBadge;

            so.FindProperty("sliderTimeline").objectReferenceValue = sliderComp;
            so.FindProperty("txtCurrentTime").objectReferenceValue = txtCur;
            so.FindProperty("txtTotalTime").objectReferenceValue = txtTot;
            so.FindProperty("problemMarker").objectReferenceValue = probRt;

            so.FindProperty("btnRewind5s").objectReferenceValue = btnRewind.GetComponent<Button>();
            so.FindProperty("btnPlayPause").objectReferenceValue = btnPlay.GetComponent<Button>();
            so.FindProperty("txtPlayPauseIcon").objectReferenceValue = txtPlayPause;
            so.FindProperty("btnForward5s").objectReferenceValue = btnFwd.GetComponent<Button>();
            so.FindProperty("btnSpeed").objectReferenceValue = btnSpeed.GetComponent<Button>();
            so.FindProperty("txtSpeed").objectReferenceValue = txtSpd;
            so.FindProperty("btnLoop").objectReferenceValue = btnLoop.GetComponent<Button>();
            so.FindProperty("btnRetryPractice").objectReferenceValue = btnRetryObj.GetComponent<Button>();

            // Sprites
            var nodeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Common/UI_CircleNode.png");
            if (nodeSprite == null) nodeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/Practice/node.png");
            if (nodeSprite != null) so.FindProperty("jointSprite").objectReferenceValue = nodeSprite;

            var playSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/Practice/play.png");
            if (playSprite != null) so.FindProperty("iconPlay").objectReferenceValue = playSprite;

            var pauseSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Icons/Practice/pause.png");
            if (pauseSprite != null) so.FindProperty("iconPause").objectReferenceValue = pauseSprite;

            // Clip
            var teacherClips = Resources.FindObjectsOfTypeAll<VideoClip>();
            foreach (var c in teacherClips)
            {
                if (c.name.IndexOf("teacher", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    so.FindProperty("defaultTeacherClip").objectReferenceValue = c;
                    vpTeacherComp.clip = c;
                    break;
                }
            }

            so.ApplyModifiedProperties();

            // Lưu thành Prefab
            string prefabDir = "Assets/_Project/Prefabs/UI";
            if (!AssetDatabase.IsValidFolder(prefabDir))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
            }
            string prefabPath = $"{prefabDir}/CameraRecapScreen.prefab";
            PrefabUtility.SaveAsPrefabAsset(recapObj, prefabPath);

            // Mặc định ẩn CameraRecapScreen khi khởi động
            recapObj.SetActive(false);

            // Wire với DanceSessionManager
            var mgr = Object.FindObjectOfType<DanceUp.Core.DanceSessionManager>(true);
            if (mgr != null)
            {
                var mgrSo = new SerializedObject(mgr);
                var propRecap = mgrSo.FindProperty("cameraRecapScreen");
                if (propRecap != null)
                {
                    propRecap.objectReferenceValue = controller;
                    mgrSo.ApplyModifiedProperties();
                }
            }

            EditorUtility.SetDirty(recapObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(canvas.scene);

            Debug.Log($"[BuildCameraRecapUI] Xây dựng thành công CameraRecapScreen tại {prefabPath} và liên kết với DanceSessionManager!");
        }

        private static GameObject CreateUIObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = go.GetComponent<Image>();
            img.color = bgColor;
            return go;
        }

        private static TMP_Text CreateTMPText(string name, Transform parent, string text, float fontSize, Color color, TMP_FontAsset font, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            if (font != null) tmp.font = font;
            return tmp;
        }
    }
}
#endif
