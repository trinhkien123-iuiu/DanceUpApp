using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using DanceUp.Core;
using DanceUp.Features.ImportEvaluation;

namespace DanceUp.Features.Classroom
{
    /// <summary>
    /// Điều khiển màn hình LessonScreen: Nhận video bài học từ Inspector, kích hoạt phiên tập trực tiếp,
    /// và hỗ trợ tính năng Đánh giá video bài nhảy ngoại tuyến (Import Video Evaluation).
    /// </summary>
    public class LessonScreenController : MonoBehaviour
    {
        [Header("=== CẤU HÌNH BÀI HỌC (INSPECTOR) ===")]
        [SerializeField] private string lessonTitle = "HIP HOP BASIC • ARM GROOVE";
        [SerializeField] private VideoClip referenceVideoClip;

        [Header("=== UI COMPONENTS ===")]
        [SerializeField] private Button btnStart;
        [SerializeField] private TMP_Text txtErrorMessage;

        [Header("=== NHẬP & ĐÁNH GIÁ VIDEO NGOẠI TUYẾN ===")]
        [SerializeField] private Button btnEvaluateExistingVideo;
        [SerializeField] private VideoImportModalController importModalController;
        [SerializeField] private VideoAnalysisLoadingController loadingController;

        private void Awake()
        {
            if (btnStart == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                foreach (var b in buttons)
                {
                    if (b.name.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        btnStart = b;
                        break;
                    }
                }
            }

            if (btnEvaluateExistingVideo == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                foreach (var b in buttons)
                {
                    if (b.name.IndexOf("Evaluate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        b.name.IndexOf("Import", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        btnEvaluateExistingVideo = b;
                        break;
                    }
                }
            }

            if (importModalController == null)
            {
                importModalController = FindObjectOfType<VideoImportModalController>(true);
            }

            if (loadingController == null)
            {
                loadingController = FindObjectOfType<VideoAnalysisLoadingController>(true);
            }

            if (referenceVideoClip == null)
            {
                var clips = Resources.FindObjectsOfTypeAll<VideoClip>();
                foreach (var c in clips)
                {
                    if (c.name.IndexOf("teacher", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        referenceVideoClip = c;
                        break;
                    }
                }
            }

            if (btnStart != null)
            {
                btnStart.onClick.RemoveAllListeners();
                btnStart.onClick.AddListener(OnStartLessonClicked);
            }

            if (btnEvaluateExistingVideo != null)
            {
                btnEvaluateExistingVideo.onClick.RemoveAllListeners();
                btnEvaluateExistingVideo.onClick.AddListener(OnEvaluateExistingVideoClicked);
            }

            if (importModalController != null && loadingController != null)
            {
                importModalController.OnStartAnalysisRequested -= HandleStartAnalysisRequested;
                importModalController.OnStartAnalysisRequested += HandleStartAnalysisRequested;
            }
        }

        private void OnDestroy()
        {
            if (importModalController != null)
            {
                importModalController.OnStartAnalysisRequested -= HandleStartAnalysisRequested;
            }
        }

        public void SetReferenceVideo(VideoClip clip)
        {
            referenceVideoClip = clip;
        }

        private void OnEvaluateExistingVideoClicked()
        {
            if (importModalController == null)
            {
                importModalController = FindObjectOfType<VideoImportModalController>(true);
            }

            if (importModalController != null)
            {
                importModalController.OpenModal(lessonTitle, referenceVideoClip);
            }
            else
            {
                Debug.LogWarning("[LessonScreenController] VideoImportModalController chưa được thiết lập trong Scene!");
            }
        }

        private void HandleStartAnalysisRequested(string videoPath, VideoClip teacherClip, string title)
        {
            if (loadingController == null)
            {
                loadingController = FindObjectOfType<VideoAnalysisLoadingController>(true);
            }

            if (loadingController != null)
            {
                loadingController.StartAnalysis(videoPath, teacherClip, title);
            }
            else
            {
                Debug.LogWarning("[LessonScreenController] VideoAnalysisLoadingController chưa được thiết lập trong Scene!");
            }
        }

        private void OnStartLessonClicked()
        {
            if (referenceVideoClip == null)
            {
                string errMsg = "Vui lòng gán Reference Video trong Unity Inspector trước khi bắt đầu!";
                Debug.LogError("[LessonScreenController] " + errMsg);
                if (txtErrorMessage == null)
                {
                    var toast = new GameObject("Toast_Error", typeof(RectTransform), typeof(Image), typeof(TextMeshProUGUI));
                    toast.transform.SetParent(transform, false);
                    var tRect = toast.GetComponent<RectTransform>();
                    tRect.anchorMin = new Vector2(0.1f, 0.15f);
                    tRect.anchorMax = new Vector2(0.9f, 0.23f);
                    tRect.offsetMin = Vector2.zero;
                    tRect.offsetMax = Vector2.zero;
                    toast.GetComponent<Image>().color = new Color(0.85f, 0.15f, 0.15f, 0.95f);
                    txtErrorMessage = toast.GetComponent<TextMeshProUGUI>();
                    txtErrorMessage.alignment = TextAlignmentOptions.Center;
                    txtErrorMessage.fontSize = 14;
                    txtErrorMessage.color = Color.white;
                }
                txtErrorMessage.text = errMsg;
                txtErrorMessage.gameObject.SetActive(true);
                return;
            }

            if (txtErrorMessage != null)
            {
                txtErrorMessage.gameObject.SetActive(false);
            }

            if (DanceSessionManager.Instance != null)
            {
                DanceSessionManager.Instance.StartLessonSession(referenceVideoClip, lessonTitle);
            }
            else
            {
                Debug.LogWarning("[LessonScreenController] DanceSessionManager chưa được khởi tạo trong Scene!");
            }
        }
    }
}
