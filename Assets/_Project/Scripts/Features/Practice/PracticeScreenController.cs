using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using DanceUp.UI;

namespace DanceUp.Features.Practice
{
    public class PracticeScreenController : MonoBehaviour
    {
        [Header("=== KẾT NỐI NAVIGATION HIỆN TẠI ===")]
        [SerializeField] private BottomNavigation bottomNavigation;
        [SerializeField] private GameObject bottomNavObject;

        [Header("=== GIAO DIỆN UI (TextMeshPro & Buttons) ===")]
        [SerializeField] private Button btnClose;
        [SerializeField] private Button btnPlayPause;
        [SerializeField] private Image imgPlayPauseIcon;
        [SerializeField] private Sprite iconPlay;
        [SerializeField] private Sprite iconPause;
        [SerializeField] private TMP_Text txtTimer;
        [SerializeField] private TMP_Text txtScore;
        [SerializeField] private TMP_Text txtAccuracyPercent;
        [SerializeField] private Slider sliderAccuracy;
        [SerializeField] private GameObject bubbleCoach;
        [SerializeField] private TMP_Text txtCoachAdvice;

        [Header("=== CÁC NÚT ĐIỀU KHIỂN THEO THIẾT KẾ FIGMA ===")]
        [SerializeField] private Button btnRewind10s;
        [SerializeField] private Button btnForward10s;
        [SerializeField] private Button btnToggleSkeleton;
        [SerializeField] private Button btnMirror;
        [SerializeField] private Button btnPerspectiveFront;
        [SerializeField] private Button btnPerspectiveBack;
        [SerializeField] private Button btnPerspectiveCamera;
        [SerializeField] private Button btnSpeed;
        [SerializeField] private Button btnLoop;
        [SerializeField] private Button btnAudio;
        [SerializeField] private TMP_Text txtSpeed;
        [SerializeField] private TMP_Text txtAccuracyBadge;

        private bool isMirrored = false;
        private float currentPlaybackSpeed = 1.0f;
        private bool isAudioMuted = false;

        [Header("=== SKELETON UI OVERLAY ===")]
        [SerializeField] private RectTransform skeletonContainer;
        [SerializeField] private GameObject jointPrefab;
        [SerializeField] private GameObject bonePrefab;
        [SerializeField] private Color correctColor = new Color(0.2f, 0.9f, 0.2f); // Xanh lá khi chuẩn
        [SerializeField] private Color errorColor = new Color(1f, 0.25f, 0.25f);  // Đỏ khi sai
        [SerializeField] private float skeletonScale = 450f;
        [SerializeField] private Vector2 skeletonOffset = new Vector2(0f, -30f);
        [SerializeField] private float jointSize = 22f;
        [SerializeField] private float boneThickness = 5f;

        [Header("=== DỮ LIỆU BÀI TẬP ===")]
        [SerializeField] private TextAsset jsonAsset;
        [SerializeField] private string jsonFileName = "dance_demo_data.json";

        [Header("=== CAMERA HỌC VIÊN & VIDEO MẪU ===")]
        [SerializeField] private DanceUp.CameraInput.WebCamController webcamController;
        [SerializeField] private ReferenceVideoController referenceVideoController;
        public ReferenceVideoController ReferenceVideo => referenceVideoController;
        [SerializeField] private DanceUp.AI.DanceMLPipeline mlPipeline;

        [Header("=== LIVE UDP POSE STREAMING (Option B) ===")]
        [SerializeField] private bool enableUdpStream = true;
        [SerializeField] private int udpPort = 5065;
        [SerializeField] private float livePoseTimeout = 1.5f;
        [SerializeField] private bool useDemoFallbackWhenNoUdp = false;
        [SerializeField] private bool keepPythonServiceAlive = true;

        [Header("=== ĐẾM NGƯỢC 3 GIÂY CHUẨN BỊ (COUNTDOWN) ===")]
        [SerializeField] private GameObject countdownContainer;
        [SerializeField] private TMP_Text txtCountdown;
        [SerializeField] private float countdownDuration = 3f;

        [Header("=== CẤU HÌNH TỶ LỆ KHUNG HÌNH CAMERA ===")]
        [SerializeField] private AspectRatioFitter.AspectMode cameraAspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        private bool isCountingDown = false;
        private Coroutine countdownCoroutine = null;

        public event Action<DanceUp.Data.FrameEvaluationResult> OnFrameEvaluated;
        public event Action OnSessionCompleted;

        [Serializable]
        public class JointData { public float x; public float y; public int is_pass; }

        [Serializable]
        public class FrameItem
        {
            public int frame_index;
            public float timestamp;
            public float frame_score;
            public bool is_pass;
            public float cumulative_accuracy;
            public Dictionary<string, JointData> student_joints = new Dictionary<string, JointData>();
            public Dictionary<string, JointData> teacher_joints = new Dictionary<string, JointData>();
        }

        private List<FrameItem> frames = new List<FrameItem>();
        private readonly Dictionary<string, RectTransform> jointUIs = new Dictionary<string, RectTransform>();
        private readonly List<(string j1, string j2, RectTransform boneUI)> boneUIs = new List<(string, string, RectTransform)>();

        private readonly string[][] bonePairs = new string[][]
        {
            new[]{"nose", "left_shoulder"}, new[]{"nose", "right_shoulder"},
            new[]{"left_shoulder", "right_shoulder"}, new[]{"left_shoulder", "left_elbow"},
            new[]{"left_elbow", "left_wrist"}, new[]{"right_shoulder", "right_elbow"},
            new[]{"right_elbow", "right_wrist"}, new[]{"left_shoulder", "left_hip"},
            new[]{"right_shoulder", "right_hip"}, new[]{"left_hip", "right_hip"},
            new[]{"left_hip", "left_knee"}, new[]{"left_knee", "left_ankle"},
            new[]{"right_hip", "right_knee"}, new[]{"right_knee", "right_ankle"}
        };

        private bool isPlaying = false;
        private int currentFrame = 0;
        private float frameTimer = 0f;
        private const float FPS = 30f;

        // UDP Socket fields
        private Thread udpThread;
        private UdpClient udpClient;
        private readonly object udpLock = new object();
        private Dictionary<string, JointData> latestLiveJoints = null;
        private byte[] latestLiveImageBytes = null;
        private Texture2D liveCameraTexture = null;
        private readonly System.Diagnostics.Stopwatch lastLivePacketStopwatch = new System.Diagnostics.Stopwatch();
        private volatile bool isUdpRunning = false;
        private int totalLivePacketsReceived = 0;

        private void Awake()
        {
            // Đảm bảo PracticeScreen stretch toàn màn hình điện thoại
            var rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            if (bottomNavigation != null)
                bottomNavigation.CameraRequested += OpenPracticeScreen;

            if (btnClose != null)
            {
                btnClose.onClick.AddListener(() => {
                    // Chuyển sang màn hình đánh giá kết quả
                    if (DanceUp.Core.DanceSessionManager.Instance != null)
                    {
                        DanceUp.Core.DanceSessionManager.Instance.CompleteSession();
                    }
                    else
                    {
                        ClosePracticeScreen();
                    }
                });
            }

            if (btnPlayPause != null)
                btnPlayPause.onClick.AddListener(TogglePlayPause);

            // Tự động khởi tạo webcam
            if (webcamController == null)
            {
                webcamController = GetComponent<DanceUp.CameraInput.WebCamController>();
                if (webcamController == null) webcamController = gameObject.AddComponent<DanceUp.CameraInput.WebCamController>();
            }

            // Tự động khởi tạo pipeline AI
            if (mlPipeline == null)
            {
                mlPipeline = GetComponent<DanceUp.AI.DanceMLPipeline>();
                if (mlPipeline == null) mlPipeline = gameObject.AddComponent<DanceUp.AI.DanceMLPipeline>();
            }

            // Tự động khởi tạo khung xem trước video mẫu 3:4 ở GÓC TRÊN BÊN TRÁI
            SetupReferenceVideoPreview();

            if (referenceVideoController != null)
            {
                referenceVideoController.OnVideoFinished -= HandleVideoFinished;
                referenceVideoController.OnVideoFinished += HandleVideoFinished;
            }

            // Khởi động UDP listener cho Option B
            StartUdpListener();

            // Khởi tạo các nút điều khiển mở rộng theo thiết kế Figma
            SetupFigmaControls();
        }

        private void SetupFigmaControls()
        {
            if (btnRewind10s != null)
            {
                btnRewind10s.onClick.AddListener(() => {
                    if (referenceVideoController != null)
                    {
                        var vp = referenceVideoController.GetComponent<UnityEngine.Video.VideoPlayer>();
                        if (vp != null && vp.canSetTime)
                        {
                            vp.time = Mathf.Max(0, (float)vp.time - 10f);
                        }
                    }
                });
            }

            if (btnForward10s != null)
            {
                btnForward10s.onClick.AddListener(() => {
                    if (referenceVideoController != null)
                    {
                        var vp = referenceVideoController.GetComponent<UnityEngine.Video.VideoPlayer>();
                        if (vp != null && vp.canSetTime)
                        {
                            vp.time = Mathf.Min((float)vp.length, (float)vp.time + 10f);
                        }
                    }
                });
            }

            if (btnToggleSkeleton != null)
            {
                btnToggleSkeleton.onClick.AddListener(() => {
                    if (skeletonContainer != null)
                    {
                        skeletonContainer.gameObject.SetActive(!skeletonContainer.gameObject.activeSelf);
                    }
                });
            }

            if (btnMirror != null)
            {
                btnMirror.onClick.AddListener(ToggleMirror);
            }

            if (btnSpeed != null)
            {
                btnSpeed.onClick.AddListener(CyclePlaybackSpeed);
            }

            if (btnAudio != null)
            {
                btnAudio.onClick.AddListener(ToggleAudio);
            }

            if (btnPerspectiveFront != null)
            {
                btnPerspectiveFront.onClick.AddListener(() => SetPerspectiveTab(0));
            }
            if (btnPerspectiveBack != null)
            {
                btnPerspectiveBack.onClick.AddListener(() => SetPerspectiveTab(1));
            }
            if (btnPerspectiveCamera != null)
            {
                btnPerspectiveCamera.onClick.AddListener(() => SetPerspectiveTab(2));
            }
        }

        private void ToggleMirror()
        {
            isMirrored = !isMirrored;
            var targetImg = (webcamController != null && webcamController.CameraDisplayImage != null)
                ? webcamController.CameraDisplayImage
                : GetComponentInChildren<RawImage>(true);

            if (targetImg != null)
            {
                var s = targetImg.rectTransform.localScale;
                s.x = isMirrored ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
                targetImg.rectTransform.localScale = s;
            }
            if (skeletonContainer != null)
            {
                var s = skeletonContainer.localScale;
                s.x = isMirrored ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
                skeletonContainer.localScale = s;
            }
        }

        private void CyclePlaybackSpeed()
        {
            if (Mathf.Approximately(currentPlaybackSpeed, 1.0f)) currentPlaybackSpeed = 0.75f;
            else if (Mathf.Approximately(currentPlaybackSpeed, 0.75f)) currentPlaybackSpeed = 0.5f;
            else if (Mathf.Approximately(currentPlaybackSpeed, 0.5f)) currentPlaybackSpeed = 1.25f;
            else currentPlaybackSpeed = 1.0f;

            if (txtSpeed != null) txtSpeed.text = currentPlaybackSpeed.ToString("0.##") + "×";

            if (referenceVideoController != null)
            {
                var vp = referenceVideoController.GetComponent<UnityEngine.Video.VideoPlayer>();
                if (vp != null) vp.playbackSpeed = currentPlaybackSpeed;
            }
        }

        private void ToggleAudio()
        {
            isAudioMuted = !isAudioMuted;
            if (referenceVideoController != null)
            {
                var vp = referenceVideoController.GetComponent<UnityEngine.Video.VideoPlayer>();
                if (vp != null && vp.audioTrackCount > 0)
                {
                    vp.SetDirectAudioMute(0, isAudioMuted);
                }
            }
        }

        private void SetPerspectiveTab(int index)
        {
            Color activeBg = new Color(0f, 0.706f, 1f, 1f); // #00B4FF
            Color activeText = new Color(0.027f, 0.063f, 0.161f, 1f); // #071029
            Color inactiveBg = new Color(0.051f, 0.106f, 0.165f, 1f); // #0D1B2A
            Color inactiveText = Color.white;

            Button[] btns = { btnPerspectiveFront, btnPerspectiveBack, btnPerspectiveCamera };
            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i] == null) continue;
                var img = btns[i].GetComponent<Image>();
                var txt = btns[i].GetComponentInChildren<TMP_Text>();
                bool isActive = (i == index);
                if (img != null) img.color = isActive ? activeBg : inactiveBg;
                if (txt != null) txt.color = isActive ? activeText : inactiveText;
            }
        }

        private void SetupReferenceVideoPreview()
        {
            if (referenceVideoController == null)
            {
                referenceVideoController = GetComponentInChildren<ReferenceVideoController>(true);
            }
            if (referenceVideoController == null)
            {
                var existing = transform.Find("ReferenceVideo_Preview_3x4");
                GameObject videoObj;
                if (existing != null)
                {
                    videoObj = existing.gameObject;
                }
                else
                {
                    videoObj = new GameObject("ReferenceVideo_Preview_3x4", typeof(RectTransform), typeof(RawImage), typeof(VideoPlayer), typeof(ReferenceVideoController));
                    videoObj.transform.SetParent(transform, false);
                }

                var vRect = videoObj.GetComponent<RectTransform>();
                // Góc trên bên trái: anchor (0, 1), pivot (0, 1)
                vRect.anchorMin = new Vector2(0f, 1f);
                vRect.anchorMax = new Vector2(0f, 1f);
                vRect.pivot = new Vector2(0f, 1f);
                vRect.anchoredPosition = new Vector2(16f, -65f); // Cách lề trái 16px, dưới thanh trạng thái 65px
                vRect.sizeDelta = new Vector2(120f, 160f); // Tỉ lệ chuẩn 3:4!

                var rawImg = videoObj.GetComponent<RawImage>();
                var vp = videoObj.GetComponent<VideoPlayer>();
                vp.playOnAwake = false;
                vp.waitForFirstFrame = true;
                vp.renderMode = VideoRenderMode.RenderTexture;
                if (vp.targetTexture == null)
                {
                    var rt = new RenderTexture(240, 320, 0);
                    vp.targetTexture = rt;
                    rawImg.texture = rt;
                }

                referenceVideoController = videoObj.GetComponent<ReferenceVideoController>();
            }
        }

        private void HandleVideoFinished()
        {
            Debug.Log("[PracticeScreenController] Video mẫu đã kết thúc. Hoàn tất phiên tập.");
            OnSessionCompleted?.Invoke();
            if (DanceUp.Core.DanceSessionManager.Instance != null)
            {
                DanceUp.Core.DanceSessionManager.Instance.CompleteSession();
            }
        }

        private void Start()
        {
            LoadDanceData();
            InitSkeletonUI();
            gameObject.SetActive(false);
        }

        public void OpenPracticeScreenWithVideo(VideoClip clip, string title)
        {
            SetupReferenceVideoPreview();
            if (referenceVideoController != null && clip != null)
            {
                referenceVideoController.SetVideoClip(clip);
            }
            OpenPracticeScreen();
        }

        public void PlaySegmentReview(VideoClip clip, float startTime, float endTime, float speed = 0.75f)
        {
            SetupReferenceVideoPreview();
            if (referenceVideoController != null && clip != null)
            {
                referenceVideoController.SetVideoClip(clip);
            }
            OpenPracticeScreen();

            if (referenceVideoController != null)
            {
                referenceVideoController.SetPlaybackSpeed(speed);
                referenceVideoController.SeekTo(Mathf.Max(0f, startTime - 0.5f));
                referenceVideoController.PlayVideo();
            }

            isPlaying = true;
            UpdatePlayPauseIcon();

            if (txtCoachAdvice != null)
            {
                txtCoachAdvice.text = string.Format("Xem lại đoạn cần chỉnh ({0:F1}s - {1:F1}s) ở tốc độ {2:F2}x", startTime, endTime, speed);
            }
        }

        public void OpenPracticeScreen()
        {
            gameObject.SetActive(true);
            if (bottomNavObject != null) bottomNavObject.SetActive(false);

            EnsureCountdownUI();

            currentFrame = 0;
            frameTimer = 0f;
            isPlaying = false; // Chưa bắt đầu bài tập vội, chờ camera sẵn sàng và đếm ngược 3s
            isCountingDown = false;
            UpdatePlayPauseIcon();

            // Xóa sạch buffer UDP cũ từ lần tập trước để đón nhận khung hình mới nhất
            lock (udpLock)
            {
                latestLiveImageBytes = null;
                latestLiveJoints = null;
                totalLivePacketsReceived = 0;
                lastLivePacketStopwatch.Reset();
            }

            // Khi dùng live UDP streaming, Python sẽ trực tiếp mở webcam để chạy MediaPipe
            // Do đó Unity không mở WebCamTexture để tránh xung đột phần cứng camera trên Windows
            if (!enableUdpStream && webcamController != null)
            {
                webcamController.StartCamera();
            }

            SetupReferenceVideoPreview();
            if (referenceVideoController != null)
            {
                // Nạp sẵn khung hình đầu tiên của giáo viên và giữ tạm dừng ở t=0
                referenceVideoController.PrepareAndHoldAtStart();
            }

            if (mlPipeline != null)
            {
                mlPipeline.ResetSession();
            }

            if (enableUdpStream)
            {
                // Đảm bảo tiến trình Python Pose Extractor đang chạy ngầm
                if (!DanceUp.AI.PythonPoseService.Instance.IsServiceRunning)
                {
                    DanceUp.AI.PythonPoseService.Instance.StartService(true);
                }
                StartUdpListener();
            }

            if (frames.Count == 0)
            {
                LoadDanceData();
            }
            if (jointUIs.Count == 0)
            {
                InitSkeletonUI();
            }

            if (frames.Count > 0)
            {
                RenderCurrentFrame(frames[0]);
            }

            // Kích hoạt luồng chờ camera sẵn sàng -> Đếm ngược 3 giây -> Đồng thời phát video và bắt đầu bài tập
            if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
            countdownCoroutine = StartCoroutine(PrepareAndStartCountdownRoutine());
        }

        private IEnumerator PrepareAndStartCountdownRoutine()
        {
            isCountingDown = true;
            isPlaying = false;
            UpdatePlayPauseIcon();

            // 1. Chờ camera mở và sẵn sàng nhận diện
            if (txtCoachAdvice != null)
            {
                txtCoachAdvice.text = "Đang kết nối camera AI... Vui lòng đứng trước khung hình!";
            }
            if (bubbleCoach != null && !bubbleCoach.activeSelf) bubbleCoach.SetActive(true);

            // Đảm bảo video mẫu luôn tạm dừng ở khung hình đầu trong lúc chờ camera và đếm ngược
            if (referenceVideoController != null)
            {
                referenceVideoController.PauseVideo();
            }

            float waitStartTime = Time.time;
            const float MAX_CAMERA_WAIT = 1.5f; // Chờ camera 1.5s, nếu chưa có thì vẫn tiếp tục đếm ngược vào bài tập ngay

            while (Time.time - waitStartTime < MAX_CAMERA_WAIT)
            {
                bool cameraReady = false;
                if (enableUdpStream)
                {
                    // Camera sẵn sàng khi đã có hình ảnh hoặc đã nhận ít nhất 1 packet pose
                    cameraReady = (latestLiveImageBytes != null || (latestLiveJoints != null && latestLiveJoints.Count > 0));
                }
                else
                {
                    cameraReady = (webcamController != null && webcamController.IsRunning);
                }

                if (cameraReady) break;
                yield return new WaitForSeconds(0.05f);
            }

            // 2. Camera đã sẵn sàng! Bây giờ mới bắt đầu đếm ngược 3 giây
            EnsureCountdownUI();
            if (countdownContainer != null) countdownContainer.SetActive(true);

            int totalSeconds = Mathf.Max(1, Mathf.RoundToInt(countdownDuration));
            for (int count = totalSeconds; count >= 1; count--)
            {
                SetCountdownVisual(count.ToString(), CountdownNumberSize, CountdownDisc, "Chuẩn bị vào tư thế");

                if (txtCoachAdvice != null)
                {
                    txtCoachAdvice.text = $"Chuẩn bị vào bài tập: {count}...";
                }

                // Số mới bật lên (phóng to → thu về, mờ → rõ) và vòng tiến độ vơi dần trong 1 giây
                float elapsed = 0f;
                while (elapsed < 1.0f)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed);
                    AnimateCountdownPop(t);
                    if (countdownRing != null) countdownRing.fillAmount = 1f - t;
                    yield return null;
                }
            }

            // "Bắt đầu!" – vòng tròn chuyển sang nền cyan đặc
            SetCountdownVisual("Bắt đầu!", CountdownStartSize, CountdownAccent, "Nhảy theo video mẫu nào!");
            if (countdownRing != null) countdownRing.fillAmount = 1f;
            if (txtCoachAdvice != null)
            {
                txtCoachAdvice.text = "Bắt đầu nhảy theo mẫu giáo viên!";
            }

            float startElapsed = 0f;
            while (startElapsed < 0.7f)
            {
                startElapsed += Time.deltaTime;
                AnimateCountdownPop(Mathf.Clamp01(startElapsed / 0.7f));
                yield return null;
            }

            // 3. Kết thúc đếm ngược, chính thức kích hoạt video mẫu và bắt đầu bài tập đồng thời
            if (countdownContainer != null) countdownContainer.SetActive(false);
            isCountingDown = false;
            isPlaying = true;
            UpdatePlayPauseIcon();

            if (referenceVideoController != null)
            {
                referenceVideoController.PlayVideo();
            }

            countdownCoroutine = null;
            Debug.Log("[PracticeScreenController] Đếm ngược hoàn tất. Video mẫu và bài tập đã chính thức bắt đầu đồng thời!");
        }

        public void ClosePracticeScreen()
        {
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
                countdownCoroutine = null;
            }

            isCountingDown = false;
            isPlaying = false;
            if (countdownContainer != null) countdownContainer.SetActive(false);

            gameObject.SetActive(false);

            if (referenceVideoController != null)
            {
                referenceVideoController.StopVideo();
            }

            // Nếu keepPythonServiceAlive = true (mặc định), giữ Python tiếp tục chạy ngầm
            // Điều này đảm bảo các lần tập tiếp theo (Attempt 2, 3...) camera mở tức thì, không bị lock thiết bị trên Windows!
            if (!keepPythonServiceAlive && enableUdpStream)
            {
                DanceUp.AI.PythonPoseService.Instance.StopService();
                StopUdpListener();
            }

            if (!enableUdpStream && webcamController != null)
            {
                webcamController.StopCamera();
            }

            if (bottomNavObject != null) bottomNavObject.SetActive(true);
        }

        private void TogglePlayPause()
        {
            isPlaying = !isPlaying;
            UpdatePlayPauseIcon();

            if (referenceVideoController != null)
            {
                if (isPlaying) referenceVideoController.ResumeVideo();
                else referenceVideoController.PauseVideo();
            }
        }

        private void UpdatePlayPauseIcon()
        {
            if (imgPlayPauseIcon != null)
                imgPlayPauseIcon.sprite = isPlaying ? iconPause : iconPlay;
        }

        private void Update()
        {
            // Cập nhật khung hình camera trực tiếp từ UDP stream nếu có
            if (enableUdpStream)
            {
                byte[] imgData = null;
                lock (udpLock)
                {
                    if (latestLiveImageBytes != null)
                    {
                        imgData = latestLiveImageBytes;
                        latestLiveImageBytes = null;
                    }
                }

                if (imgData != null && imgData.Length > 0)
                {
                    if (liveCameraTexture == null)
                    {
                        liveCameraTexture = new Texture2D(2, 2);
                    }
                    liveCameraTexture.LoadImage(imgData);

                    RawImage targetRawImg = null;
                    if (webcamController != null && webcamController.CameraDisplayImage != null)
                    {
                        targetRawImg = webcamController.CameraDisplayImage;
                    }
                    else
                    {
                        var bg = transform.Find("Background_Video");
                        if (bg != null) targetRawImg = bg.GetComponent<RawImage>();
                        if (targetRawImg == null) targetRawImg = GetComponentInChildren<RawImage>(true);
                    }

                    if (targetRawImg != null)
                    {
                        targetRawImg.texture = liveCameraTexture;
                        targetRawImg.color = Color.white; // Luôn giữ màu trắng để luồng camera AI sáng rõ tự nhiên
                        if (!targetRawImg.gameObject.activeSelf) targetRawImg.gameObject.SetActive(true);

                        // Áp dụng Aspect Ratio chuẩn, bảo toàn tỷ lệ gốc không bị kéo giãn dọc
                        ApplyCameraAspectRatio(targetRawImg, liveCameraTexture.width, liveCameraTexture.height);
                    }
                }
            }
            else if (webcamController != null && webcamController.Texture != null && webcamController.Texture.isPlaying)
            {
                if (webcamController.CameraDisplayImage != null && webcamController.Texture.width > 50)
                {
                    ApplyCameraAspectRatio(webcamController.CameraDisplayImage, webcamController.Texture.width, webcamController.Texture.height);
                }
            }

            // Trong lúc đếm ngược 3 giây chuẩn bị, vẫn cập nhật Skeleton học viên trên camera để căn chỉnh trước gương
            if (isCountingDown)
            {
                if (frames.Count > 0)
                {
                    RenderCurrentFrame(frames[0]);
                }
                return;
            }

            if (!isPlaying || frames.Count == 0) return;

            frameTimer += Time.deltaTime;
            if (frameTimer >= (1f / FPS))
            {
                frameTimer = 0f;
                if (currentFrame + 1 >= frames.Count)
                {
                    if (referenceVideoController == null || !referenceVideoController.IsPlaying)
                    {
                        HandleVideoFinished();
                        return;
                    }
                }

                currentFrame = (currentFrame + 1) % frames.Count;
                RenderCurrentFrame(frames[currentFrame]);
            }
        }

        private void RenderCurrentFrame(FrameItem fr)
        {
            if (fr == null) return;

            // 1. Cập nhật HUD Text: Timer
            TimeSpan time = TimeSpan.FromSeconds(fr.timestamp);
            if (txtTimer != null) txtTimer.text = time.ToString(@"mm\:ss");

            // 2. Xác định dữ liệu khớp của học viên: Live UDP (Option B) hoặc Dữ liệu file (Fallback)
            bool isUdpAlive = enableUdpStream && lastLivePacketStopwatch.IsRunning &&
                              lastLivePacketStopwatch.Elapsed.TotalSeconds < livePoseTimeout;
            bool hasLivePose = isUdpAlive && latestLiveJoints != null && latestLiveJoints.Count > 0;

            Dictionary<string, JointData> activeStudentJoints = null;
            if (hasLivePose)
            {
                lock (udpLock)
                {
                    activeStudentJoints = new Dictionary<string, JointData>(latestLiveJoints);
                }
            }
            else if (!enableUdpStream || useDemoFallbackWhenNoUdp)
            {
                activeStudentJoints = fr.student_joints;
            }

            // Nếu chưa có khớp học viên (chưa phát hiện người hoặc chưa bật Python), ẩn bộ xương
            if (activeStudentJoints == null || activeStudentJoints.Count == 0)
            {
                SetSkeletonVisible(false);

                if (enableUdpStream)
                {
                    if (totalLivePacketsReceived == 0)
                    {
                        if (txtCoachAdvice != null) txtCoachAdvice.text = "Đang chờ khởi chạy python pose_extractor.py...";
                    }
                    else
                    {
                        if (txtCoachAdvice != null) txtCoachAdvice.text = "Vui lòng đứng vào khung hình camera";
                    }
                    if (bubbleCoach != null && !bubbleCoach.activeSelf) bubbleCoach.SetActive(true);
                }
                return;
            }

            SetSkeletonVisible(true);

            // 3. Chạy live ML pipeline (processing_11 / ONNX)
            DanceUp.Data.FrameEvaluationResult evalResult = null;
            if (mlPipeline != null && fr.teacher_joints != null && fr.teacher_joints.Count > 0)
            {
                var sDict = new Dictionary<string, Vector2>();
                foreach (var kvp in activeStudentJoints) sDict[kvp.Key] = new Vector2(kvp.Value.x, kvp.Value.y);

                var tDict = new Dictionary<string, Vector2>();
                foreach (var kvp in fr.teacher_joints) tDict[kvp.Key] = new Vector2(kvp.Value.x, kvp.Value.y);

                evalResult = mlPipeline.EvaluateFrame(fr.frame_index, fr.timestamp, sDict, tDict);
            }

            if (evalResult == null)
            {
                evalResult = new DanceUp.Data.FrameEvaluationResult
                {
                    frameIndex = fr.frame_index,
                    timestamp = fr.timestamp,
                    frameScore = fr.frame_score,
                    isPass = fr.is_pass,
                    cumulativeAccuracy = fr.cumulative_accuracy
                };
                foreach (var kvp in activeStudentJoints)
                {
                    evalResult.joints[kvp.Key] = new DanceUp.Data.JointScoreItem
                    {
                        jointName = kvp.Key,
                        studentPos = new Vector2(kvp.Value.x, kvp.Value.y),
                        isPass = kvp.Value.is_pass
                    };
                }
            }

            float currentAccuracy = evalResult.cumulativeAccuracy;
            if (txtAccuracyBadge != null)
            {
                txtAccuracyBadge.text = currentAccuracy.ToString("F0");
            }

            if (txtAccuracyPercent != null)
            {
                txtAccuracyPercent.text = currentAccuracy.ToString("F0") + "%";
            }
            else if (txtScore != null)
            {
                txtScore.text = currentAccuracy.ToString("F0") + "%";
            }

            if (sliderAccuracy != null) sliderAccuracy.value = currentAccuracy / 100f;

            // 4. AI Coach Feedback
            UpdateCoachAdvice(evalResult, hasLivePose);

            // 5. Gửi sự kiện Frame Evaluation cho DanceSessionManager
            OnFrameEvaluated?.Invoke(evalResult);

            // 6. Cập nhật Skeleton Joints & Bones trên màn hình
            if (skeletonContainer == null) return;

            float scaleX = skeletonScale;
            float scaleY = skeletonScale;
            Vector2 currentOffset = skeletonOffset;

            if (hasLivePose)
            {
                RectTransform camRt = null;
                if (webcamController != null && webcamController.CameraDisplayImage != null)
                {
                    camRt = webcamController.CameraDisplayImage.rectTransform;
                }
                else
                {
                    var bg = transform.Find("Background_Video");
                    if (bg != null) camRt = bg.GetComponent<RectTransform>();
                }

                if (camRt != null && camRt.rect.width > 50 && camRt.rect.height > 50)
                {
                    scaleX = camRt.rect.width;
                    scaleY = camRt.rect.height;
                    currentOffset = Vector2.zero; // Live camera được căn giữa
                }
            }

            foreach (var kvp in evalResult.joints)
            {
                if (jointUIs.TryGetValue(kvp.Key, out var jointRect))
                {
                    jointRect.anchoredPosition = currentOffset + new Vector2(
                        (kvp.Value.studentPos.x - 0.5f) * scaleX,
                        (kvp.Value.studentPos.y - 0.5f) * scaleY
                    );

                    var img = jointRect.GetComponent<Image>();
                    if (img != null)
                        img.color = kvp.Value.isPass == 1 ? correctColor : errorColor;
                }
            }

            foreach (var (j1, j2, boneRect) in boneUIs)
            {
                if (evalResult.joints.TryGetValue(j1, out var p1) &&
                    evalResult.joints.TryGetValue(j2, out var p2))
                {
                    Vector2 posA = currentOffset + new Vector2((p1.studentPos.x - 0.5f) * scaleX, (p1.studentPos.y - 0.5f) * scaleY);
                    Vector2 posB = currentOffset + new Vector2((p2.studentPos.x - 0.5f) * scaleX, (p2.studentPos.y - 0.5f) * scaleY);

                    Vector2 diff = posB - posA;
                    boneRect.anchoredPosition = (posA + posB) / 2f;
                    boneRect.sizeDelta = new Vector2(boneThickness, diff.magnitude);
                    float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg - 90f;
                    boneRect.rotation = Quaternion.Euler(0, 0, angle);

                    var img = boneRect.GetComponent<Image>();
                    if (img != null)
                        img.color = (p1.isPass == 1 && p2.isPass == 1) ? correctColor : errorColor;
                }
            }
        }

        private void UpdateCoachAdvice(DanceUp.Data.FrameEvaluationResult evalResult, bool isLive)
        {
            if (txtCoachAdvice == null || evalResult == null) return;

            if (mlPipeline != null)
            {
                txtCoachAdvice.text = mlPipeline.GenerateCoachAdvice(evalResult);
            }
            else
            {
                bool handError = (evalResult.joints.ContainsKey("left_wrist") && evalResult.joints["left_wrist"].isPass == 0)
                              || (evalResult.joints.ContainsKey("right_wrist") && evalResult.joints["right_wrist"].isPass == 0)
                              || (evalResult.joints.ContainsKey("left_elbow") && evalResult.joints["left_elbow"].isPass == 0);

                bool legError = (evalResult.joints.ContainsKey("left_knee") && evalResult.joints["left_knee"].isPass == 0)
                             || (evalResult.joints.ContainsKey("right_knee") && evalResult.joints["right_knee"].isPass == 0);

                if (handError) txtCoachAdvice.text = "Nâng tay cao hơn một chút";
                else if (legError) txtCoachAdvice.text = "Hạ thấp trọng tâm và bước chắc chân";
                else if (evalResult.isPass) txtCoachAdvice.text = "Chuẩn rồi! Giữ đúng nhịp điệu nhé";
                else txtCoachAdvice.text = "Chú ý giữ thẳng lưng và mở rộng vai";
            }

            if (bubbleCoach != null && !bubbleCoach.activeSelf)
                bubbleCoach.SetActive(true);
        }

        private void SetSkeletonVisible(bool visible)
        {
            if (skeletonContainer != null && skeletonContainer.gameObject.activeSelf != visible)
            {
                skeletonContainer.gameObject.SetActive(visible);
            }
        }

        private void InitSkeletonUI()
        {
            if (skeletonContainer == null) return;

            foreach (Transform child in skeletonContainer)
            {
                if (child.name.StartsWith("Holder", StringComparison.OrdinalIgnoreCase) ||
                    child.name.StartsWith("joint_", StringComparison.OrdinalIgnoreCase) ||
                    child.name.StartsWith("bone", StringComparison.OrdinalIgnoreCase))
                {
                    child.gameObject.SetActive(false);
                }
            }

            jointUIs.Clear();
            boneUIs.Clear();

            string[] joints = {
                "nose", "left_shoulder", "right_shoulder",
                "left_elbow", "right_elbow", "left_wrist", "right_wrist",
                "left_hip", "right_hip", "left_knee", "right_knee",
                "left_ankle", "right_ankle"
            };

            foreach (var j in joints)
            {
                GameObject obj = jointPrefab != null ? Instantiate(jointPrefab, skeletonContainer) : new GameObject("joint_" + j, typeof(RectTransform), typeof(Image));
                if (jointPrefab == null) obj.transform.SetParent(skeletonContainer, false);
                obj.name = "joint_" + j;

                var rect = obj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(jointSize, jointSize);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                var img = obj.GetComponent<Image>();
                img.color = correctColor;

                jointUIs[j] = rect;
            }

            foreach (var pair in bonePairs)
            {
                GameObject obj = bonePrefab != null ? Instantiate(bonePrefab, skeletonContainer) : new GameObject("bone_" + pair[0] + "_" + pair[1], typeof(RectTransform), typeof(Image));
                if (bonePrefab == null) obj.transform.SetParent(skeletonContainer, false);
                obj.name = "bone_" + pair[0] + "_" + pair[1];

                var rect = obj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(boneThickness, 20f);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                var img = obj.GetComponent<Image>();
                img.color = correctColor;

                boneUIs.Add((pair[0], pair[1], rect));
            }
        }

        #region Camera Aspect Ratio & Countdown UI

        private void ApplyCameraAspectRatio(RawImage rawImg, int texWidth, int texHeight)
        {
            if (rawImg == null || texWidth <= 0 || texHeight <= 0) return;

            // Đảm bảo RawImage căn giữa trong parent (PracticeScreen)
            var rt = rawImg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            // Gắn AspectRatioFitter để bảo toàn tỷ lệ khung hình thực của camera
            var fitter = rawImg.GetComponent<AspectRatioFitter>();
            if (fitter == null)
            {
                fitter = rawImg.gameObject.AddComponent<AspectRatioFitter>();
            }

            float ratio = (float)texWidth / (float)texHeight;
            fitter.aspectRatio = ratio;
            fitter.aspectMode = cameraAspectMode;

            // Đảm bảo PracticeScreen có RectMask2D để cắt phần video tràn hai bên gọn gàng
            var mask = GetComponent<RectMask2D>();
            if (mask == null)
            {
                gameObject.AddComponent<RectMask2D>();
            }
        }

        // ── Giao diện đếm ngược: huy hiệu tròn + vòng tiến độ (dựng sẵn trong PracticeScreen.prefab/CountdownOverlay) ──
        private const float CountdownNumberSize = 96f;
        private const float CountdownStartSize = 34f;
        private static readonly Color CountdownDisc = new Color(0.051f, 0.106f, 0.165f, 0.92f);   // navy #0D1B2A
        private static readonly Color CountdownAccent = new Color(0f, 0.706f, 1f, 1f);            // cyan #00B4FF

        private Image countdownRing;
        private Image countdownDisc;
        private TMP_Text countdownCaption;
        private RectTransform countdownBadge;

        private void CacheCountdownParts()
        {
            if (countdownContainer == null) return;
            var root = countdownContainer.transform;
            countdownBadge = root.Find("Badge") as RectTransform;
            countdownRing = root.Find("Badge/Ring")?.GetComponent<Image>();
            countdownDisc = root.Find("Badge/Disc")?.GetComponent<Image>();
            countdownCaption = root.Find("Caption")?.GetComponent<TMP_Text>();
            var number = root.Find("Badge/Text_Countdown")?.GetComponent<TMP_Text>();
            if (number != null) txtCountdown = number;
        }

        private void SetCountdownVisual(string text, float size, Color discColor, string caption)
        {
            if (txtCountdown != null)
            {
                txtCountdown.text = text;
                txtCountdown.fontSize = size;
                txtCountdown.color = Color.white;
            }
            if (countdownDisc != null) countdownDisc.color = discColor;
            if (countdownCaption != null) countdownCaption.text = caption;
        }

        // t: 0→1 trong một nhịp. Bật lên nhanh ở 35% đầu (ease-out), chữ rõ dần ở 20% đầu.
        private void AnimateCountdownPop(float t)
        {
            float k = Mathf.Clamp01(t / 0.35f);
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            float scale = Mathf.Lerp(1.25f, 1f, ease);
            var target = countdownBadge != null ? (Transform)countdownBadge : (txtCountdown != null ? txtCountdown.transform : null);
            if (target != null) target.localScale = new Vector3(scale, scale, 1f);
            if (txtCountdown != null) txtCountdown.alpha = Mathf.Clamp01(t / 0.2f);
        }

        private void EnsureCountdownUI()
        {
            if (countdownContainer != null && txtCountdown != null)
            {
                if (countdownBadge == null) CacheCountdownParts();
                return;
            }

            var existing = transform.Find("CountdownOverlay");
            if (existing != null)
            {
                countdownContainer = existing.gameObject;
                txtCountdown = existing.GetComponentInChildren<TMP_Text>(true);
                CacheCountdownParts();
                return;
            }

            var cdObj = new GameObject("CountdownOverlay", typeof(RectTransform));
            cdObj.transform.SetParent(transform, false);
            cdObj.transform.SetAsLastSibling(); // Luôn hiển thị trên cùng của màn hình

            var cdRt = cdObj.GetComponent<RectTransform>();
            cdRt.anchorMin = new Vector2(0.5f, 0.5f);
            cdRt.anchorMax = new Vector2(0.5f, 0.5f);
            cdRt.pivot = new Vector2(0.5f, 0.5f);
            cdRt.anchoredPosition = new Vector2(0f, 30f);
            cdRt.sizeDelta = new Vector2(240f, 240f);

            var bgObj = new GameObject("BgCircle", typeof(RectTransform), typeof(Image));
            bgObj.transform.SetParent(cdObj.transform, false);
            var bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0.04f, 0.04f, 0.08f, 0.82f);

            var textObj = new GameObject("Text_Countdown", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(cdObj.transform, false);
            var textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            txtCountdown = textObj.GetComponent<TextMeshProUGUI>();
            txtCountdown.alignment = TextAlignmentOptions.Center;
            txtCountdown.fontSize = 110;
            txtCountdown.fontStyle = FontStyles.Bold;
            txtCountdown.color = new Color(1f, 0.85f, 0.1f, 1f);

            countdownContainer = cdObj;
            countdownContainer.SetActive(false);
        }

        #endregion

        #region UDP Socket Listener (Option B)

        private void StartUdpListener()
        {
            if (isUdpRunning || !enableUdpStream) return;
            try
            {
                isUdpRunning = true;
                lastLivePacketStopwatch.Reset();
                udpThread = new Thread(new ThreadStart(UdpListenWorker));
                udpThread.IsBackground = true;
                udpThread.Start();
                Debug.Log($"[PracticeScreenController] Đã khởi chạy UDP listener tại port {udpPort} (Option B).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PracticeScreenController] Lỗi khởi động UDP listener: {ex.Message}");
            }
        }

        private void StopUdpListener()
        {
            isUdpRunning = false;
            lastLivePacketStopwatch.Reset();
            if (udpClient != null)
            {
                try { udpClient.Close(); } catch { }
                udpClient = null;
            }
            if (udpThread != null && udpThread.IsAlive)
            {
                try { udpThread.Abort(); } catch { }
                udpThread = null;
            }
        }

        private void UdpListenWorker()
        {
            try
            {
                if (udpClient == null)
                {
                    udpClient = new UdpClient();
                    udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, udpPort));
                }

                while (isUdpRunning)
                {
                    IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpClient.Receive(ref anyIP);
                    if (data == null || data.Length == 0) continue;

                    string text = Encoding.UTF8.GetString(data);
                    ParseLiveUdpJson(text);
                }
            }
            catch (SocketException sex)
            {
                if (isUdpRunning) Debug.LogWarning($"[PracticeScreenController] UDP SocketException: {sex.Message}");
            }
            catch (Exception ex)
            {
                if (isUdpRunning) Debug.LogWarning($"[PracticeScreenController] UDP Exception: {ex.Message}");
            }
        }

        private void ParseLiveUdpJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            int sjIdx = text.IndexOf("\"student_joints\":");
            if (sjIdx < 0) return;

            int openBrace = text.IndexOf('{', sjIdx);
            if (openBrace < 0) return;
            int closeBrace = FindClosingBrace(text, openBrace);
            if (closeBrace < 0) return;

            string jointsBlock = text.Substring(openBrace + 1, closeBrace - openBrace - 1);
            var liveDict = new Dictionary<string, JointData>();
            ParseJoints(jointsBlock, liveDict);

            // Trích xuất ảnh camera JPEG nén nếu có trong gói tin UDP
            byte[] decodedImgBytes = null;
            int imgIdx = text.IndexOf("\"image\":");
            if (imgIdx >= 0)
            {
                int q1 = text.IndexOf('"', imgIdx + 8);
                if (q1 >= 0)
                {
                    int q2 = text.IndexOf('"', q1 + 1);
                    if (q2 > q1)
                    {
                        string b64 = text.Substring(q1 + 1, q2 - q1 - 1);
                        try
                        {
                            decodedImgBytes = Convert.FromBase64String(b64);
                        }
                        catch { }
                    }
                }
            }

            lock (udpLock)
            {
                latestLiveJoints = liveDict;
                if (decodedImgBytes != null)
                {
                    latestLiveImageBytes = decodedImgBytes;
                }
                lastLivePacketStopwatch.Restart();
                totalLivePacketsReceived++;
            }
        }

        private void OnDestroy()
        {
            StopUdpListener();
        }

        private void OnApplicationQuit()
        {
            StopUdpListener();
        }

        #endregion

        private void LoadDanceData()
        {
            frames.Clear();
            string jsonText = null;

            if (jsonAsset != null)
            {
                jsonText = jsonAsset.text;
            }

            if (string.IsNullOrEmpty(jsonText))
            {
                string path1 = Path.Combine(Application.streamingAssetsPath, jsonFileName);
                string path2 = Path.Combine(Application.dataPath, "_Project/StreamingAssets", jsonFileName);
                string path3 = Path.Combine(Application.dataPath, "StreamingAssets", jsonFileName);

                if (File.Exists(path1)) jsonText = File.ReadAllText(path1);
                else if (File.Exists(path2)) jsonText = File.ReadAllText(path2);
                else if (File.Exists(path3)) jsonText = File.ReadAllText(path3);
            }

            if (string.IsNullOrEmpty(jsonText))
            {
                Debug.LogError("[PracticeScreenController] Không tìm thấy file JSON bài tập: " + jsonFileName);
                return;
            }

            try
            {
                ParseJsonData(jsonText);
                Debug.Log("[PracticeScreenController] Nạp thành công " + frames.Count + " frames bài tập.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[PracticeScreenController] Lỗi phân tích JSON: " + ex.Message);
            }
        }

        private void ParseJsonData(string json)
        {
            int framesStart = json.IndexOf("\"frames\":");
            if (framesStart == -1) return;

            int arrayStart = json.IndexOf('[', framesStart);
            if (arrayStart == -1) return;

            int arrayEnd = json.LastIndexOf(']');
            if (arrayEnd == -1) return;

            int i = arrayStart + 1;
            while (i < arrayEnd)
            {
                int objStart = json.IndexOf('{', i);
                if (objStart == -1 || objStart >= arrayEnd) break;

                int objEnd = FindClosingBrace(json, objStart);
                if (objEnd == -1) break;

                string frameStr = json.Substring(objStart, objEnd - objStart + 1);
                var item = ParseSingleFrame(frameStr);
                if (item != null) frames.Add(item);

                i = objEnd + 1;
            }
        }

        private int FindClosingBrace(string text, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private FrameItem ParseSingleFrame(string text)
        {
            var fr = new FrameItem();
            fr.frame_index = ExtractInt(text, "frame_index");
            fr.timestamp = ExtractFloat(text, "timestamp");
            fr.frame_score = ExtractFloat(text, "frame_score");
            fr.is_pass = ExtractBool(text, "is_pass");
            fr.cumulative_accuracy = ExtractFloat(text, "cumulative_accuracy");

            int sjStart = text.IndexOf("\"student_joints\":");
            if (sjStart != -1)
            {
                int braceOpen = text.IndexOf('{', sjStart);
                int braceClose = FindClosingBrace(text, braceOpen);
                if (braceOpen != -1 && braceClose != -1)
                {
                    string jointsBlock = text.Substring(braceOpen + 1, braceClose - braceOpen - 1);
                    ParseJoints(jointsBlock, fr.student_joints);
                }
            }

            int tjStart = text.IndexOf("\"teacher_joints\":");
            if (tjStart != -1)
            {
                int braceOpen = text.IndexOf('{', tjStart);
                int braceClose = FindClosingBrace(text, braceOpen);
                if (braceOpen != -1 && braceClose != -1)
                {
                    string jointsBlock = text.Substring(braceOpen + 1, braceClose - braceOpen - 1);
                    ParseJoints(jointsBlock, fr.teacher_joints);
                }
            }

            return fr;
        }

        private void ParseJoints(string text, Dictionary<string, JointData> dict)
        {
            int i = 0;
            while (i < text.Length)
            {
                int quote1 = text.IndexOf('"', i);
                if (quote1 == -1) break;
                int quote2 = text.IndexOf('"', quote1 + 1);
                if (quote2 == -1) break;

                string jointName = text.Substring(quote1 + 1, quote2 - quote1 - 1);

                int braceOpen = text.IndexOf('{', quote2);
                if (braceOpen == -1) break;
                int braceClose = FindClosingBrace(text, braceOpen);
                if (braceClose == -1) break;

                string jBlock = text.Substring(braceOpen, braceClose - braceOpen + 1);
                var jd = new JointData
                {
                    x = ExtractFloat(jBlock, "x"),
                    y = ExtractFloat(jBlock, "y"),
                    is_pass = ExtractInt(jBlock, "is_pass")
                };
                dict[jointName] = jd;

                i = braceClose + 1;
            }
        }

        private int ExtractInt(string text, string key)
        {
            string pattern = "\"" + key + "\":";
            int idx = text.IndexOf(pattern);
            if (idx == -1) return 0;
            int start = idx + pattern.Length;
            int end = text.IndexOfAny(new[] { ',', '}', '\n', '\r' }, start);
            if (end == -1) end = text.Length;
            string val = text.Substring(start, end - start).Trim();
            int.TryParse(val, out int res);
            return res;
        }

        private float ExtractFloat(string text, string key)
        {
            string pattern = "\"" + key + "\":";
            int idx = text.IndexOf(pattern);
            if (idx == -1) return 0f;
            int start = idx + pattern.Length;
            int end = text.IndexOfAny(new[] { ',', '}', '\n', '\r' }, start);
            if (end == -1) end = text.Length;
            string val = text.Substring(start, end - start).Trim();
            float.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float res);
            return res;
        }

        private bool ExtractBool(string text, string key)
        {
            string pattern = "\"" + key + "\":";
            int idx = text.IndexOf(pattern);
            if (idx == -1) return false;
            int start = idx + pattern.Length;
            int end = text.IndexOfAny(new[] { ',', '}', '\n', '\r' }, start);
            if (end == -1) end = text.Length;
            string val = text.Substring(start, end - start).Trim().ToLower();
            return val == "true" || val == "1";
        }
    }
}
