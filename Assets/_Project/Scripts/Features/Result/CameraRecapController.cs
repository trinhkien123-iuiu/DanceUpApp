using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using DanceUp.Data;

namespace DanceUp.Features.Result
{
    /// <summary>
    /// Điều khiển màn hình Camera Recap (Xem lại video luyện tập & đối chiếu chuyển động học viên với giáo viên).
    /// Hỗ trợ:
    /// 1. Phát đồng bộ Video giáo viên mẫu (1_teacher.mp4) và Khung xương học viên (Student Skeleton).
    /// 2. Tua thanh timeline (Scrubber Slider) để xem từng giây.
    /// 3. Chỉnh tốc độ (0.5x, 0.75x, 1.0x, 1.25x), Lặp lại phân đoạn (Loop).
    /// 4. Đánh dấu trực quan khớp lỗi (Màu Đỏ) so với khớp chuẩn (Màu Xanh lá).
    /// </summary>
    public class CameraRecapController : MonoBehaviour
    {
        [Header("=== HEADER & NAVIGATION ===")]
        [SerializeField] private Button btnClose;
        [SerializeField] private TMP_Text txtTitle;
        [SerializeField] private TMP_Text txtSubtitle;

        [Header("=== VIEWPORT: GIÁO VIÊN MẪU ===")]
        [SerializeField] private VideoPlayer vpTeacher;
        [SerializeField] private RawImage imgTeacherVideo;
        [SerializeField] private VideoClip defaultTeacherClip;
        [SerializeField] private TMP_Text txtTeacherLabel;

        [Header("=== VIEWPORT: HỌC VIÊN SKELETON ===")]
        [SerializeField] private RectTransform studentSkeletonContainer;
        [SerializeField] private TMP_Text txtStudentLabel;
        [SerializeField] private TMP_Text txtAccuracyBadge;

        [Header("=== COACH BANNER ===")]
        [SerializeField] private GameObject bannerCoach;
        [SerializeField] private TMP_Text txtCoachAdvice;
        [SerializeField] private TMP_Text txtSegmentBadge;

        [Header("=== TIMELINE SCRUBBER ===")]
        [SerializeField] private Slider sliderTimeline;
        [SerializeField] private TMP_Text txtCurrentTime;
        [SerializeField] private TMP_Text txtTotalTime;
        [SerializeField] private RectTransform problemMarker;

        [Header("=== CONTROLS ===")]
        [SerializeField] private Button btnRewind5s;
        [SerializeField] private Button btnPlayPause;
        [SerializeField] private Image imgPlayPauseIcon;
        [SerializeField] private TMP_Text txtPlayPauseIcon;
        [SerializeField] private Sprite iconPlay;
        [SerializeField] private Sprite iconPause;
        [SerializeField] private Sprite jointSprite;
        [SerializeField] private Button btnForward5s;
        [SerializeField] private Button btnSpeed;
        [SerializeField] private TMP_Text txtSpeed;
        [SerializeField] private Button btnLoop;
        [SerializeField] private Image imgLoopIndicator;
        [SerializeField] private Button btnRetryPractice;

        public event Action OnCloseRequested;
        public event Action OnRetryRequested;

        // Skeleton Visual Elements
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

        private Color colorPass = new Color(0.1f, 0.95f, 0.45f, 1f);   // Xanh lá chuẩn
        private Color colorError = new Color(1f, 0.25f, 0.25f, 1f);   // Đỏ báo lỗi khớp

        // Playback State
        private SessionEvaluationData sessionData;
        private List<FrameEvaluationResult> frames = new List<FrameEvaluationResult>();
        private float currentTime = 0f;
        private float sessionDuration = 30f;
        private float playbackSpeed = 1.0f;
        private bool isPlaying = false;
        private bool isLooping = true;
        private bool isUserDraggingSlider = false;

        // Segment Review Mode
        private bool isSegmentMode = false;
        private float segmentStart = 0f;
        private float segmentEnd = 0f;
        private string highlightedErrorJoint = "";

        private RenderTexture teacherRT;

        private void Awake()
        {
            BindControls();
            InitSkeletonUI();
            SetupTeacherRenderTexture();
        }

        private void SetupTeacherRenderTexture()
        {
            if (teacherRT == null)
            {
                teacherRT = new RenderTexture(480, 640, 0, RenderTextureFormat.ARGB32);
                teacherRT.name = "RT_TeacherRecap";
            }

            if (vpTeacher != null)
            {
                vpTeacher.renderMode = VideoRenderMode.RenderTexture;
                vpTeacher.targetTexture = teacherRT;
            }

            if (imgTeacherVideo != null)
            {
                imgTeacherVideo.texture = teacherRT;
            }
        }

        private void BindControls()
        {
            if (btnClose != null)
            {
                btnClose.onClick.RemoveAllListeners();
                btnClose.onClick.AddListener(CloseRecap);
            }

            if (btnPlayPause != null)
            {
                btnPlayPause.onClick.RemoveAllListeners();
                btnPlayPause.onClick.AddListener(TogglePlayPause);
            }

            if (btnRewind5s != null)
            {
                btnRewind5s.onClick.RemoveAllListeners();
                btnRewind5s.onClick.AddListener(() => SeekTo(currentTime - 5f));
            }

            if (btnForward5s != null)
            {
                btnForward5s.onClick.RemoveAllListeners();
                btnForward5s.onClick.AddListener(() => SeekTo(currentTime + 5f));
            }

            if (btnSpeed != null)
            {
                btnSpeed.onClick.RemoveAllListeners();
                btnSpeed.onClick.AddListener(CyclePlaybackSpeed);
            }

            if (btnLoop != null)
            {
                btnLoop.onClick.RemoveAllListeners();
                btnLoop.onClick.AddListener(ToggleLoop);
            }

            if (btnRetryPractice != null)
            {
                btnRetryPractice.onClick.RemoveAllListeners();
                btnRetryPractice.onClick.AddListener(() => {
                    CloseRecap();
                    OnRetryRequested?.Invoke();
                });
            }

            if (sliderTimeline != null)
            {
                sliderTimeline.onValueChanged.RemoveAllListeners();
                sliderTimeline.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        public void OpenRecap(SessionEvaluationData data, float startSec = 0f, float endSec = -1f, float initialSpeed = 1.0f)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            SetupTeacherRenderTexture();
            InitSkeletonUI();

            sessionData = data;
            playbackSpeed = Mathf.Clamp(initialSpeed, 0.25f, 2.0f);
            if (txtSpeed != null) txtSpeed.text = playbackSpeed.ToString("0.##") + "×";

            // Nạp video mẫu
            if (defaultTeacherClip == null)
            {
                var clips = Resources.FindObjectsOfTypeAll<VideoClip>();
                foreach (var c in clips)
                {
                    if (c.name.IndexOf("teacher", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        defaultTeacherClip = c;
                        break;
                    }
                }
            }

            if (vpTeacher != null)
            {
                if (vpTeacher.clip == null && defaultTeacherClip != null)
                {
                    vpTeacher.clip = defaultTeacherClip;
                }
                vpTeacher.playbackSpeed = playbackSpeed;
            }

            // Nạp dữ liệu Frame Results
            frames.Clear();
            if (sessionData != null && sessionData.frameResults != null && sessionData.frameResults.Count > 0)
            {
                frames = new List<FrameEvaluationResult>(sessionData.frameResults);
            }
            else
            {
                // Fallback nếu không có frames
                LoadDemoFramesFallback();
            }

            // Tính tổng thời lượng
            if (sessionData != null && sessionData.sessionDuration > 1f)
            {
                sessionDuration = sessionData.sessionDuration;
            }
            else if (vpTeacher != null && vpTeacher.clip != null && vpTeacher.clip.length > 1f)
            {
                sessionDuration = (float)vpTeacher.clip.length;
            }
            else if (frames.Count > 0)
            {
                sessionDuration = frames[frames.Count - 1].timestamp;
            }
            else
            {
                sessionDuration = 30f;
            }

            if (txtTotalTime != null) txtTotalTime.text = FormatTime(sessionDuration);

            // Xác định chế độ: Toàn bài hay Đoạn cần chỉnh
            if (endSec > startSec)
            {
                isSegmentMode = true;
                segmentStart = Mathf.Max(0f, startSec);
                segmentEnd = Mathf.Min(sessionDuration, endSec);
                currentTime = segmentStart;

                if (txtSegmentBadge != null)
                {
                    txtSegmentBadge.text = string.Format("ĐOẠN CẦN CHỈNH ({0:00}:{1:00} - {2:00}:{3:00})",
                        Mathf.FloorToInt(segmentStart / 60), Mathf.FloorToInt(segmentStart % 60),
                        Mathf.FloorToInt(segmentEnd / 60), Mathf.FloorToInt(segmentEnd % 60));
                    txtSegmentBadge.gameObject.SetActive(true);
                }

                if (txtCoachAdvice != null)
                {
                    string advice = "Xem lại chuyển động ở tốc độ chậm 0.75× để quan sát chi tiết khớp lỗi.";
                    if (sessionData != null && sessionData.incorrectSections != null && sessionData.incorrectSections.Count > 0)
                    {
                        advice = sessionData.incorrectSections[0].description;
                        highlightedErrorJoint = sessionData.incorrectSections[0].primaryJointError;
                    }
                    txtCoachAdvice.text = advice;
                }

                // Cập nhật marker trên thanh timeline
                UpdateProblemMarker(segmentStart, segmentEnd, sessionDuration);
            }
            else
            {
                isSegmentMode = false;
                segmentStart = 0f;
                segmentEnd = sessionDuration;
                currentTime = 0f;

                if (txtSegmentBadge != null)
                {
                    txtSegmentBadge.text = "TOÀN BỘ PHIÊN TẬP";
                    txtSegmentBadge.gameObject.SetActive(true);
                }

                if (txtCoachAdvice != null)
                {
                    txtCoachAdvice.text = (sessionData != null && !string.IsNullOrEmpty(sessionData.coachAdvice))
                        ? sessionData.coachAdvice
                        : "Quan sát và đối chiếu chuyển động của bạn với mẫu giáo viên.";
                }

                if (problemMarker != null) problemMarker.gameObject.SetActive(false);
            }

            if (txtSubtitle != null && sessionData != null)
            {
                txtSubtitle.text = sessionData.lessonTitle;
            }

            if (txtAccuracyBadge != null && sessionData != null)
            {
                txtAccuracyBadge.text = sessionData.overallAccuracy.ToString("F0") + "% ĐẠT";
            }

            SeekTo(currentTime);
            Play();
        }

        public void CloseRecap()
        {
            Pause();
            gameObject.SetActive(false);
            OnCloseRequested?.Invoke();
        }

        private void Update()
        {
            if (!isPlaying) return;

            currentTime += Time.deltaTime * playbackSpeed;

            // Kiểm tra kết thúc đoạn hoặc kết thúc video
            if (isSegmentMode)
            {
                if (currentTime >= segmentEnd)
                {
                    if (isLooping)
                    {
                        currentTime = segmentStart;
                        SeekTo(currentTime);
                    }
                    else
                    {
                        currentTime = segmentEnd;
                        Pause();
                    }
                }
            }
            else
            {
                if (currentTime >= sessionDuration)
                {
                    if (isLooping)
                    {
                        currentTime = 0f;
                        SeekTo(currentTime);
                    }
                    else
                    {
                        currentTime = sessionDuration;
                        Pause();
                    }
                }
            }

            UpdateTimelineUI();
            RenderFrameAtTime(currentTime);
        }

        public void Play()
        {
            isPlaying = true;
            if (vpTeacher != null && (vpTeacher.clip != null || !string.IsNullOrEmpty(vpTeacher.url)))
            {
                vpTeacher.playbackSpeed = playbackSpeed;
                vpTeacher.time = currentTime;
                vpTeacher.Play();
            }
            UpdatePlayPauseIcon();
        }

        public void Pause()
        {
            isPlaying = false;
            if (vpTeacher != null)
            {
                vpTeacher.Pause();
            }
            UpdatePlayPauseIcon();
        }

        public void TogglePlayPause()
        {
            if (isPlaying) Pause();
            else Play();
        }

        public void SeekTo(float timeSec)
        {
            currentTime = Mathf.Clamp(timeSec, 0f, sessionDuration);

            if (vpTeacher != null && (vpTeacher.clip != null || !string.IsNullOrEmpty(vpTeacher.url)))
            {
                vpTeacher.time = currentTime;
                if (!isPlaying)
                {
                    vpTeacher.Play();
                    vpTeacher.Pause();
                }
            }

            UpdateTimelineUI();
            RenderFrameAtTime(currentTime);
        }

        private void OnSliderChanged(float val)
        {
            float targetTime = val * sessionDuration;
            if (Mathf.Abs(targetTime - currentTime) > 0.3f)
            {
                SeekTo(targetTime);
            }
        }

        private void CyclePlaybackSpeed()
        {
            if (Mathf.Approximately(playbackSpeed, 1.0f)) playbackSpeed = 0.75f;
            else if (Mathf.Approximately(playbackSpeed, 0.75f)) playbackSpeed = 0.5f;
            else if (Mathf.Approximately(playbackSpeed, 0.5f)) playbackSpeed = 1.25f;
            else playbackSpeed = 1.0f;

            if (txtSpeed != null) txtSpeed.text = playbackSpeed.ToString("0.##") + "×";

            if (vpTeacher != null)
            {
                vpTeacher.playbackSpeed = playbackSpeed;
            }
        }

        private void ToggleLoop()
        {
            isLooping = !isLooping;
            if (imgLoopIndicator != null)
            {
                imgLoopIndicator.color = isLooping ? new Color(0f, 0.706f, 1f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.6f);
            }
        }

        private void UpdateTimelineUI()
        {
            if (sliderTimeline != null && sessionDuration > 0.01f)
            {
                sliderTimeline.SetValueWithoutNotify(currentTime / sessionDuration);
            }

            if (txtCurrentTime != null)
            {
                txtCurrentTime.text = FormatTime(currentTime);
            }
        }

        private void UpdatePlayPauseIcon()
        {
            if (imgPlayPauseIcon != null)
            {
                if (isPlaying && iconPause != null) imgPlayPauseIcon.sprite = iconPause;
                else if (!isPlaying && iconPlay != null) imgPlayPauseIcon.sprite = iconPlay;
            }
            if (txtPlayPauseIcon != null)
            {
                txtPlayPauseIcon.text = isPlaying ? "||" : "▶";
            }
        }

        private void UpdateProblemMarker(float start, float end, float total)
        {
            if (problemMarker == null || total <= 0.01f) return;

            problemMarker.gameObject.SetActive(true);
            float startPct = Mathf.Clamp01(start / total);
            float endPct = Mathf.Clamp01(end / total);

            problemMarker.anchorMin = new Vector2(startPct, 0f);
            problemMarker.anchorMax = new Vector2(endPct, 1f);
            problemMarker.offsetMin = Vector2.zero;
            problemMarker.offsetMax = Vector2.zero;
        }

        private void RenderFrameAtTime(float timeSec)
        {
            if (frames == null || frames.Count == 0 || studentSkeletonContainer == null) return;

            // Tìm frame gần nhất với timeSec
            FrameEvaluationResult bestFrame = frames[0];
            float minDiff = float.MaxValue;

            for (int i = 0; i < frames.Count; i++)
            {
                float diff = Mathf.Abs(frames[i].timestamp - timeSec);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestFrame = frames[i];
                }
            }

            // Vẽ Skeleton học viên lên studentSkeletonContainer
            DrawSkeleton(bestFrame);
        }

        private void DrawSkeleton(FrameEvaluationResult fr)
        {
            if (studentSkeletonContainer == null || fr == null || fr.joints == null) return;

            float containerW = studentSkeletonContainer.rect.width > 20f ? studentSkeletonContainer.rect.width : 280f;
            float containerH = studentSkeletonContainer.rect.height > 20f ? studentSkeletonContainer.rect.height : 360f;

            // Cập nhật vị trí các khớp
            foreach (var kvp in fr.joints)
            {
                string jointName = kvp.Key;
                var item = kvp.Value;

                if (jointUIs.TryGetValue(jointName, out var jointRt))
                {
                    // Chuyển đổi tọa độ chuẩn hóa (0..1) sang khung hình Canvas
                    // Y trong Mediapipe là từ trên xuống dưới (0 ở trên, 1 ở dưới)
                    float x = (item.studentPos.x - 0.5f) * containerW;
                    float y = (0.5f - item.studentPos.y) * containerH;

                    jointRt.anchoredPosition = new Vector2(x, y);

                    var img = jointRt.GetComponent<Image>();
                    if (img != null)
                    {
                        bool isFail = item.isPass == 0 || (!string.IsNullOrEmpty(highlightedErrorJoint) && jointName.Equals(highlightedErrorJoint, StringComparison.OrdinalIgnoreCase));
                        img.color = isFail ? colorError : colorPass;
                        jointRt.localScale = isFail ? Vector3.one * 1.35f : Vector3.one;
                    }
                }
            }

            // Cập nhật các đoạn xương
            foreach (var (j1, j2, boneRt) in boneUIs)
            {
                if (fr.joints.TryGetValue(j1, out var p1) && fr.joints.TryGetValue(j2, out var p2))
                {
                    Vector2 posA = new Vector2((p1.studentPos.x - 0.5f) * containerW, (0.5f - p1.studentPos.y) * containerH);
                    Vector2 posB = new Vector2((p2.studentPos.x - 0.5f) * containerW, (0.5f - p2.studentPos.y) * containerH);

                    Vector2 diff = posB - posA;
                    boneRt.anchoredPosition = (posA + posB) / 2f;
                    boneRt.sizeDelta = new Vector2(4f, diff.magnitude);

                    float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg - 90f;
                    boneRt.rotation = Quaternion.Euler(0f, 0f, angle);

                    var img = boneRt.GetComponent<Image>();
                    if (img != null)
                    {
                        bool isPass = (p1.isPass == 1 && p2.isPass == 1);
                        img.color = isPass ? new Color(colorPass.r, colorPass.g, colorPass.b, 0.75f) : new Color(colorError.r, colorError.g, colorError.b, 0.85f);
                    }
                }
            }
        }

        private void InitSkeletonUI()
        {
            if (studentSkeletonContainer == null) return;

            if (jointUIs.Count > 0 && boneUIs.Count > 0) return;

            jointUIs.Clear();
            boneUIs.Clear();

            // Xóa rác cũ
            for (int i = studentSkeletonContainer.childCount - 1; i >= 0; i--)
            {
                var c = studentSkeletonContainer.GetChild(i);
                if (c.name.StartsWith("Joint_") || c.name.StartsWith("Bone_"))
                {
                    DestroyImmediate(c.gameObject);
                }
            }

            // Tạo Bones trước (để nằm dưới các khớp)
            foreach (var pair in bonePairs)
            {
                var boneObj = new GameObject($"Bone_{pair[0]}_{pair[1]}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                boneObj.transform.SetParent(studentSkeletonContainer, false);

                var rt = boneObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(4f, 20f);

                var img = boneObj.GetComponent<Image>();
                img.color = colorPass;
                img.raycastTarget = false;

                boneUIs.Add((pair[0], pair[1], rt));
            }

            // Tạo Joints
            string[] jointNames = {
                "nose", "left_shoulder", "right_shoulder",
                "left_elbow", "right_elbow", "left_wrist", "right_wrist",
                "left_hip", "right_hip", "left_knee", "right_knee",
                "left_ankle", "right_ankle"
            };

            foreach (var jName in jointNames)
            {
                var jointObj = new GameObject($"Joint_{jName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                jointObj.transform.SetParent(studentSkeletonContainer, false);

                var rt = jointObj.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(16f, 16f);

                var img = jointObj.GetComponent<Image>();
                if (jointSprite != null) img.sprite = jointSprite;
                img.color = colorPass;
                img.raycastTarget = false;

                jointUIs[jName] = rt;
            }
        }

        private void LoadDemoFramesFallback()
        {
            try
            {
                string jsonPath = Path.Combine(Application.streamingAssetsPath, "dance_demo_data.json");
                if (!File.Exists(jsonPath))
                {
                    jsonPath = Path.Combine(Application.dataPath, "_Project/StreamingAssets/dance_demo_data.json");
                }

                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    // Parse demo JSON
                    ParseDemoJson(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CameraRecap] Fallback demo JSON error: " + ex.Message);
            }
        }

        [Serializable]
        private class DemoRoot
        {
            public float fps;
            public int total_frames;
            public List<DemoFrame> frames;
        }

        [Serializable]
        private class DemoJoint { public float x; public float y; public int is_pass; }

        [Serializable]
        private class DemoFrame
        {
            public int frame_index;
            public float timestamp;
            public float frame_score;
            public bool is_pass;
            public Dictionary<string, DemoJoint> student_joints;
        }

        private void ParseDemoJson(string json)
        {
            // Simple manual parse for demo frames if JsonUtility dictionary fails
            // In Unity, demo_data can be adapted
            int count = 180; // 6 seconds at 30 fps
            for (int i = 0; i < count; i++)
            {
                float t = i / 30f;
                var fr = new FrameEvaluationResult
                {
                    frameIndex = i,
                    timestamp = t,
                    frameScore = 11.5f,
                    isPass = true,
                    cumulativeAccuracy = 88f
                };

                // Fake smooth movement
                float sway = Mathf.Sin(t * 3f) * 0.15f;
                AddJoint(fr, "nose", 0.5f + sway * 0.3f, 0.2f);
                AddJoint(fr, "left_shoulder", 0.42f + sway, 0.32f);
                AddJoint(fr, "right_shoulder", 0.58f + sway, 0.32f);
                AddJoint(fr, "left_elbow", 0.35f + sway * 1.2f, 0.45f);
                AddJoint(fr, "right_elbow", 0.65f + sway * 1.2f, 0.45f);
                AddJoint(fr, "left_wrist", 0.30f + sway * 1.5f, 0.58f);
                AddJoint(fr, "right_wrist", 0.70f + sway * 1.5f, 0.58f);
                AddJoint(fr, "left_hip", 0.45f + sway * 0.5f, 0.58f);
                AddJoint(fr, "right_hip", 0.55f + sway * 0.5f, 0.58f);
                AddJoint(fr, "left_knee", 0.44f + sway * 0.4f, 0.75f);
                AddJoint(fr, "right_knee", 0.56f + sway * 0.4f, 0.75f);
                AddJoint(fr, "left_ankle", 0.43f + sway * 0.2f, 0.92f);
                AddJoint(fr, "right_ankle", 0.57f + sway * 0.2f, 0.92f);

                frames.Add(fr);
            }
        }

        private void AddJoint(FrameEvaluationResult fr, string name, float x, float y)
        {
            fr.joints[name] = new JointScoreItem
            {
                jointName = name,
                studentPos = new Vector2(x, y),
                teacherPos = new Vector2(x, y),
                isPass = 1
            };
        }

        private string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return string.Format("{0:00}:{1:00}", m, s);
        }

        private void OnDestroy()
        {
            if (teacherRT != null)
            {
                teacherRT.Release();
                teacherRT = null;
            }
        }
    }
}
