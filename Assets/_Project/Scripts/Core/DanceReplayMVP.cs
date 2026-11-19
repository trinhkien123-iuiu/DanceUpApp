using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using DanceUp.AI;

/// <summary>
/// DanceReplayMVP - Trực quan hóa và đánh giá động tác nhảy thời gian thực trong Unity
/// Đề tài NCKH: Dance Movement Evaluation (Chuẩn processing_11)
/// 
/// Hỗ trợ cả 2 chế độ:
/// 1. Offline JSON Replay: Phát lại dữ liệu mẫu đã tính toán sẵn.
/// 2. Live UDP Stream: Tự động chạy Python MediaPipe Pose Extractor ngầm,
///    nhận tọa độ 13 khớp trực tiếp, đánh giá sai số so với giáo viên theo pipeline processing_11,
///    phán quyết Đạt (Xanh)/Lỗi (Đỏ) từng khớp trong từng khung hình.
/// </summary>
public class DanceReplayMVP : MonoBehaviour
{
    public enum PlayMode
    {
        LiveUdpStream,
        OfflineJsonReplay
    }

    [Header("=== CẤU HÌNH CHẾ ĐỘ ===")]
    [Tooltip("LiveUdpStream: Nhận stream MediaPipe từ Python. OfflineJsonReplay: Phát lại file demo.")]
    public PlayMode mode = PlayMode.LiveUdpStream;
    public bool autoStartPythonInLiveMode = true;

    [Header("=== CẤU HÌNH OFFLINE / GIÁO VIÊN MẪU ===")]
    [Tooltip("Kéo thả file dance_demo_data.json vào đây, hoặc để trống để tự nạp từ StreamingAssets")]
    public TextAsset jsonFileAsset;
    public string jsonFileName = "dance_demo_data.json";
    public bool autoPlay = true;
    public bool loop = true;
    [Range(0.2f, 2.0f)] public float playbackSpeed = 1.0f;

    [Header("=== CẤU HÌNH LIVE UDP ===")]
    public int udpPort = 5065;
    public float livePoseTimeout = 2.0f;

    [Header("=== THIẾT LẬP HIỂN THỊ SKELETON 3D ===")]
    public float skeletonScale = 4.0f;
    public Vector3 teacherOffset = new Vector3(-2.2f, -1.5f, 0f);
    public Vector3 studentOffset = new Vector3(2.2f, -1.5f, 0f);
    public float jointRadius = 0.09f;
    public float boneWidth = 0.04f;

    [Header("=== MÀU SẮC TRỰC QUAN (CHUẨN XANH / ĐỎ) ===")]
    public Color teacherColor = new Color(0.2f, 0.7f, 1.0f, 1.0f);   // Cyan (Mẫu Giáo viên)
    public Color passJointColor = new Color(0.1f, 0.9f, 0.2f, 1.0f);  // Xanh lá (Đạt)
    public Color failJointColor = new Color(0.95f, 0.2f, 0.2f, 1.0f); // Đỏ (Lỗi khớp)
    public Color passBoneColor = new Color(0.2f, 0.85f, 0.3f, 0.7f);
    public Color failBoneColor = new Color(0.9f, 0.25f, 0.25f, 0.7f);
    public Color teacherBoneColor = new Color(0.4f, 0.8f, 1.0f, 0.6f);

    // Dữ liệu nội bộ
    [System.Serializable]
    public class JointCoord
    {
        public float x;
        public float y;
        public int is_pass = 1;
        public float joint_error = 0f;
        public float joint_cosine = 1f;
        public float velocity_error = 0f;
    }

    [System.Serializable]
    public class FrameData
    {
        public int frame_index;
        public int teacher_frame_index;
        public float timestamp;
        public float frame_score;
        public float max_score = 13.0f;
        public bool is_pass;
        public float cumulative_accuracy;
        public Dictionary<string, JointCoord> student_joints = new Dictionary<string, JointCoord>();
        public Dictionary<string, JointCoord> teacher_joints = new Dictionary<string, JointCoord>();
    }

    [System.Serializable]
    public class DemoRootData
    {
        public string project;
        public int video_id;
        public float fps;
        public int total_frames;
        public float score_threshold;
        public List<List<string>> bones;
        public List<FrameData> frames;
    }

    private DemoRootData rootData;
    private List<string[]> bonePairs = new List<string[]>();
    private int currentFrameIdx = 0;
    private float frameTimer = 0f;
    private bool isPaused = false;
    private FrameData currentFrame;

    // UDP Threading
    private Thread udpThread;
    private UdpClient udpClient;
    private string latestUdpJson = "";
    private readonly object lockObj = new object();
    private bool hasNewUdpPacket = false;
    private volatile bool isUdpRunning = false;
    private System.Diagnostics.Stopwatch lastLivePacketWatch = new System.Diagnostics.Stopwatch();
    private int totalPacketsReceived = 0;

    // AI Pipeline
    private DanceMLPipeline mlPipeline;
    private string coachAdvice = "Sẵn sàng... Bắt đầu chuyển động theo bài nhảy!";

    // Visual Objects
    private class SkeletonVisual
    {
        public GameObject root;
        public Dictionary<string, GameObject> joints = new Dictionary<string, GameObject>();
        public List<LineRenderer> bones = new List<LineRenderer>();
        public List<string[]> boneJointNames = new List<string[]>();
    }

    private SkeletonVisual teacherVisual = new SkeletonVisual();
    private SkeletonVisual studentVisual = new SkeletonVisual();

    private Material jointMaterial;
    private Material boneMaterial;

    void Start()
    {
        CreateDefaultMaterials();
        InitDefaultBones();

        // 1. Luôn nạp file JSON bài mẫu để lấy chuyển động giáo viên chuẩn
        LoadTeacherReferenceJson();

        // 2. Khởi tạo Pipeline ML
        mlPipeline = GetComponent<DanceMLPipeline>();
        if (mlPipeline == null) mlPipeline = gameObject.AddComponent<DanceMLPipeline>();
        mlPipeline.InitModel();

        // 3. Khởi tạo bộ xương 3D
        CreateSkeletonVisuals();

        // 4. Nếu ở chế độ Live UDP, tự động khởi chạy Python MediaPipe trực tiếp từ Unity
        if (mode == PlayMode.LiveUdpStream)
        {
            StartLiveUdpPipeline();
        }
    }

    public void StartLiveUdpPipeline()
    {
        mode = PlayMode.LiveUdpStream;
        StartUdpListener();

        if (autoStartPythonInLiveMode)
        {
            PythonPoseService.Instance.StartService(true);
        }
    }

    public void SwitchToOfflineReplay()
    {
        mode = PlayMode.OfflineJsonReplay;
        currentFrameIdx = 0;
        frameTimer = 0f;
    }

    void CreateDefaultMaterials()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        jointMaterial = new Material(shader);
        boneMaterial = new Material(shader);
    }

    void InitDefaultBones()
    {
        bonePairs = new List<string[]>
        {
            new string[] { "nose", "left_shoulder" },
            new string[] { "nose", "right_shoulder" },
            new string[] { "left_shoulder", "right_shoulder" },
            new string[] { "left_shoulder", "left_elbow" },
            new string[] { "left_elbow", "left_wrist" },
            new string[] { "right_shoulder", "right_elbow" },
            new string[] { "right_elbow", "right_wrist" },
            new string[] { "left_shoulder", "left_hip" },
            new string[] { "right_shoulder", "right_hip" },
            new string[] { "left_hip", "right_hip" },
            new string[] { "left_hip", "left_knee" },
            new string[] { "left_knee", "left_ankle" },
            new string[] { "right_hip", "right_knee" },
            new string[] { "right_knee", "right_ankle" }
        };
    }

    void LoadTeacherReferenceJson()
    {
        string rawJson = "";
        if (jsonFileAsset != null)
        {
            rawJson = jsonFileAsset.text;
        }
        else
        {
            string path1 = Path.Combine(Application.streamingAssetsPath, jsonFileName);
            string path2 = Path.Combine(Application.dataPath, "_Project/StreamingAssets", jsonFileName);
            string path3 = Path.Combine(Application.dataPath, "StreamingAssets", jsonFileName);
            string path4 = Path.Combine(Application.dataPath, jsonFileName);

            if (File.Exists(path1)) rawJson = File.ReadAllText(path1);
            else if (File.Exists(path2)) rawJson = File.ReadAllText(path2);
            else if (File.Exists(path3)) rawJson = File.ReadAllText(path3);
            else if (File.Exists(path4)) rawJson = File.ReadAllText(path4);
            else
            {
                Debug.LogWarning($"[DanceMVP] Không tìm thấy file JSON tại: {path1}");
            }
        }

        if (!string.IsNullOrEmpty(rawJson))
        {
            ParseJsonData(rawJson);
        }
    }

    void ParseJsonData(string jsonText)
    {
        try
        {
            rootData = ParseFullJson(jsonText);
            if (rootData != null && rootData.frames.Count > 0)
            {
                currentFrame = rootData.frames[0];
                Debug.Log($"[DanceMVP] Nạp thành công {rootData.total_frames} khung hình mẫu (Video #{rootData.video_id})");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DanceMVP] Lỗi khi parse JSON: {ex.Message}");
        }
    }

    void CreateSkeletonVisuals()
    {
        if (teacherVisual.root == null)
        {
            teacherVisual.root = new GameObject("Teacher_Skeleton");
            teacherVisual.root.transform.position = teacherOffset;
            teacherVisual.root.transform.parent = transform;
        }

        if (studentVisual.root == null)
        {
            studentVisual.root = new GameObject("Student_Skeleton");
            studentVisual.root.transform.position = studentOffset;
            studentVisual.root.transform.parent = transform;
        }

        string[] jointNames = {
            "nose", "left_shoulder", "right_shoulder", "left_elbow", "right_elbow",
            "left_wrist", "right_wrist", "left_hip", "right_hip", "left_knee", "right_knee",
            "left_ankle", "right_ankle"
        };

        SetupSkeletonHierarchy(teacherVisual, jointNames, teacherColor, teacherBoneColor);
        SetupSkeletonHierarchy(studentVisual, jointNames, passJointColor, passBoneColor);
    }

    void SetupSkeletonHierarchy(SkeletonVisual skel, string[] jointNames, Color defaultColor, Color defaultBoneColor)
    {
        foreach (string jName in jointNames)
        {
            if (skel.joints.ContainsKey(jName)) continue;

            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = jName;
            sphere.transform.parent = skel.root.transform;
            sphere.transform.localScale = Vector3.one * (jointRadius * 2f);

            Destroy(sphere.GetComponent<Collider>());

            Renderer r = sphere.GetComponent<Renderer>();
            r.material = new Material(jointMaterial);
            r.material.color = defaultColor;

            skel.joints[jName] = sphere;
        }

        foreach (string[] pair in bonePairs)
        {
            string boneName = $"Bone_{pair[0]}_{pair[1]}";
            var existing = skel.root.transform.Find(boneName);
            if (existing != null) continue;

            GameObject boneObj = new GameObject(boneName);
            boneObj.transform.parent = skel.root.transform;

            LineRenderer lr = boneObj.AddComponent<LineRenderer>();
            lr.material = new Material(boneMaterial);
            lr.startColor = defaultBoneColor;
            lr.endColor = defaultBoneColor;
            lr.startWidth = boneWidth;
            lr.endWidth = boneWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = true;

            skel.bones.Add(lr);
            skel.boneJointNames.Add(pair);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            currentFrameIdx = 0;
            frameTimer = 0f;
            if (mlPipeline != null) mlPipeline.ResetSession();
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (mode == PlayMode.OfflineJsonReplay) StartLiveUdpPipeline();
            else SwitchToOfflineReplay();
        }

        if (mode == PlayMode.OfflineJsonReplay)
        {
            UpdateOfflineReplay();
        }
        else
        {
            UpdateLiveUdp();
        }

        RenderFrame();
    }

    void UpdateOfflineReplay()
    {
        if (rootData == null || rootData.frames == null || rootData.frames.Count == 0 || isPaused)
            return;

        float fps = rootData.fps > 0 ? rootData.fps : 30f;
        frameTimer += Time.deltaTime * playbackSpeed;

        if (frameTimer >= (1f / fps))
        {
            frameTimer = 0f;
            currentFrameIdx++;
            if (currentFrameIdx >= rootData.frames.Count)
            {
                currentFrameIdx = loop ? 0 : rootData.frames.Count - 1;
            }
            currentFrame = rootData.frames[currentFrameIdx];
        }
    }

    void UpdateLiveUdp()
    {
        string jsonToProcess = null;
        lock (lockObj)
        {
            if (hasNewUdpPacket)
            {
                jsonToProcess = latestUdpJson;
                hasNewUdpPacket = false;
            }
        }

        // Tăng thời gian phát mẫu của giáo viên theo nhịp 30 FPS
        if (!isPaused && rootData != null && rootData.frames.Count > 0)
        {
            frameTimer += Time.deltaTime;
            if (frameTimer >= (1f / 30f))
            {
                frameTimer = 0f;
                currentFrameIdx = (currentFrameIdx + 1) % rootData.frames.Count;
            }
        }

        FrameData teacherFrame = (rootData != null && rootData.frames.Count > 0) ? rootData.frames[currentFrameIdx] : null;

        if (!string.IsNullOrEmpty(jsonToProcess))
        {
            // Trích xuất student joints từ packet UDP của Python
            Dictionary<string, JointCoord> rawStudentJoints = ParseJointsDict(jsonToProcess, "student_joints");

            if (rawStudentJoints != null && rawStudentJoints.Count > 0 && teacherFrame != null && teacherFrame.teacher_joints.Count > 0)
            {
                // Thực hiện đánh giá pipeline processing_11 đầy đủ:
                // 1. Chuyển đổi dữ liệu thô sang Dictionary<string, Vector2>
                var sDict = new Dictionary<string, Vector2>();
                foreach (var kvp in rawStudentJoints) sDict[kvp.Key] = new Vector2(kvp.Value.x, kvp.Value.y);

                var tDict = new Dictionary<string, Vector2>();
                foreach (var kvp in teacherFrame.teacher_joints) tDict[kvp.Key] = new Vector2(kvp.Value.x, kvp.Value.y);

                // 2. Gọi DanceMLPipeline (chuẩn hóa hình học, trích xuất 3 đặc trưng, suy luận ONNX / CatBoost)
                var evalRes = mlPipeline.EvaluateFrame(currentFrameIdx, (float)currentFrameIdx / 30f, sDict, tDict);

                // 3. Cập nhật FrameData hiện tại
                var fd = new FrameData
                {
                    frame_index = currentFrameIdx,
                    teacher_frame_index = teacherFrame.teacher_frame_index,
                    timestamp = (float)currentFrameIdx / 30f,
                    frame_score = evalRes.frameScore,
                    max_score = 13.0f,
                    is_pass = evalRes.isPass,
                    cumulative_accuracy = evalRes.cumulativeAccuracy,
                    teacher_joints = teacherFrame.teacher_joints
                };

                foreach (var kvp in evalRes.joints)
                {
                    fd.student_joints[kvp.Key] = new JointCoord
                    {
                        x = kvp.Value.studentPos.x,
                        y = kvp.Value.studentPos.y,
                        is_pass = kvp.Value.isPass,
                        joint_error = kvp.Value.jointError,
                        joint_cosine = kvp.Value.jointCosine,
                        velocity_error = kvp.Value.velocityError
                    };
                }

                currentFrame = fd;
                coachAdvice = mlPipeline.GenerateCoachAdvice(evalRes);
            }
            else if (rawStudentJoints != null && rawStudentJoints.Count > 0)
            {
                // Có học viên nhưng chưa có giáo viên mẫu
                var fd = new FrameData
                {
                    frame_index = currentFrameIdx,
                    timestamp = Time.time,
                    frame_score = 13f,
                    is_pass = true,
                    cumulative_accuracy = 100f,
                    student_joints = rawStudentJoints
                };
                currentFrame = fd;
            }
        }
        else if (currentFrame == null && teacherFrame != null)
        {
            currentFrame = teacherFrame;
        }
    }

    void RenderFrame()
    {
        if (currentFrame == null) return;

        // Cập nhật Teacher Skeleton (Cyan)
        UpdateSkeletonJoints(teacherVisual, currentFrame.teacher_joints, teacherOffset, false);

        // Cập nhật Student Skeleton (Green: Đạt, Red: Lỗi)
        UpdateSkeletonJoints(studentVisual, currentFrame.student_joints, studentOffset, true);
    }

    void UpdateSkeletonJoints(SkeletonVisual skel, Dictionary<string, JointCoord> jointsData, Vector3 baseOffset, bool isStudent)
    {
        if (jointsData == null) return;

        foreach (var kvp in jointsData)
        {
            string jName = kvp.Key;
            JointCoord coord = kvp.Value;

            if (skel.joints.ContainsKey(jName))
            {
                GameObject obj = skel.joints[jName];
                Vector3 worldPos = baseOffset + new Vector3(
                    (coord.x - 0.5f) * skeletonScale,
                    (coord.y - 0.5f) * skeletonScale,
                    0f
                );
                obj.transform.position = worldPos;

                Renderer r = obj.GetComponent<Renderer>();
                if (r != null)
                {
                    if (isStudent)
                    {
                        r.material.color = (coord.is_pass == 1) ? passJointColor : failJointColor;
                    }
                    else
                    {
                        r.material.color = teacherColor;
                    }
                }
            }
        }

        // Cập nhật các đường xương
        for (int i = 0; i < skel.bones.Count; i++)
        {
            LineRenderer lr = skel.bones[i];
            string[] pair = skel.boneJointNames[i];

            if (skel.joints.ContainsKey(pair[0]) && skel.joints.ContainsKey(pair[1]))
            {
                Vector3 p0 = skel.joints[pair[0]].transform.position;
                Vector3 p1 = skel.joints[pair[1]].transform.position;
                lr.SetPosition(0, p0);
                lr.SetPosition(1, p1);

                if (isStudent)
                {
                    bool p0Pass = jointsData.ContainsKey(pair[0]) && jointsData[pair[0]].is_pass == 1;
                    bool p1Pass = jointsData.ContainsKey(pair[1]) && jointsData[pair[1]].is_pass == 1;
                    Color boneClr = (p0Pass && p1Pass) ? passBoneColor : failBoneColor;
                    lr.startColor = boneClr;
                    lr.endColor = boneClr;
                }
                else
                {
                    lr.startColor = teacherBoneColor;
                    lr.endColor = teacherBoneColor;
                }
            }
        }
    }

    void OnGUI()
    {
        // HUD Dashboard phong cách Cyberpunk / Dark UI
        GUI.color = Color.white;
        int boxWidth = 400;
        int boxHeight = 310;
        Rect hudRect = new Rect(20, 20, boxWidth, boxHeight);
        GUI.Box(hudRect, "DANCE EVALUATION SYSTEM - AI MVP");

        GUILayout.BeginArea(new Rect(35, 45, boxWidth - 30, boxHeight - 50));

        // Thanh chuyển đổi chế độ và trạng thái Python
        GUILayout.BeginHorizontal();
        if (mode == PlayMode.LiveUdpStream)
        {
            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.3f);
            GUILayout.Button("CHẾ ĐỘ: LIVE AI STREAM", GUILayout.Height(26));
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Sang Demo Replay", GUILayout.Width(130), GUILayout.Height(26)))
            {
                SwitchToOfflineReplay();
            }
        }
        else
        {
            GUI.backgroundColor = new Color(0.2f, 0.7f, 1f);
            GUILayout.Button("CHẾ ĐỘ: OFFLINE REPLAY", GUILayout.Height(26));
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Sang Live AI Stream", GUILayout.Width(130), GUILayout.Height(26)))
            {
                StartLiveUdpPipeline();
            }
        }
        GUILayout.EndHorizontal();

        // Trạng thái Python Service
        GUILayout.BeginHorizontal();
        bool isPyRunning = PythonPoseService.Instance.IsServiceRunning;
        string pyStatus = isPyRunning ? "<color=#00FF66>● Python Service: ĐANG CHẠY</color>" : "<color=#FF4444>○ Python Service: ĐÃ DỪNG</color>";
        GUIStyle richStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12 };
        GUILayout.Label(pyStatus, richStyle);

        if (!isPyRunning)
        {
            if (GUILayout.Button("Khởi động Python", GUILayout.Width(120)))
            {
                PythonPoseService.Instance.StartService(false);
            }
        }
        else
        {
            if (GUILayout.Button("Dừng Python", GUILayout.Width(100)))
            {
                PythonPoseService.Instance.StopService();
            }
        }
        GUILayout.EndHorizontal();

        if (currentFrame != null)
        {
            GUILayout.Label($"<b>Khung hình:</b> {currentFrame.frame_index} / {(rootData != null ? rootData.total_frames : 0)} ({currentFrame.timestamp:F2}s)");

            // Điểm số khung hình
            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            GUILayout.Label($"<b>Điểm khung hình:</b> {currentFrame.frame_score:F1} / 13.0", scoreStyle);

            // Badge PASS / FAIL
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            if (currentFrame.is_pass)
            {
                GUI.backgroundColor = new Color(0.1f, 0.85f, 0.25f);
                GUILayout.Button("TRẠNG THÁI: PASS (ĐẠT CHUẨN)", badgeStyle, GUILayout.Height(28));
            }
            else
            {
                GUI.backgroundColor = new Color(0.95f, 0.2f, 0.2f);
                GUILayout.Button("TRẠNG THÁI: FAIL (LỖI ĐỘNG TÁC)", badgeStyle, GUILayout.Height(28));
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Label($"<b>Độ chính xác tích lũy:</b> {currentFrame.cumulative_accuracy:F1}%");

            // Lời khuyên HLV AI
            GUILayout.Label($"<b>HLV AI:</b> <i>\"{coachAdvice}\"</i>");
        }
        else
        {
            GUILayout.Label("Đang chờ dữ liệu khung hình hoặc MediaPipe...");
        }

        // Điều khiển phím tắt
        GUILayout.Space(4);
        GUILayout.Label("<size=11>[Space]: Tạm dừng | [R]: Reset | [M]: Đổi chế độ</size>", richStyle);

        GUILayout.EndArea();

        // Nhãn chỉ dẫn phía dưới hai nhân vật
        if (Camera.main != null)
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            Vector3 screenTeacher = Camera.main.WorldToScreenPoint(teacherOffset + Vector3.down * 1.6f);
            Vector3 screenStudent = Camera.main.WorldToScreenPoint(studentOffset + Vector3.down * 1.6f);

            GUI.color = teacherColor;
            GUI.Label(new Rect(screenTeacher.x - 100, Screen.height - screenTeacher.y, 200, 30), "GIÁO VIÊN (MẪU CHUẨN)", labelStyle);

            GUI.color = Color.white;
            GUI.Label(new Rect(screenStudent.x - 100, Screen.height - screenStudent.y, 200, 30), "HỌC VIÊN (AI ĐÁNH GIÁ LIVE)", labelStyle);
        }
    }

    void StartUdpListener()
    {
        if (isUdpRunning) return;
        try
        {
            isUdpRunning = true;
            lastLivePacketWatch.Reset();
            udpThread = new Thread(new ThreadStart(UdpListenWorker));
            udpThread.IsBackground = true;
            udpThread.Start();
            Debug.Log($"[DanceMVP] Đang lắng nghe UDP port {udpPort} (MediaPipe AI Stream)...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DanceMVP] Lỗi khởi chạy UDP listener: {ex.Message}");
        }
    }

    void StopUdpListener()
    {
        isUdpRunning = false;
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

    void UdpListenWorker()
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
                lock (lockObj)
                {
                    latestUdpJson = text;
                    hasNewUdpPacket = true;
                    totalPacketsReceived++;
                    lastLivePacketWatch.Restart();
                }
            }
        }
        catch (SocketException sex)
        {
            if (isUdpRunning) Debug.LogWarning($"[DanceMVP] UDP Socket: {sex.Message}");
        }
        catch (Exception ex)
        {
            if (isUdpRunning) Debug.LogWarning($"[DanceMVP] UDP Exception: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        StopUdpListener();
    }

    void OnApplicationQuit()
    {
        StopUdpListener();
    }

    // ==========================================
    // JSON PARSER TỰ VIẾT (Không phụ thuộc plugin ngoài)
    // ==========================================
    DemoRootData ParseFullJson(string json)
    {
        DemoRootData data = new DemoRootData();
        data.frames = new List<FrameData>();

        data.project = ExtractString(json, "project");
        data.fps = ExtractFloat(json, "fps", 30f);
        data.total_frames = ExtractInt(json, "total_frames", 0);
        data.score_threshold = ExtractFloat(json, "score_threshold", 7.1f);

        int framesIdx = json.IndexOf("\"frames\":");
        if (framesIdx < 0) return data;

        int startBracket = json.IndexOf('[', framesIdx);
        int endBracket = json.LastIndexOf(']');
        if (startBracket < 0 || endBracket < 0) return data;

        string framesArrayStr = json.Substring(startBracket + 1, endBracket - startBracket - 1);
        List<string> frameBlocks = SplitJsonObjects(framesArrayStr);

        foreach (string block in frameBlocks)
        {
            FrameData fd = ParseSingleFrameJson(block);
            if (fd != null) data.frames.Add(fd);
        }

        return data;
    }

    FrameData ParseSingleFrameJson(string block)
    {
        FrameData fd = new FrameData();
        fd.frame_index = ExtractInt(block, "frame_index", 0);
        fd.teacher_frame_index = ExtractInt(block, "teacher_frame_index", 0);
        fd.timestamp = ExtractFloat(block, "timestamp", 0f);
        fd.frame_score = ExtractFloat(block, "frame_score", 0f);
        fd.max_score = ExtractFloat(block, "max_score", 13f);
        fd.is_pass = block.Contains("\"is_pass\": true");
        fd.cumulative_accuracy = ExtractFloat(block, "cumulative_accuracy", 0f);

        fd.student_joints = ParseJointsDict(block, "student_joints");
        fd.teacher_joints = ParseJointsDict(block, "teacher_joints");

        return fd;
    }

    Dictionary<string, JointCoord> ParseJointsDict(string parentJson, string dictKey)
    {
        Dictionary<string, JointCoord> dict = new Dictionary<string, JointCoord>();
        int keyPos = parentJson.IndexOf($"\"{dictKey}\":");
        if (keyPos < 0) return dict;

        int openBrace = parentJson.IndexOf('{', keyPos);
        if (openBrace < 0) return dict;

        int closeBrace = FindClosingBrace(parentJson, openBrace);
        if (closeBrace < 0) return dict;

        string sub = parentJson.Substring(openBrace + 1, closeBrace - openBrace - 1);
        string[] jointNames = {
            "nose", "left_shoulder", "right_shoulder", "left_elbow", "right_elbow",
            "left_wrist", "right_wrist", "left_hip", "right_hip", "left_knee", "right_knee",
            "left_ankle", "right_ankle"
        };

        foreach (string jName in jointNames)
        {
            int jPos = sub.IndexOf($"\"{jName}\":");
            if (jPos >= 0)
            {
                int jOpen = sub.IndexOf('{', jPos);
                int jClose = FindClosingBrace(sub, jOpen);
                if (jOpen >= 0 && jClose > jOpen)
                {
                    string jBlock = sub.Substring(jOpen, jClose - jOpen + 1);
                    JointCoord jc = new JointCoord();
                    jc.x = ExtractFloat(jBlock, "x", 0.5f);
                    jc.y = ExtractFloat(jBlock, "y", 0.5f);
                    jc.is_pass = ExtractInt(jBlock, "is_pass", 1);
                    jc.joint_error = ExtractFloat(jBlock, "joint_error", 0f);
                    dict[jName] = jc;
                }
            }
        }

        return dict;
    }

    int FindClosingBrace(string text, int openPos)
    {
        int depth = 0;
        for (int i = openPos; i < text.Length; i++)
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

    List<string> SplitJsonObjects(string arrayContent)
    {
        List<string> objects = new List<string>();
        int depth = 0;
        int start = -1;

        for (int i = 0; i < arrayContent.Length; i++)
        {
            char c = arrayContent[i];
            if (c == '{')
            {
                if (depth == 0) start = i;
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    objects.Add(arrayContent.Substring(start, i - start + 1));
                    start = -1;
                }
            }
        }
        return objects;
    }

    float ExtractFloat(string json, string key, float defaultVal)
    {
        string pattern = $"\"{key}\":";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return defaultVal;
        int valStart = idx + pattern.Length;
        int valEnd = json.IndexOfAny(new char[] { ',', '}', '\n', '\r' }, valStart);
        if (valEnd < 0) valEnd = json.Length;
        string numStr = json.Substring(valStart, valEnd - valStart).Trim();
        if (float.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float res))
            return res;
        return defaultVal;
    }

    int ExtractInt(string json, string key, int defaultVal)
    {
        return (int)ExtractFloat(json, key, defaultVal);
    }

    string ExtractString(string json, string key)
    {
        string pattern = $"\"{key}\":";
        int idx = json.IndexOf(pattern);
        if (idx < 0) return "";
        int valStart = json.IndexOf('"', idx + pattern.Length);
        if (valStart < 0) return "";
        int valEnd = json.IndexOf('"', valStart + 1);
        if (valEnd < 0) return "";
        return json.Substring(valStart + 1, valEnd - valStart - 1);
    }
}
