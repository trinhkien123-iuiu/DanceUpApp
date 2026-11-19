using System;
using UnityEngine;
using UnityEngine.UI;

namespace DanceUp.CameraInput
{
    /// <summary>
    /// Điều khiển Webcam thời gian thực của học viên, xuất ra RawImage nền.
    /// Có cơ chế bắt lỗi và giải phóng bộ nhớ khi kết thúc phiên.
    /// </summary>
    public class WebCamController : MonoBehaviour
    {
        [Header("=== HIỂN THỊ CAMERA ===")]
        [SerializeField] private RawImage cameraDisplayImage;
        [SerializeField] private AspectRatioFitter aspectRatioFitter;
        [SerializeField] private AspectRatioFitter.AspectMode aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        [SerializeField] private int requestedWidth = 1280;
        [SerializeField] private int requestedHeight = 720;
        [SerializeField] private int requestedFPS = 30;

        private WebCamTexture webCamTexture;

        public bool IsRunning => webCamTexture != null && webCamTexture.isPlaying;
        public RawImage CameraDisplayImage => cameraDisplayImage;
        public WebCamTexture Texture => webCamTexture;

        public event Action<WebCamTexture> OnCameraStarted;
        public event Action<string> OnCameraError;

        public void StartCamera()
        {
            if (WebCamTexture.devices.Length == 0)
            {
                Debug.LogWarning("[WebCamController] Không tìm thấy thiết bị webcam trên máy tính.");
                OnCameraError?.Invoke("Không tìm thấy Webcam!");
                return;
            }

            try
            {
                if (webCamTexture == null)
                {
                    string deviceName = WebCamTexture.devices[0].name;
                    webCamTexture = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFPS);
                }

                if (!webCamTexture.isPlaying)
                {
                    webCamTexture.Play();
                }

                if (cameraDisplayImage == null)
                {
                    var bg = transform.Find("Background_Video");
                    if (bg != null) cameraDisplayImage = bg.GetComponent<RawImage>();
                    if (cameraDisplayImage == null) cameraDisplayImage = GetComponentInChildren<RawImage>(true);
                }

                if (cameraDisplayImage != null)
                {
                    cameraDisplayImage.texture = webCamTexture;
                    cameraDisplayImage.color = Color.white; // Luôn giữ màu trắng để video camera sáng rõ, không bị ám tối
                    cameraDisplayImage.gameObject.SetActive(true);

                    // Căn chỉnh RectTransform của cameraDisplayImage về tâm để co giãn đều 2 phía
                    var rt = cameraDisplayImage.rectTransform;
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;

                    // Đảm bảo có AspectRatioFitter
                    if (aspectRatioFitter == null)
                    {
                        aspectRatioFitter = cameraDisplayImage.GetComponent<AspectRatioFitter>();
                        if (aspectRatioFitter == null)
                        {
                            aspectRatioFitter = cameraDisplayImage.gameObject.AddComponent<AspectRatioFitter>();
                        }
                    }
                    aspectRatioFitter.aspectMode = aspectMode;
                }

                // Đảm bảo PracticeScreen có RectMask2D để cắt gọn phần video tràn 2 bên
                var mask = GetComponent<RectMask2D>();
                if (mask == null)
                {
                    gameObject.AddComponent<RectMask2D>();
                }

                Debug.Log("[WebCamController] Webcam đã bật: " + webCamTexture.deviceName);
                OnCameraStarted?.Invoke(webCamTexture);
            }
            catch (Exception ex)
            {
                Debug.LogError("[WebCamController] Lỗi khi khởi động camera: " + ex.Message);
                OnCameraError?.Invoke(ex.Message);
            }
        }

        private void Update()
        {
            if (webCamTexture != null && webCamTexture.isPlaying)
            {
                // Khi webcam đã nhận diện kích thước thực từ phần cứng (> 100px)
                if (webCamTexture.width > 100 && webCamTexture.height > 100)
                {
                    UpdateAspectRatio();
                }
            }
        }

        private void UpdateAspectRatio()
        {
            if (cameraDisplayImage == null || webCamTexture == null) return;

            if (aspectRatioFitter == null)
            {
                aspectRatioFitter = cameraDisplayImage.GetComponent<AspectRatioFitter>();
                if (aspectRatioFitter == null)
                {
                    aspectRatioFitter = cameraDisplayImage.gameObject.AddComponent<AspectRatioFitter>();
                }
            }

            float videoRatio = (float)webCamTexture.width / Mathf.Max(1, webCamTexture.height);

            // Nếu camera bị xoay 90 hoặc 270 độ (trên mobile hoặc webcam dọc)
            if (webCamTexture.videoRotationAngle == 90 || webCamTexture.videoRotationAngle == 270)
            {
                videoRatio = 1f / videoRatio;
            }

            if (aspectRatioFitter.aspectMode != aspectMode)
            {
                aspectRatioFitter.aspectMode = aspectMode;
            }

            if (Mathf.Abs(aspectRatioFitter.aspectRatio - videoRatio) > 0.005f)
            {
                aspectRatioFitter.aspectRatio = videoRatio;
            }

            // Xử lý góc xoay và lật nếu camera phần cứng báo
            float scaleY = webCamTexture.videoVerticallyMirrored ? -1f : 1f;
            cameraDisplayImage.rectTransform.localEulerAngles = new Vector3(0, 0, -webCamTexture.videoRotationAngle);
            cameraDisplayImage.rectTransform.localScale = new Vector3(1f, scaleY, 1f);
        }

        public void StopCamera()
        {
            if (webCamTexture != null)
            {
                if (webCamTexture.isPlaying)
                {
                    webCamTexture.Stop();
                }
                webCamTexture = null;
            }

            if (cameraDisplayImage != null)
            {
                cameraDisplayImage.texture = null;
            }
            Debug.Log("[WebCamController] Đã tắt và giải phóng Webcam.");
        }

        private void OnDisable()
        {
            StopCamera();
        }

        private void OnDestroy()
        {
            StopCamera();
        }
    }
}
