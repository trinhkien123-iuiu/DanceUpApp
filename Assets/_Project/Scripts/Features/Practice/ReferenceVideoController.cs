using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace DanceUp.Features.Practice
{
    /// <summary>
    /// Điều khiển video mẫu của giáo viên ở góc trên bên trái màn hình tập (tỉ lệ 3:4).
    /// Khi video kết thúc, báo tín hiệu hoàn thành phiên tập luyện.
    /// </summary>
    public class ReferenceVideoController : MonoBehaviour
    {
        [Header("=== THIẾT LẬP VIDEO MẪU ===")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoDisplayImage;
        [SerializeField] private VideoClip defaultReferenceClip;
        [SerializeField] private GameObject placeholderImage;

        public event Action OnVideoFinished;
        public event Action OnVideoStarted;

        public VideoClip CurrentClip => videoPlayer != null ? videoPlayer.clip : defaultReferenceClip;
        public double Duration => videoPlayer != null ? videoPlayer.length : 0.0;
        public double CurrentTime => videoPlayer != null ? videoPlayer.time : 0.0;
        public bool IsPlaying => videoPlayer != null && videoPlayer.isPlaying;

        private void Awake()
        {
            InitializeVideoReferences();
        }

        private void OnEnable()
        {
            InitializeVideoReferences();
            if (videoPlayer != null && (videoPlayer.clip != null || !string.IsNullOrEmpty(videoPlayer.url)))
            {
                PrepareAndHoldAtStart();
            }
        }

        private void InitializeVideoReferences()
        {
            if (videoPlayer == null)
            {
                videoPlayer = GetComponent<VideoPlayer>();
            }

            if (videoDisplayImage == null)
            {
                videoDisplayImage = GetComponent<RawImage>();
            }

            // Tìm placeholder che mất video nếu có
            if (placeholderImage == null)
            {
                var dp = transform.Find("DancerPlaceholder");
                if (dp != null) placeholderImage = dp.gameObject;
            }

            // Tự động nạp clip 1_teacher nếu chưa gán
            if (defaultReferenceClip == null)
            {
                var clips = Resources.FindObjectsOfTypeAll<VideoClip>();
                foreach (var c in clips)
                {
                    if (c.name.IndexOf("teacher", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        defaultReferenceClip = c;
                        break;
                    }
                }
            }

            if (videoPlayer != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.waitForFirstFrame = true;

                if (videoPlayer.clip == null && defaultReferenceClip != null)
                {
                    videoPlayer.clip = defaultReferenceClip;
                }

                if (videoPlayer.renderMode == VideoRenderMode.RenderTexture)
                {
                    if (videoPlayer.targetTexture == null)
                    {
                        var rt = Resources.Load<RenderTexture>("RT_ReferenceVideo");
                        if (rt == null)
                        {
                            rt = new RenderTexture(360, 480, 0);
                        }
                        videoPlayer.targetTexture = rt;
                    }

                    if (videoDisplayImage != null)
                    {
                        videoDisplayImage.texture = videoPlayer.targetTexture;
                    }
                }

                // Ẩn placeholder che phủ video
                if (placeholderImage != null && (videoPlayer.clip != null || defaultReferenceClip != null))
                {
                    placeholderImage.SetActive(false);
                }

                videoPlayer.loopPointReached -= HandleVideoEnd;
                videoPlayer.loopPointReached += HandleVideoEnd;
                videoPlayer.errorReceived -= HandleVideoError;
                videoPlayer.errorReceived += HandleVideoError;
            }
        }

        public void SetVideoClip(VideoClip clip)
        {
            if (videoPlayer != null && clip != null)
            {
                videoPlayer.playOnAwake = false;
                videoPlayer.clip = clip;
                if (placeholderImage != null) placeholderImage.SetActive(false);
                PrepareAndHoldAtStart();
            }
        }

        /// <summary>
        /// Nạp trước khung hình đầu tiên của giáo viên để hiển thị sẵn sàng, nhưng tạm dừng không tự ý phát.
        /// Chỉ chính thức phát khi hoàn tất đếm ngược 3 giây.
        /// </summary>
        public void PrepareAndHoldAtStart()
        {
            InitializeVideoReferences();
            if (videoPlayer == null) return;
            videoPlayer.playOnAwake = false;

            if (videoPlayer.clip == null && defaultReferenceClip != null)
            {
                videoPlayer.clip = defaultReferenceClip;
            }

            if (placeholderImage != null)
            {
                placeholderImage.SetActive(false);
            }

            if (videoDisplayImage != null && videoPlayer.targetTexture != null)
            {
                videoDisplayImage.texture = videoPlayer.targetTexture;
                videoDisplayImage.gameObject.SetActive(true);
            }

            if (videoPlayer.clip != null || !string.IsNullOrEmpty(videoPlayer.url))
            {
                videoPlayer.time = 0;
                videoPlayer.prepareCompleted -= HandlePrepareCompleted;
                videoPlayer.prepareCompleted += HandlePrepareCompleted;
                videoPlayer.Prepare();
            }
        }

        private void HandlePrepareCompleted(VideoPlayer vp)
        {
            vp.prepareCompleted -= HandlePrepareCompleted;
            // Force first frame render into target texture
            vp.Play();
            vp.Pause();
            vp.time = 0;
            if (placeholderImage != null)
            {
                placeholderImage.SetActive(false);
            }
            if (videoDisplayImage != null)
            {
                if (vp.targetTexture != null) videoDisplayImage.texture = vp.targetTexture;
                videoDisplayImage.gameObject.SetActive(true);
            }
            Debug.Log("[ReferenceVideoController] Video mẫu đã nạp sẵn khung hình đầu tiên (Sẵn sàng đồng bộ cùng Camera).");
        }

        public void PlayVideo(VideoClip customClip = null)
        {
            InitializeVideoReferences();
            if (videoPlayer == null) return;

            if (customClip != null)
            {
                videoPlayer.clip = customClip;
            }
            else if (videoPlayer.clip == null && defaultReferenceClip != null)
            {
                videoPlayer.clip = defaultReferenceClip;
            }

            if (placeholderImage != null)
            {
                placeholderImage.SetActive(false);
            }

            if (videoDisplayImage != null && videoPlayer.targetTexture != null)
            {
                videoDisplayImage.texture = videoPlayer.targetTexture;
                videoDisplayImage.gameObject.SetActive(true);
            }

            if (videoPlayer.clip != null || !string.IsNullOrEmpty(videoPlayer.url))
            {
                videoPlayer.time = 0;
                videoPlayer.Play();
                OnVideoStarted?.Invoke();
                string clipName = videoPlayer.clip != null ? videoPlayer.clip.name : "Stream";
                Debug.Log("[ReferenceVideoController] Bắt đầu phát video mẫu: " + clipName + " (" + videoPlayer.length.ToString("F1") + "s)");
            }
            else
            {
                Debug.LogWarning("[ReferenceVideoController] Chưa gán video mẫu trong Inspector!");
            }
        }

        public void PauseVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Pause();
            }
        }

        public void ResumeVideo()
        {
            if (videoPlayer != null && !videoPlayer.isPlaying)
            {
                videoPlayer.Play();
            }
        }

        public void StopVideo()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.time = 0;
            }
        }

        public void SeekTo(float timeInSeconds)
        {
            if (videoPlayer != null)
            {
                videoPlayer.time = Mathf.Clamp(timeInSeconds, 0f, (float)videoPlayer.length);
            }
        }

        public void SetPlaybackSpeed(float speed)
        {
            if (videoPlayer != null)
            {
                videoPlayer.playbackSpeed = Mathf.Clamp(speed, 0.25f, 2.0f);
            }
        }

        private void HandleVideoEnd(VideoPlayer vp)
        {
            Debug.Log("[ReferenceVideoController] Video mẫu đã phát hết. Báo hiệu kết thúc bài tập.");
            OnVideoFinished?.Invoke();
        }

        private void HandleVideoError(VideoPlayer vp, string message)
        {
            Debug.LogError("[ReferenceVideoController] Lỗi video player: " + message);
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= HandleVideoEnd;
                videoPlayer.errorReceived -= HandleVideoError;
            }
        }
    }
}
