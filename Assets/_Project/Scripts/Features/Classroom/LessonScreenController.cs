using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using DanceUp.Core;

namespace DanceUp.Features.Classroom
{
    /// <summary>
    /// Điều khiển màn hình LessonScreen: Nhận video bài học từ Inspector và kích hoạt phiên tập.
    /// </summary>
    public class LessonScreenController : MonoBehaviour
    {
        [Header("=== CẤU HÌNH BÀI HỌC (INSPECTOR) ===")]
        [SerializeField] private string lessonTitle = "HIP HOP BASIC • ARM GROOVE";
        [SerializeField] private VideoClip referenceVideoClip;

        [Header("=== UI COMPONENTS ===")]
        [SerializeField] private Button btnStart;
        [SerializeField] private TMP_Text txtErrorMessage;

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
                btnStart.onClick.AddListener(OnStartLessonClicked);
            }
        }

        public void SetReferenceVideo(VideoClip clip)
        {
            referenceVideoClip = clip;
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
