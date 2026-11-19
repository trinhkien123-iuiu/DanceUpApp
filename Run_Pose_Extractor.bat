@echo off
title DanceUp - Pose Extractor
cd /d "%~dp0"
echo ========================================================
echo   DanceUp App - MediaPipe Pose Extractor (Option B)
echo   Streaming 13 Landmarks via UDP to Unity (127.0.0.1:5065)
echo ========================================================
echo.
python Assets/_Project/Scripts/AI/pose_extractor.py
pause
