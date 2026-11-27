using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace DanceUp.Features.ImportEvaluation
{
    /// <summary>
    /// Điều khiển Pop-up Modal chọn/nhập CẢ 2 VIDEO:
    /// 1. Video mẫu đối chiếu (Reference Video - mặc định 1_teacher.mp4 hoặc chọn file tùy ý từ máy)
    /// 2. Video bài nhảy cần đánh giá (Student Video - chọn video kiểm thử hoặc chọn file từ máy)
    /// </summary>
    public class VideoImportModalController : MonoBehaviour
    {
        [Header("=== HEADER & CONTROLS ===")]
        [SerializeField] private Button btnClose;
        [SerializeField] private TMP_Text txtTitle;

        [Header("=== REFERENCE VIDEO OPTIONS (VIDEO MẪU) ===")]
        [SerializeField] private Button btnSelectDefaultRef;
        [SerializeField] private Button btnBrowseRefFile;
        [SerializeField] private TMP_Text txtRefVideoName;
        [SerializeField] private TMP_Text txtRefVideoMeta;

        [Header("=== STUDENT VIDEO OPTIONS (VIDEO CẦN ĐÁNH GIÁ) ===")]
        [SerializeField] private Button btnSelectSample;
        [SerializeField] private Button btnBrowseFile;
        [SerializeField] private TMP_Text txtSelectedVideoName;
        [SerializeField] private TMP_Text txtSelectedVideoMeta;

        [Header("=== ACTION BUTTONS ===")]
        [SerializeField] private Button btnStartAnalysis;
        [SerializeField] private TMP_Text txtBtnStart;

        [Header("=== REFERENCE LESSON ===")]
        [SerializeField] private VideoClip defaultTeacherClip;
        [SerializeField] private string lessonTitle = "HIP HOP BASIC • ARM GROOVE";

        public event Action<string, VideoClip, string> OnStartAnalysisRequested;
        public event Action<string, string, VideoClip, string> OnStartDualAnalysisRequested;

        private string currentReferenceVideoPath = "";
        private string currentSelectedVideoPath = "";
        private bool dualUiInitialized = false;

        private void Awake()
        {
            EnsureDualReferenceUI();
            BindButtons();
            SelectDefaultReferenceVideo();
            SelectSampleVideoDefault();
        }

        private void OnEnable()
        {
            EnsureDualReferenceUI();
            BindButtons();
            if (string.IsNullOrEmpty(currentReferenceVideoPath))
            {
                SelectDefaultReferenceVideo();
            }
            if (string.IsNullOrEmpty(currentSelectedVideoPath))
            {
                SelectSampleVideoDefault();
            }
        }

        /// <summary>
        /// Tự động bổ sung vùng chọn Video Mẫu trên Modal nếu Prefab đang dùng bản 1-video cũ,
        /// đảm bảo giao diện luôn gọn gàng và không chồng chéo.
        /// </summary>
        private void EnsureDualReferenceUI()
        {
            if (dualUiInitialized && btnBrowseRefFile != null) return;

            var cardObj = transform.Find("DialogCard");
            var contentTrans = transform.Find("DialogCard/Content");
            if (cardObj == null || contentTrans == null) return;

            var cardRt = cardObj.GetComponent<RectTransform>();
            if (cardRt != null && cardRt.sizeDelta.y < 540f)
            {
                cardRt.sizeDelta = new Vector2(Mathf.Max(cardRt.sizeDelta.x, 368f), 556f);
            }

            // Cập nhật nhãn nút mẫu học viên để phân biệt rõ với video mẫu giáo viên
            if (btnSelectSample != null)
            {
                var lbl = btnSelectSample.transform.Find("Label")?.GetComponent<TMP_Text>();
                var meta = btnSelectSample.transform.Find("Meta")?.GetComponent<TMP_Text>();
                if (lbl != null) lbl.text = "Dùng Video Bài Nhảy Thử Nghiệm";
                if (meta != null) meta.text = "1_student.mp4 / 2_student.mp4 • Sẵn trong project";
            }

            var existingRefGroup = contentTrans.Find("RefVideoSection");
            if (existingRefGroup != null)
            {
                btnSelectDefaultRef = existingRefGroup.Find("BtnRow/Btn_DefaultRef")?.GetComponent<Button>();
                btnBrowseRefFile = existingRefGroup.Find("BtnRow/Btn_BrowseRef")?.GetComponent<Button>();
                txtRefVideoName = existingRefGroup.Find("RefSelectedBox/Txt_RefName")?.GetComponent<TMP_Text>();
                txtRefVideoMeta = existingRefGroup.Find("RefSelectedBox/Txt_RefMeta")?.GetComponent<TMP_Text>();
                dualUiInitialized = true;
                return;
            }

            if (btnBrowseRefFile != null && txtRefVideoName != null)
            {
                dualUiInitialized = true;
                return;
            }

            // Tạo RefVideoSection ngay trên cùng của Content dựa theo template hiện có
            TMP_FontAsset fontBold = txtTitle != null ? txtTitle.font : null;
            TMP_FontAsset fontRegular = txtSelectedVideoMeta != null ? txtSelectedVideoMeta.font : fontBold;
            Sprite sprRounded = btnBrowseFile != null ? btnBrowseFile.GetComponent<Image>()?.sprite : null;

            var refSection = new GameObject("RefVideoSection", typeof(RectTransform));
            refSection.transform.SetParent(contentTrans, false);
            refSection.transform.SetSiblingIndex(0);
            var refLe = refSection.AddComponent<LayoutElement>();
            refLe.minHeight = 114f;
            refLe.preferredHeight = 114f;

            var refVlg = refSection.AddComponent<VerticalLayoutGroup>();
            refVlg.spacing = 6f;
            refVlg.childControlWidth = true;
            refVlg.childControlHeight = false;
            refVlg.childForceExpandWidth = true;
            refVlg.childForceExpandHeight = false;

            // Tiêu đề mục 1
            var lblSection1 = CreateLabel("Txt_RefHeader", refSection.transform, "1. VIDEO MẪU ĐỐI CHIẾU (GIÁO VIÊN)",
                fontBold, 10.5f, new Color(0f, 0.706f, 1f, 1f), 16f);

            // Hàng 2 nút: Mặc định 1_teacher | Chọn file khác
            var btnRow = new GameObject("BtnRow", typeof(RectTransform));
            btnRow.transform.SetParent(refSection.transform, false);
            var rowLe = btnRow.AddComponent<LayoutElement>();
            rowLe.minHeight = 36f;
            rowLe.preferredHeight = 36f;
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            btnSelectDefaultRef = CreateCompactButton("Btn_DefaultRef", btnRow.transform, sprRounded,
                new Color(0.08f, 0.15f, 0.25f, 1f), "Mẫu sẵn (1_teacher)", fontBold, 11f, Color.white);
            btnBrowseRefFile = CreateCompactButton("Btn_BrowseRef", btnRow.transform, sprRounded,
                new Color(0.06f, 0.22f, 0.34f, 1f), "Chọn Video Mẫu...", fontBold, 11f, new Color(0f, 0.85f, 1f, 1f));

            // Box hiển thị Video mẫu đang chọn
            var refBox = new GameObject("RefSelectedBox", typeof(RectTransform), typeof(Image));
            refBox.transform.SetParent(refSection.transform, false);
            var boxImg = refBox.GetComponent<Image>();
            boxImg.sprite = sprRounded;
            if (sprRounded != null) { boxImg.type = Image.Type.Sliced; boxImg.pixelsPerUnitMultiplier = 3f; }
            boxImg.color = new Color(0.04f, 0.08f, 0.14f, 1f);
            var boxLe = refBox.AddComponent<LayoutElement>();
            boxLe.minHeight = 46f;
            boxLe.preferredHeight = 46f;

            txtRefVideoName = CreateAnchoredLabel("Txt_RefName", refBox.transform, "1_teacher.mp4 (Mẫu mặc định)",
                fontBold, 11f, new Color(0f, 0.706f, 1f, 1f), new Vector2(12f, 8f));
            txtRefVideoMeta = CreateAnchoredLabel("Txt_RefMeta", refBox.transform, "12.3s • 30 FPS • Sẵn sàng đối chiếu",
                fontRegular, 9.5f, new Color(0.067f, 0.72f, 0.38f, 1f), new Vector2(12f, -9f));

            // Thêm tiêu đề mục 2 ngay trước Btn_SelectSample
            if (contentTrans.Find("Txt_StudentHeader") == null)
            {
                var lblSection2 = CreateLabel("Txt_StudentHeader", contentTrans, "2. VIDEO BÀI NHẢY CẦN ĐÁNH GIÁ (HỌC VIÊN)",
                    fontBold, 10.5f, new Color(0.067f, 0.72f, 0.38f, 1f), 18f);
                lblSection2.transform.SetSiblingIndex(1);
            }

            dualUiInitialized = true;
        }

        private static TMP_Text CreateLabel(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.BottomLeft;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static TMP_Text CreateAnchoredLabel(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(-24f, 18f);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateCompactButton(string name, Transform parent, Sprite sprite, Color bgColor, string label, TMP_FontAsset font, float fontSize, Color textColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = bgColor;
            if (sprite != null) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 3f; }

            var txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(go.transform, false);
            var rt = txtObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 0f);
            rt.offsetMax = new Vector2(-6f, 0f);
            var tmp = txtObj.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return go.GetComponent<Button>();
        }

        private void BindButtons()
        {
            if (btnClose != null)
            {
                btnClose.onClick.RemoveAllListeners();
                btnClose.onClick.AddListener(CloseModal);
            }

            if (btnSelectDefaultRef != null)
            {
                btnSelectDefaultRef.onClick.RemoveAllListeners();
                btnSelectDefaultRef.onClick.AddListener(SelectDefaultReferenceVideo);
            }

            if (btnBrowseRefFile != null)
            {
                btnBrowseRefFile.onClick.RemoveAllListeners();
                btnBrowseRefFile.onClick.AddListener(BrowseReferenceVideoFromDisk);
            }

            if (btnSelectSample != null)
            {
                btnSelectSample.onClick.RemoveAllListeners();
                btnSelectSample.onClick.AddListener(SelectSampleVideoDefault);
            }

            if (btnBrowseFile != null)
            {
                btnBrowseFile.onClick.RemoveAllListeners();
                btnBrowseFile.onClick.AddListener(BrowseFileFromDisk);
            }

            if (btnStartAnalysis != null)
            {
                btnStartAnalysis.onClick.RemoveAllListeners();
                btnStartAnalysis.onClick.AddListener(HandleStartAnalysis);
            }
        }

        public void OpenModal(string title = "HIP HOP BASIC • ARM GROOVE", VideoClip teacherClip = null)
        {
            if (!string.IsNullOrEmpty(title)) lessonTitle = title;
            if (teacherClip != null) defaultTeacherClip = teacherClip;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            EnsureDualReferenceUI();
            BindButtons();

            if (string.IsNullOrEmpty(currentReferenceVideoPath))
            {
                SelectDefaultReferenceVideo();
            }
            if (string.IsNullOrEmpty(currentSelectedVideoPath))
            {
                SelectSampleVideoDefault();
            }
        }

        public void CloseModal()
        {
            gameObject.SetActive(false);
        }

        private void SelectDefaultReferenceVideo()
        {
            string teacherSamplePath = Path.Combine(Application.dataPath, "_Project/Video/1_teacher.mp4");
            if (File.Exists(teacherSamplePath))
            {
                SetReferenceVideo(teacherSamplePath, "1_teacher.mp4 (Mẫu mặc định)", "12.3s • 720×1280 • Sẵn sàng đối chiếu");
            }
            else
            {
                SetReferenceVideo("", "Dữ liệu mẫu chuẩn (1_teacher_pose)", "Nạp sẵn trong StreamingAssets");
            }
        }

        private void BrowseReferenceVideoFromDisk()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Chọn VIDEO MẪU (Giáo viên)", "", "mp4,mov,avi");
            if (!string.IsNullOrEmpty(path))
            {
                string fName = Path.GetFileName(path);
                var fInfo = new FileInfo(path);
                float mb = fInfo.Length / (1024f * 1024f);
                SetReferenceVideo(path, fName + " (Video mẫu tùy chọn)", $"Tệp máy tính • {mb:F1} MB • Sẽ trích xuất pose AI");
            }
#else
            SelectDefaultReferenceVideo();
#endif
        }

        private void SelectSampleVideoDefault()
        {
            string student1 = Path.Combine(Application.dataPath, "_Project/Video/1_student.mp4");
            string student2 = Path.Combine(Application.dataPath, "_Project/Video/2_student.mp4");
            string teacherFallback = Path.Combine(Application.dataPath, "_Project/Video/1_teacher.mp4");

            // Cho phép bấm lặp để luân phiên giữa 1_student.mp4 và 2_student.mp4 khi kiểm thử nhanh
            string chosen = "";
            string label = "";
            if (File.Exists(student1) && !string.Equals(currentSelectedVideoPath, student1, StringComparison.OrdinalIgnoreCase))
            {
                chosen = student1;
                label = "1_student.mp4 (Video học viên #1)";
            }
            else if (File.Exists(student2))
            {
                chosen = student2;
                label = "2_student.mp4 (Video học viên #2)";
            }
            else if (File.Exists(student1))
            {
                chosen = student1;
                label = "1_student.mp4 (Video học viên #1)";
            }
            else if (File.Exists(teacherFallback))
            {
                chosen = teacherFallback;
                label = "1_teacher.mp4 (Video mẫu kiểm thử)";
            }

            if (!string.IsNullOrEmpty(chosen))
            {
                var fInfo = new FileInfo(chosen);
                float mb = fInfo.Length / (1024f * 1024f);
                SetSelectedVideo(chosen, label, $"Video trong project • {mb:F1} MB • Sẵn sàng");
            }
            else
            {
                SetSelectedVideo("", "Chưa chọn video nào", "Vui lòng chọn video mẫu hoặc tải tệp từ máy tính");
            }
        }

        private void BrowseFileFromDisk()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Chọn VIDEO BÀI NHẢY cần đánh giá", "", "mp4,mov,avi");
            if (!string.IsNullOrEmpty(path))
            {
                string fName = Path.GetFileName(path);
                var fInfo = new FileInfo(path);
                float mb = fInfo.Length / (1024f * 1024f);
                SetSelectedVideo(path, fName, $"Tệp máy tính • {mb:F1} MB • Sẵn sàng");
            }
#else
            SelectSampleVideoDefault();
#endif
        }

        private void SetReferenceVideo(string path, string name, string meta)
        {
            currentReferenceVideoPath = path ?? "";
            if (txtRefVideoName != null) txtRefVideoName.text = name;
            if (txtRefVideoMeta != null) txtRefVideoMeta.text = meta;
        }

        private void SetSelectedVideo(string path, string name, string meta)
        {
            currentSelectedVideoPath = path ?? "";

            if (txtSelectedVideoName != null) txtSelectedVideoName.text = name;
            if (txtSelectedVideoMeta != null) txtSelectedVideoMeta.text = meta;

            if (btnStartAnalysis != null)
            {
                bool canStart = !string.IsNullOrEmpty(currentSelectedVideoPath) && File.Exists(currentSelectedVideoPath);
                btnStartAnalysis.interactable = canStart;
            }
        }

        private void HandleStartAnalysis()
        {
            if (string.IsNullOrEmpty(currentSelectedVideoPath) || !File.Exists(currentSelectedVideoPath))
            {
                Debug.LogWarning("[VideoImportModal] Chưa có video hợp lệ được chọn!");
                return;
            }

            CloseModal();
            if (OnStartDualAnalysisRequested != null)
            {
                OnStartDualAnalysisRequested.Invoke(currentSelectedVideoPath, currentReferenceVideoPath, defaultTeacherClip, lessonTitle);
            }
            else
            {
                OnStartAnalysisRequested?.Invoke(currentSelectedVideoPath, defaultTeacherClip, lessonTitle);
            }
        }
    }
}
