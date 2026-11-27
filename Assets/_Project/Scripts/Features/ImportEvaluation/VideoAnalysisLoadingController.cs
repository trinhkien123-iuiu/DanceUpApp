using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using DanceUp.Core;
using DanceUp.Data;

namespace DanceUp.Features.ImportEvaluation
{
    /// <summary>
    /// Điều khiển màn hình Loading khi đang chạy phân tích MediaPipe và DTW trên video ngoại tuyến.
    /// </summary>
    public class VideoAnalysisLoadingController : MonoBehaviour
    {
        [Header("=== HEADER ===")]
        [SerializeField] private TMP_Text txtTitle;
        [SerializeField] private TMP_Text txtSubtitle;
        [SerializeField] private Button btnCancel;

        [Header("=== PROGRESS DISPLAY ===")]
        [SerializeField] private Slider sliderProgress;
        [SerializeField] private TMP_Text txtProgressPercent;
        [SerializeField] private TMP_Text txtStatusMessage;

        [Header("=== STEP INDICATORS ===")]
        [SerializeField] private TMP_Text txtStep1;
        [SerializeField] private TMP_Text txtStep2;
        [SerializeField] private TMP_Text txtStep3;
        [SerializeField] private TMP_Text txtStep4;

        public event Action OnAnalysisCancelled;

        private Color colorActive = new Color(0f, 0.706f, 1f, 1f); // Cyan
        private Color colorDone = new Color(0.1f, 0.95f, 0.45f, 1f);   // Green
        private Color colorWaiting = new Color(0.5f, 0.6f, 0.7f, 0.6f);

        private bool isCancelled = false;

        private void Awake()
        {
            if (btnCancel != null)
            {
                btnCancel.onClick.RemoveAllListeners();
                btnCancel.onClick.AddListener(CancelAnalysis);
            }
        }

        public void StartAnalysis(string videoPath, VideoClip teacherClip, string lessonTitle, string referenceVideoPath = "")
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            isCancelled = false;

            if (txtSubtitle != null)
            {
                string fName = System.IO.Path.GetFileName(videoPath);
                string refName = !string.IsNullOrEmpty(referenceVideoPath)
                    ? System.IO.Path.GetFileName(referenceVideoPath)
                    : (teacherClip != null ? teacherClip.name : "Mẫu chuẩn");
                txtSubtitle.text = $"{refName} vs {fName}";
            }

            UpdateProgress(0f, "Đang khởi tạo bộ phân tích MediaPipe...");

            VideoAnalysisService.Instance.StartVideoAnalysis(
                videoPath,
                teacherClip,
                lessonTitle,
                (progress, status) =>
                {
                    if (isCancelled) return;
                    UpdateProgress(progress, status);
                },
                (sessionData) =>
                {
                    if (isCancelled) return;
                    gameObject.SetActive(false);
                    // Mở D_coach với sessionData đã phân tích
                    if (DanceSessionManager.Instance != null)
                    {
                        DanceSessionManager.Instance.DisplayImportedVideoResults(sessionData);
                    }
                },
                (error) =>
                {
                    if (isCancelled) return;
                    Debug.LogError("[VideoAnalysisLoading] Analysis error: " + error);
                    UpdateProgress(0f, "Lỗi: " + error);
                },
                referenceVideoPath
            );
        }

        private void UpdateProgress(float progress, string status)
        {
            if (sliderProgress != null)
            {
                sliderProgress.value = Mathf.Clamp01(progress);
            }

            if (txtProgressPercent != null)
            {
                txtProgressPercent.text = Mathf.RoundToInt(progress * 100f) + "%";
            }

            if (txtStatusMessage != null)
            {
                txtStatusMessage.text = status;
            }

            // Cập nhật màu 4 bước theo tiến trình
            UpdateStepColor(txtStep1, progress >= 0.15f, progress < 0.15f && progress > 0f);
            UpdateStepColor(txtStep2, progress >= 0.65f, progress < 0.65f && progress >= 0.15f);
            UpdateStepColor(txtStep3, progress >= 0.85f, progress < 0.85f && progress >= 0.65f);
            UpdateStepColor(txtStep4, progress >= 0.99f, progress < 0.99f && progress >= 0.85f);
        }

        private void UpdateStepColor(TMP_Text stepTxt, bool isDone, bool isActive)
        {
            if (stepTxt == null) return;
            if (isDone)
            {
                stepTxt.color = colorDone;
            }
            else if (isActive)
            {
                stepTxt.color = colorActive;
            }
            else
            {
                stepTxt.color = colorWaiting;
            }
        }

        public void CancelAnalysis()
        {
            isCancelled = true;
            gameObject.SetActive(false);
            OnAnalysisCancelled?.Invoke();
        }
    }
}
