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


def main():
    parser = argparse.ArgumentParser(description="MediaPipe Pose Extractor to Unity UDP")
    parser.add_argument("--ip", default="127.0.0.1", help="Target UDP IP (default: 127.0.0.1)")
    parser.add_argument("--port", type=int, default=5065, help="Target UDP Port (default: 5065)")
    parser.add_argument("--cam", type=int, default=0, help="Webcam device index (default: 0)")
    parser.add_argument("--width", type=int, default=640, help="Camera width (default: 640)")
    parser.add_argument("--height", type=int, default=480, help="Camera height (default: 480)")
    parser.add_argument("--no-mirror", action="store_true", help="Disable horizontal mirroring")
    parser.add_argument("--no-display", action="store_true", help="Headless mode without OpenCV window")
    parser.add_argument("--stream-image", action="store_true", default=True, help="Stream compressed JPEG camera frame to Unity")
    args = parser.parse_args()

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
