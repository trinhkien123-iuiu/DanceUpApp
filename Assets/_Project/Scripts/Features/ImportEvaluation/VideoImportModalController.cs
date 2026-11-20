using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace DanceUp.Features.ImportEvaluation
{
    /// <summary>
    /// Điều khiển Pop-up Modal chọn hoặc nhập video bài nhảy từ máy tính/dự án để đánh giá ngoại tuyến.
    /// </summary>
    public class VideoImportModalController : MonoBehaviour
    {
        [Header("=== HEADER & CONTROLS ===")]
        [SerializeField] private Button btnClose;
        [SerializeField] private TMP_Text txtTitle;

        [Header("=== VIDEO OPTIONS ===")]
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

        private string currentSelectedVideoPath = "";

        private void Awake()
        {
            BindButtons();
            // Mặc định chọn video mẫu 1_teacher.mp4 nếu có
            SelectSampleVideoDefault();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(currentSelectedVideoPath))
            {
                SelectSampleVideoDefault();
            }
        }

        private void BindButtons()
        {
            if (btnClose != null)
            {
                btnClose.onClick.RemoveAllListeners();
                btnClose.onClick.AddListener(CloseModal);
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

            SelectSampleVideoDefault();
        }

        public void CloseModal()
        {
            gameObject.SetActive(false);
        }

        private void SelectSampleVideoDefault()
        {
            string samplePath = Path.Combine(Application.dataPath, "_Project/Video/1_teacher.mp4");
            if (File.Exists(samplePath))
            {
                SetSelectedVideo(samplePath, "1_teacher.mp4 (Video mẫu kiểm thử)", "12.3s • 720×1280 • 30 FPS");
            }
            else
            {
                SetSelectedVideo("", "Chưa chọn video nào", "Vui lòng chọn video mẫu hoặc tải tệp từ máy tính");
            }
        }

        private void BrowseFileFromDisk()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Chọn video bài nhảy", "", "mp4,mov,avi");
            if (!string.IsNullOrEmpty(path))
            {
                string fName = Path.GetFileName(path);
                var fInfo = new FileInfo(path);
                float mb = fInfo.Length / (1024f * 1024f);
                SetSelectedVideo(path, fName, $"Tệp máy tính • {mb:F1} MB • Sẵn sàng");
            }
#else
            // Runtime fallback
            SelectSampleVideoDefault();
#endif
        }

        private void SetSelectedVideo(string path, string name, string meta)
        {
            currentSelectedVideoPath = path;

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
            OnStartAnalysisRequested?.Invoke(currentSelectedVideoPath, defaultTeacherClip, lessonTitle);
        }
    }
}
