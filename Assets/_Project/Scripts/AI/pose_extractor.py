"""
Python MediaPipe Pose Extractor for Unity Dance Movement Evaluation
-------------------------------------------------------------------
Captures video frames from webcam, detects human pose landmarks with MediaPipe,
normalizes and maps the 13 required joints to Unity coordinate space,
and streams JSON packets via UDP to Unity (127.0.0.1:5065).

Supported Joints (processing_11 specification):
0: nose, 11: left_shoulder, 12: right_shoulder, 13: left_elbow, 14: right_elbow,
15: left_wrist, 16: right_wrist, 23: left_hip, 24: right_hip,
25: left_knee, 26: right_knee, 27: left_ankle, 28: right_ankle.
"""

import os
import sys
import time
import json
import socket
import signal
import argparse
import urllib.request
import base64
import cv2
import numpy as np

# Force unbuffered standard output for real-time Unity Editor log capturing
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(line_buffering=True)
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(line_buffering=True)

# 13 Landmark Mapping
LANDMARKS_MAP = {
    "nose": 0,
    "left_shoulder": 11,
    "right_shoulder": 12,
    "left_elbow": 13,
    "right_elbow": 14,
    "left_wrist": 15,
    "right_wrist": 16,
    "left_hip": 23,
    "right_hip": 24,
    "left_knee": 25,
    "right_knee": 26,
    "left_ankle": 27,
    "right_ankle": 28
}

SKELETON_BONES = [
    ("nose", "left_shoulder"),
    ("nose", "right_shoulder"),
    ("left_shoulder", "right_shoulder"),
    ("left_shoulder", "left_elbow"),
    ("left_elbow", "left_wrist"),
    ("right_shoulder", "right_elbow"),
    ("right_elbow", "right_wrist"),
    ("left_shoulder", "left_hip"),
    ("right_shoulder", "right_hip"),
    ("left_hip", "right_hip"),
    ("left_hip", "left_knee"),
    ("left_knee", "left_ankle"),
    ("right_hip", "right_knee"),
    ("right_knee", "right_ankle")
]

MODEL_DOWNLOAD_URL = "https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/latest/pose_landmarker_lite.task"


class PoseDetector:
    """Wrapper that supports both modern MediaPipe Tasks API and legacy solutions.pose API."""

    def __init__(self, model_path="pose_landmarker_lite.task"):
        self.use_tasks_api = False
        self.detector = None

        import mediapipe as mp

        # Try Tasks API first
        if hasattr(mp, "tasks") and hasattr(mp.tasks, "vision"):
            try:
                from mediapipe.tasks import python as mp_python
                from mediapipe.tasks.python import vision as mp_vision

                # Ensure model file exists
                if not os.path.exists(model_path):
                    alt_path = os.path.join(os.path.dirname(__file__), model_path)
                    if os.path.exists(alt_path):
                        model_path = alt_path
                    elif os.path.exists("D:/tmp/pose_landmarker_lite.task"):
                        model_path = "D:/tmp/pose_landmarker_lite.task"
                    else:
                        print(f"[PoseExtractor] Model '{model_path}' not found. Downloading...")
                        urllib.request.urlretrieve(MODEL_DOWNLOAD_URL, model_path)
                        print(f"[PoseExtractor] Download complete: {model_path}")

                base_options = mp_python.BaseOptions(model_asset_path=model_path)
                options = mp_vision.PoseLandmarkerOptions(
                    base_options=base_options,
                    running_mode=mp_vision.RunningMode.IMAGE,
                    num_poses=1,
                    min_pose_detection_confidence=0.5,
                    min_pose_presence_confidence=0.5,
                    min_tracking_confidence=0.5,
                    output_segmentation_masks=False
                )
                self.detector = mp_vision.PoseLandmarker.create_from_options(options)
                self.use_tasks_api = True
                self.mp = mp
                print("[PoseExtractor] Initialized MediaPipe Tasks PoseLandmarker successfully.")
                return
            except Exception as e:
                print(f"[PoseExtractor] Tasks API failed ({e}), falling back to solutions.pose...")

        # Fallback to legacy solutions API
        if hasattr(mp, "solutions") and hasattr(mp.solutions, "pose"):
            self.detector = mp.solutions.pose.Pose(
                min_detection_confidence=0.5,
                min_tracking_confidence=0.5
            )
            self.use_tasks_api = False
            self.mp = mp
            print("[PoseExtractor] Initialized legacy MediaPipe solutions.pose successfully.")
        else:
            raise RuntimeError("Could not initialize MediaPipe Pose detector with either API.")

    def detect(self, rgb_frame):
        """Returns list of normalized landmarks [{x, y, z, visibility}] or None."""
        if self.use_tasks_api:
            mp_image = self.mp.Image(image_format=self.mp.ImageFormat.SRGB, data=rgb_frame)
            result = self.detector.detect(mp_image)
            if result.pose_landmarks and len(result.pose_landmarks) > 0:
                return result.pose_landmarks[0]
            return None
        else:
            results = self.detector.process(rgb_frame)
            if results.pose_landmarks:
                return results.pose_landmarks.landmark
            return None


def run_analyze_video(args, detector):
    """Processes an offline video file frame-by-frame and outputs MediaPipe pose keypoints to JSON."""
    if not args.video or not os.path.exists(args.video):
        print(f"[Error] Video file not found: {args.video}", flush=True)
        sys.exit(1)

    out_json = args.output_json or os.path.splitext(args.video)[0] + "_pose.json"
    print(f"[PoseExtractor] Analyzing video: {args.video}", flush=True)
    print(f"[PoseExtractor] Output JSON will be saved to: {out_json}", flush=True)

    cap = cv2.VideoCapture(args.video)
    if not cap.isOpened():
        print(f"[Error] Could not open video file: {args.video}", flush=True)
        sys.exit(1)

    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    fps = cap.get(cv2.CAP_PROP_FPS)
    if fps <= 0 or np.isnan(fps):
        fps = 30.0

    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

    print(f"[PoseExtractor] Video Info: {width}x{height} | {fps:.1f} FPS | Total frames: {total_frames}", flush=True)

    analyzed_frames = []
    frame_idx = 0
    start_time = time.time()

    while True:
        ret, frame = cap.read()
        if not ret or frame is None:
            break

        frame_idx += 1
        t_sec = round((frame_idx - 1) / fps, 4)

        rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        landmarks = detector.detect(rgb_frame)

        student_joints = {}
        if landmarks:
            for joint_name, idx in LANDMARKS_MAP.items():
                if idx < len(landmarks):
                    lm = landmarks[idx]
                    unity_x = float(np.clip(lm.x, 0.0, 1.0))
                    unity_y = float(np.clip(1.0 - lm.y, 0.0, 1.0))
                    vis = float(getattr(lm, 'visibility', 1.0))

                    student_joints[joint_name] = {
                        "x": round(unity_x, 4),
                        "y": round(unity_y, 4),
                        "visibility": round(vis, 3),
                        "is_pass": 1
                    }

        frame_data = {
            "frame_index": frame_idx - 1,
            "timestamp": t_sec,
            "pose_detected": bool(landmarks is not None and len(student_joints) >= 6),
            "student_joints": student_joints
        }
        analyzed_frames.append(frame_data)

        # Print progress for Unity tracking
        if frame_idx % 10 == 0 or frame_idx == total_frames:
            pct = (frame_idx / max(1, total_frames)) * 100.0
            print(f"PROGRESS:{pct:.1f} ({frame_idx}/{total_frames})", flush=True)

    cap.release()
    elapsed = time.time() - start_time

    output_data = {
        "video_path": os.path.abspath(args.video),
        "total_frames": len(analyzed_frames),
        "fps": fps,
        "duration": round(len(analyzed_frames) / fps, 2),
        "width": width,
        "height": height,
        "processed_time_seconds": round(elapsed, 2),
        "frames": analyzed_frames
    }

    os.makedirs(os.path.dirname(os.path.abspath(out_json)), exist_ok=True)
    with open(out_json, "w", encoding="utf-8") as f:
        json.dump(output_data, f, ensure_ascii=False, indent=2)

    print(f"ANALYSIS_COMPLETE:{out_json}", flush=True)
    print(f"[PoseExtractor] Successfully analyzed {len(analyzed_frames)} frames in {elapsed:.1f}s.", flush=True)


def run_export_video(args):
    """Renders the analyzed video with 2D skeleton overlay, color-coded joints, and score HUD."""
    if not args.video or not os.path.exists(args.video):
        print(f"[Error] Input video file not found: {args.video}", flush=True)
        sys.exit(1)

    analysis_file = args.analysis
    if not analysis_file or not os.path.exists(analysis_file):
        # Look for default analysis file
        candidate = os.path.splitext(args.video)[0] + "_pose.json"
        if os.path.exists(candidate):
            analysis_file = candidate
        else:
            print(f"[Error] Analysis JSON not found: {analysis_file}", flush=True)
            sys.exit(1)

    out_video = args.output_video
    if not out_video:
        out_video = os.path.splitext(args.video)[0] + "_skeleton_export.mp4"

    print(f"[PoseExtractor] Exporting video with skeleton overlay...", flush=True)
    print(f"  Input:    {args.video}", flush=True)
    print(f"  Analysis: {analysis_file}", flush=True)
    print(f"  Output:   {out_video}", flush=True)

    with open(analysis_file, "r", encoding="utf-8") as f:
        analysis_data = json.load(f)

    frame_map = {}
    frames_list = analysis_data.get("frames") or analysis_data.get("frameResults") or []
    for f in frames_list:
        f_idx = f.get("frame_index", f.get("frameIndex", -1))
        if f_idx >= 0:
            frame_map[f_idx] = f

    overall_acc = analysis_data.get("overallAccuracy", 84.0)
    is_passed = analysis_data.get("isPassedStandard", overall_acc >= 70.0)

    cap = cv2.VideoCapture(args.video)
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))

    os.makedirs(os.path.dirname(os.path.abspath(out_video)), exist_ok=True)

    fourcc = cv2.VideoWriter_fourcc(*'mp4v')
    out = cv2.VideoWriter(out_video, fourcc, fps, (width, height))

    frame_idx = 0
    color_pass = (76, 230, 16)      # Neon Green BGR
    color_error = (102, 51, 255)    # Neon Red BGR
    color_cyan = (255, 180, 0)      # DanceUp Cyan BGR

    while True:
        ret, frame = cap.read()
        if not ret or frame is None:
            break

        f_data = frame_map.get(frame_idx)
        student_joints = {}
        joints_eval = {}
        failed_joint_names = []
        frame_score = 0.0
        frame_pass = True

        if f_data:
            frame_score = float(f_data.get("frame_score", f_data.get("frameScore", 0.0)))
            frame_pass = bool(f_data.get("is_pass", f_data.get("isPass", frame_score >= 8.1)))
            sj = f_data.get("student_joints") or f_data.get("joints") or {}
            for j_name, j_val in sj.items():
                if isinstance(j_val, dict):
                    student_joints[j_name] = j_val
                    # Check pass/fail status
                    is_pass = int(j_val.get("is_pass", j_val.get("isPass", 1)))
                    joints_eval[j_name] = is_pass
                    if is_pass == 0:
                        failed_joint_names.append(j_name)

        # 1. Draw Skeleton Bones
        for j1, j2 in SKELETON_BONES:
            if j1 in student_joints and j2 in student_joints:
                p1_data = student_joints[j1]
                p2_data = student_joints[j2]

                p1_x = p1_data.get("x", p1_data.get("studentPos", {}).get("x", 0))
                p1_y = p1_data.get("y", p1_data.get("studentPos", {}).get("y", 0))
                p2_x = p2_data.get("x", p2_data.get("studentPos", {}).get("x", 0))
                p2_y = p2_data.get("y", p2_data.get("studentPos", {}).get("y", 0))

                # Convert normalized Unity (0 at bottom) to OpenCV pixel (0 at top)
                pt1 = (int(p1_x * width), int((1.0 - p1_y) * height))
                pt2 = (int(p2_x * width), int((1.0 - p2_y) * height))

                both_pass = (joints_eval.get(j1, 1) == 1 and joints_eval.get(j2, 1) == 1)
                bone_color = color_pass if both_pass else color_error
                thickness = 4 if both_pass else 5

                cv2.line(frame, pt1, pt2, bone_color, thickness, cv2.LINE_AA)

        # 2. Draw Skeleton Joints
        for j_name, j_val in student_joints.items():
            jx = j_val.get("x", j_val.get("studentPos", {}).get("x", 0))
            jy = j_val.get("y", j_val.get("studentPos", {}).get("y", 0))
            pt = (int(jx * width), int((1.0 - jy) * height))

            is_pass = joints_eval.get(j_name, 1)
            j_color = color_pass if is_pass == 1 else color_error
            radius = 7 if is_pass == 1 else 10

            cv2.circle(frame, pt, radius, j_color, -1, cv2.LINE_AA)
            cv2.circle(frame, pt, radius + 2, (255, 255, 255), 1, cv2.LINE_AA)

        # 3. Draw Sleek Top HUD Banner
        banner_h = 96 if failed_joint_names else 76
        hud_overlay = frame.copy()
        cv2.rectangle(hud_overlay, (0, 0), (width, banner_h), (15, 20, 35), -1)
        cv2.addWeighted(hud_overlay, 0.78, frame, 0.22, 0, frame)

        t_sec = frame_idx / fps
        time_str = f"{int(t_sec // 60):02d}:{int(t_sec % 60):02d}.{int((t_sec % 1) * 10)}"

        # HUD Texts
        verdict_txt = "DAT CHUAN (PASS)" if is_passed else "CHUA DAT (FAIL)"
        verdict_col = color_pass if is_passed else color_error
        cv2.putText(frame, "DanceUp AI Coach", (16, 28),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.7, color_cyan, 2, cv2.LINE_AA)
        cv2.putText(frame, f"Time: {time_str} | Score: {frame_score:.0f}/13", (16, 54),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, color_pass if frame_pass else color_error, 1, cv2.LINE_AA)

        # Accuracy & Verdict Badge (Right aligned cleanly inside width)
        badge_text = f"{overall_acc:.0f}% - {verdict_txt}"
        text_size, _ = cv2.getTextSize(badge_text, cv2.FONT_HERSHEY_SIMPLEX, 0.58, 2)
        badge_x = max(16, width - text_size[0] - 16)
        cv2.putText(frame, badge_text, (badge_x, 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.58, verdict_col, 2, cv2.LINE_AA)

        if failed_joint_names:
            err_preview = ", ".join(failed_joint_names[:3])
            if len(failed_joint_names) > 3:
                err_preview += f" (+{len(failed_joint_names) - 3})"
            cv2.putText(frame, f"Loi khop: {err_preview}", (16, 80),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.48, color_error, 1, cv2.LINE_AA)

        out.write(frame)
        frame_idx += 1

        if frame_idx % 20 == 0 or frame_idx == total_frames:
            pct = (frame_idx / max(1, total_frames)) * 100.0
            print(f"EXPORT_PROGRESS:{pct:.1f} ({frame_idx}/{total_frames})", flush=True)

    cap.release()
    out.release()

    print(f"EXPORT_COMPLETE:{out_video}", flush=True)
    print(f"[PoseExtractor] Exported {frame_idx} frames with skeleton overlay to: {out_video}", flush=True)


def main():
    parser = argparse.ArgumentParser(description="MediaPipe Pose Extractor and Dance Video Analyzer")
    parser.add_argument("--mode", default="stream", choices=["stream", "analyze_video", "export_video"],
                        help="Mode: stream (Webcam UDP), analyze_video (Offline video analysis), export_video (Render skeleton on video)")
    parser.add_argument("--video", default="", help="Input video file path for analyze_video or export_video")
    parser.add_argument("--output-json", default="", help="Path for saving analysis JSON")
    parser.add_argument("--analysis", default="", help="Path to analysis JSON for export_video")
    parser.add_argument("--output-video", default="", help="Path for saving exported MP4 video")
    parser.add_argument("--ip", default="127.0.0.1", help="Target UDP IP (default: 127.0.0.1)")
    parser.add_argument("--port", type=int, default=5065, help="Target UDP Port (default: 5065)")
    parser.add_argument("--cam", type=int, default=0, help="Webcam device index (default: 0)")
    parser.add_argument("--width", type=int, default=640, help="Camera width (default: 640)")
    parser.add_argument("--height", type=int, default=480, help="Camera height (default: 480)")
    parser.add_argument("--no-mirror", action="store_true", help="Disable horizontal mirroring")
    parser.add_argument("--no-display", action="store_true", help="Headless mode without OpenCV window")
    parser.add_argument("--stream-image", action="store_true", default=True, help="Stream compressed JPEG camera frame to Unity")
    args = parser.parse_args()

    # Route based on mode
    if args.mode == "analyze_video":
        detector = PoseDetector()
        run_analyze_video(args, detector)
        return

    if args.mode == "export_video":
        run_export_video(args)
        return

    # Default Mode: Live Stream (Option B)
    mirror = not args.no_mirror

    print("=" * 60, flush=True)
    print(" DANCE MOVEMENT EVALUATION - PYTHON POSE EXTRACTOR", flush=True)
    print(f" Target Endpoint: {args.ip}:{args.port}", flush=True)
    print(f" Camera Index:    {args.cam} ({args.width}x{args.height})", flush=True)
    print(f" Mirror Mode:     {'Enabled' if mirror else 'Disabled'}", flush=True)
    print(f" Stream Image:    {'Enabled' if args.stream_image else 'Disabled'}", flush=True)
    print("=" * 60, flush=True)

    # Initialize UDP socket
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    # Initialize Pose Detector
    detector = PoseDetector()

    # Open Camera with DirectShow on Windows for instant startup & low latency
    # Includes robust retry loop so consecutive attempts succeed even if Windows kernel is releasing camera driver
    cap = None
    max_camera_retries = 10
    for attempt in range(max_camera_retries):
        if sys.platform.startswith("win"):
            try:
                cap = cv2.VideoCapture(args.cam, cv2.CAP_DSHOW)
            except Exception:
                cap = None

        if cap is None or not cap.isOpened():
            cap = cv2.VideoCapture(args.cam)

        if cap is not None and cap.isOpened():
            print(f"[PoseExtractor] Webcam device {args.cam} opened successfully (attempt {attempt + 1}).", flush=True)
            break

        print(f"[PoseExtractor] Webcam device {args.cam} not yet ready (attempt {attempt + 1}/{max_camera_retries}). Retrying in 0.5s...", flush=True)
        time.sleep(0.5)

    if cap is None or not cap.isOpened():
        print(f"[Error] No webcam could be opened at index {args.cam} after {max_camera_retries} attempts! Retrying index 1...", flush=True)
        cap = cv2.VideoCapture(1)

    if not cap.isOpened():
        print(f"[Error] No webcam could be opened! Please ensure camera is connected and not locked by another app.", flush=True)
        sys.exit(1)

    # Register graceful exit handlers to cleanly release camera on shutdown
    def _graceful_exit(signum, frame):
        print(f"[PoseExtractor] Received signal {signum}. Cleanly releasing camera...", flush=True)
        if cap is not None and cap.isOpened():
            cap.release()
        cv2.destroyAllWindows()
        sys.exit(0)

    try:
        signal.signal(signal.SIGINT, _graceful_exit)
        signal.signal(signal.SIGTERM, _graceful_exit)
    except Exception:
        pass

    cap.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)

    frame_index = 0
    start_time = time.time()
    last_fps_time = time.time()
    fps = 0.0
    packets_sent = 0

    print("[PoseExtractor] Streaming started. Press 'q' to exit, 'm' to toggle mirror.", flush=True)

    try:
        while True:
            ret, frame = cap.read()
            if not ret or frame is None:
                time.sleep(0.01)
                continue

            frame_index += 1
            current_time = time.time()
            elapsed = current_time - start_time

            # Calculate FPS
            if current_time - last_fps_time >= 0.5:
                fps = 1.0 / max(1e-4, (current_time - last_fps_time))
                last_fps_time = current_time

            if mirror:
                frame = cv2.flip(frame, 1)

            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            landmarks = detector.detect(rgb_frame)

            student_joints = {}
            pixel_joints = {}

            if landmarks:
                h, w, _ = frame.shape
                for joint_name, idx in LANDMARKS_MAP.items():
                    if idx < len(landmarks):
                        lm = landmarks[idx]
                        raw_x = lm.x
                        raw_y = lm.y

                        # In Unity, y=0 is at bottom, y=1 is at top.
                        # MediaPipe has y=0 at top, y=1 at bottom.
                        unity_y = float(np.clip(1.0 - raw_y, 0.0, 1.0))
                        unity_x = float(np.clip(raw_x, 0.0, 1.0))

                        # Save for JSON packet
                        student_joints[joint_name] = {
                            "x": round(unity_x, 4),
                            "y": round(unity_y, 4),
                            "is_pass": 1,
                            "joint_error": 0.0
                        }

                        # Save for preview drawing
                        px = int(raw_x * w)
                        py = int(raw_y * h)
                        pixel_joints[joint_name] = (px, py)

                packet = {
                    "frame_index": frame_index,
                    "timestamp": round(elapsed, 3),
                    "pose_detected": True,
                    "student_joints": student_joints
                }
            else:
                packet = {
                    "frame_index": frame_index,
                    "timestamp": round(elapsed, 3),
                    "pose_detected": False,
                    "student_joints": {}
                }

            # Optional: compress small JPEG for Unity Background_Video display
            if args.stream_image and frame is not None:
                try:
                    h_f, w_f = frame.shape[:2]
                    target_w = 480
                    target_h = int(target_w * (h_f / max(1, w_f)))
                    small = cv2.resize(frame, (target_w, target_h))
                    _, buf = cv2.imencode('.jpg', small, [int(cv2.IMWRITE_JPEG_QUALITY), 55])
                    packet["image"] = base64.b64encode(buf).decode('ascii')
                except Exception:
                    pass

            # Send UDP Packet
            json_str = json.dumps(packet)
            sock.sendto(json_str.encode("utf-8"), (args.ip, args.port))
            packets_sent += 1

            # OpenCV Preview Window
            if not args.no_display:
                # Draw skeleton bones
                for j1, j2 in SKELETON_BONES:
                    if j1 in pixel_joints and j2 in pixel_joints:
                        cv2.line(frame, pixel_joints[j1], pixel_joints[j2], (0, 255, 128), 2)

                # Draw joint points
                for j_name, pt in pixel_joints.items():
                    cv2.circle(frame, pt, 5, (0, 200, 255), -1)
                    cv2.circle(frame, pt, 6, (255, 255, 255), 1)

                # Overlay status text
                status_color = (0, 255, 0) if landmarks else (0, 255, 255)
                status_text = "TRACKING" if landmarks else "SEARCHING FOR PERSON"
                cv2.putText(frame, f"STATUS: {status_text}", (15, 30),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.7, status_color, 2)
                cv2.putText(frame, f"UDP: {args.ip}:{args.port} | Sent: {packets_sent}", (15, 60),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1)
                cv2.putText(frame, f"FPS: {fps:.1f} | Mirror: {'ON' if mirror else 'OFF'}", (15, 85),
                            cv2.FONT_HERSHEY_SIMPLEX, 0.55, (200, 200, 200), 1)

                cv2.imshow("DanceUp - Pose Extractor (Option B)", frame)
                key = cv2.waitKey(1) & 0xFF
                if key == ord('q') or key == 27:
                    print("\n[PoseExtractor] Exit requested by user.")
                    break
                elif key == ord('m'):
                    mirror = not mirror
                    print(f"[PoseExtractor] Mirror toggled: {mirror}")

    except KeyboardInterrupt:
        print("\n[PoseExtractor] Stopped by Ctrl+C.")
    finally:
        cap.release()
        cv2.destroyAllWindows()
        sock.close()
        print(f"[PoseExtractor] Closed cleanly. Total frames processed: {frame_index}, packets sent: {packets_sent}.")


if __name__ == "__main__":
    main()
