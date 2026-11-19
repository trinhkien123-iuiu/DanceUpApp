using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Debug = UnityEngine.Debug;

namespace DanceUp.AI
{
    /// <summary>
    /// Quản lý tiến trình Python MediaPipe Pose Extractor ngầm trực tiếp từ Unity.
    /// Giúp toàn bộ ứng dụng chạy trực tiếp từ Unity mà không cần mở terminal bên ngoài.
    /// Tự động tắt sạch tiến trình khi dừng Play Mode hoặc thoát ứng dụng.
    /// </summary>
    public class PythonPoseService : MonoBehaviour
    {
        private static PythonPoseService instance;
        public static PythonPoseService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<PythonPoseService>();
                    if (instance == null)
                    {
                        var go = new GameObject("PythonPoseService");
                        instance = go.AddComponent<PythonPoseService>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("=== CẤU HÌNH PYTHON SERVICE ===")]
        [SerializeField] private string pythonExecutable = "python";
        [SerializeField] private string scriptRelativePath = "_Project/Scripts/AI/pose_extractor.py";
        [SerializeField] private int targetUdpPort = 5065;
        [SerializeField] private bool runHeadless = true;
        [SerializeField] private bool streamImage = true;
        [SerializeField] private bool autoStartOnPlay = true;

        private Process pythonProcess = null;
        public bool IsServiceRunning => pythonProcess != null && !pythonProcess.HasExited;

        public event Action OnServiceStarted;
        public event Action OnServiceStopped;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
#endif

            if (autoStartOnPlay)
            {
                StartService(runHeadless);
            }
        }

#if UNITY_EDITOR
        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                StopService();
            }
        }
#endif

        private string ResolvePythonExecutable()
        {
            if (!string.IsNullOrEmpty(pythonExecutable) && File.Exists(pythonExecutable))
                return pythonExecutable;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidatePaths = new string[]
            {
                Path.Combine(localAppData, @"Programs\Python\Python313\python.exe"),
                Path.Combine(localAppData, @"Programs\Python\Python312\python.exe"),
                Path.Combine(localAppData, @"Programs\Python\Python311\python.exe"),
                Path.Combine(localAppData, @"Programs\Python\Python310\python.exe"),
                @"C:\Python313\python.exe",
                @"C:\Python312\python.exe",
                @"C:\Python311\python.exe",
                @"C:\Python310\python.exe"
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path)) return path;
            }

            return "python";
        }

        private void KillStalePoseProcesses()
        {
            try
            {
                // Kiểm tra nhanh: nếu không có bất kỳ tiến trình python nào đang chạy, bỏ qua để không tốn thời gian gọi PowerShell
                var pyProcs = Process.GetProcessesByName("python");
                if (pyProcs == null || pyProcs.Length == 0) return;

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -Command \"Get-CimInstance Win32_Process -Filter \\\"Name = 'python.exe'\\\" | Where-Object { $_.CommandLine -like '*pose_extractor.py*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit(1500);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PythonPoseService] Không thể dọn dẹp tiến trình cũ: " + ex.Message);
            }
        }

        public bool StartService(bool headless = true)
        {
            if (IsServiceRunning)
            {
                Debug.Log($"[PythonPoseService] Dịch vụ Python đang chạy sẵn (PID: {pythonProcess.Id}).");
                return true;
            }

            // Dọn dẹp triệt để bất kỳ tiến trình pose_extractor.py mồ côi nào đang chiếm giữ webcam hoặc port 5065
            KillStalePoseProcesses();

            string scriptPath = Path.Combine(Application.dataPath, scriptRelativePath);
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.Combine(Application.dataPath, "Scripts/AI/pose_extractor.py");
                if (!File.Exists(scriptPath))
                {
                    scriptPath = @"D:\study\NCKH\code\danceEvaluation\pose_extractor.py";
                }
            }

            if (!File.Exists(scriptPath))
            {
                Debug.LogError("[PythonPoseService] Không tìm thấy file script: " + scriptPath);
                return false;
            }

            string exe = ResolvePythonExecutable();
            string workingDir = Path.GetDirectoryName(scriptPath);
            // Thêm -u để output không bị buffer, Unity nhận log thời gian thực ngay lập tức
            string args = $"-u \"{scriptPath}\" --port {targetUdpPort}" +
                          (streamImage ? " --stream-image" : "") +
                          (headless ? " --no-display" : "");

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = headless,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                pythonProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

                pythonProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.Log("[Python-Out] " + e.Data);
                    }
                };

                pythonProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data) &&
                        !e.Data.Contains("oneDNN") &&
                        !e.Data.Contains("INFO:") &&
                        !e.Data.Contains("WARNING:") &&
                        !e.Data.Contains("W0000"))
                    {
                        Debug.LogWarning("[Python-Err] " + e.Data);
                    }
                };

                pythonProcess.Exited += (sender, e) =>
                {
                    Debug.Log("[PythonPoseService] Tiến trình Python đã kết thúc.");
                    OnServiceStopped?.Invoke();
                };

                bool started = pythonProcess.Start();
                if (started)
                {
                    pythonProcess.BeginOutputReadLine();
                    pythonProcess.BeginErrorReadLine();
                    Debug.Log($"[PythonPoseService] Đã khởi chạy Python Pose Extractor trực tiếp từ Unity (PID: {pythonProcess.Id}, Port: {targetUdpPort}, Executable: {exe}).");
                    OnServiceStarted?.Invoke();
                    return true;
                }
                else
                {
                    Debug.LogError("[PythonPoseService] Không thể khởi chạy tiến trình Python.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PythonPoseService] Lỗi khởi động Python: {ex.Message}");
                return false;
            }
        }

        public void StopService()
        {
            if (pythonProcess != null)
            {
                try
                {
                    if (!pythonProcess.HasExited)
                    {
                        pythonProcess.Kill();
                        pythonProcess.WaitForExit(1000);
                        Debug.Log("[PythonPoseService] Đã dừng và giải phóng tiến trình Python thành công.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[PythonPoseService] Ngoại lệ khi dừng tiến trình: " + ex.Message);
                }
                finally
                {
                    try { pythonProcess.Dispose(); } catch { }
                    pythonProcess = null;
                }
            }

            // Dọn dẹp bổ sung để đảm bảo không còn process pose_extractor.py nào chạy ngầm
            KillStalePoseProcesses();
            OnServiceStopped?.Invoke();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
#endif
            StopService();
        }

        private void OnApplicationQuit()
        {
            StopService();
        }

#if UNITY_EDITOR
        [MenuItem("DanceUp/AI/Khởi động Python Pose Service", false, 10)]
        public static void MenuStartService()
        {
            Instance.StartService(false); // Chạy có giao diện để dễ quan sát
        }

        [MenuItem("DanceUp/AI/Dừng Python Pose Service", false, 11)]
        public static void MenuStopService()
        {
            Instance.StopService();
        }
#endif
    }
}
