using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanceUp.Data
{
    [Serializable]
    public class JointScoreItem
    {
        public string jointName;
        public Vector2 studentPos;
        public Vector2 teacherPos;
        public int isPass; // 1 = pass, 0 = fail
        public float jointError;
        public float jointCosine;
        public float velocityError;
    }

    [Serializable]
    public class FrameEvaluationResult
    {
        public int frameIndex;
        public int teacherFrameIndex;
        public float timestamp;
        public float frameScore; // Sum of joint points (0 - 13)
        public float maxScore = 13.0f;
        public bool isPass; // frameScore >= 7.1
        public float cumulativeAccuracy; // %
        public Dictionary<string, JointScoreItem> joints = new Dictionary<string, JointScoreItem>();
    }

    [Serializable]
    public class IncorrectSectionInfo
    {
        public float startTime;
        public float endTime;
        public string primaryJointError;
        public string description;
        public float avgScore;
    }

    [Serializable]
    public class DtwDeviationSegment
    {
        public float startTime;
        public float endTime;
        public string deviationType; // "Sớm nhịp", "Trễ nhịp", "Lệch tư thế", "Khớp nhịp"
        public float timeOffsetSeconds; // > 0: trễ nhịp, < 0: sớm nhịp
        public float spatialDistance;
        public string primaryJoint;
        public string description;
        public int severity; // 0 = Good (Xanh), 1 = Minor drift (Vàng), 2 = Severe mismatch (Đỏ)
    }

    [Serializable]
    public class SessionEvaluationData
    {
        public string lessonTitle = "HIP HOP BASIC • ARM GROOVE";
        public float sessionDuration = 30f; // seconds
        public float overallAccuracy = 87f; // % (Điểm tổng hợp)
        public float dtwAccuracy = 87f; // % (Điểm tương đồng quỹ đạo qua DTW)
        public float frameByFrameAccuracy = 87f; // % (Điểm frame-by-frame trực tiếp)
        public int totalMovementsEvaluated = 12; // Evaluated movements / segments
        public int xpEarned = 15;
        public string coachAdvice = "Bạn đang tiến bộ rất tốt! 🎉";

        // Accuracy curve over time (timestamp, accuracy%)
        public List<Vector2> accuracyOverTime = new List<Vector2>();

        // Joint error breakdown (jointName -> error count)
        public Dictionary<string, int> jointErrorCounts = new Dictionary<string, int>();

        // Sorted top error joints
        public List<KeyValuePair<string, int>> topErrorJoints = new List<KeyValuePair<string, int>>();

        // Incorrect temporal sections
        public List<IncorrectSectionInfo> incorrectSections = new List<IncorrectSectionInfo>();

        // DTW Temporal & Movement Deviations
        public List<DtwDeviationSegment> dtwDeviations = new List<DtwDeviationSegment>();
        public float dtwTimeDriftSeconds = 0f;
        public string analyzedVideoPath = "";
        public string referenceVideoPath = "";
        public bool isImportedVideo = false;
        public bool isPassedStandard = false;
        public float passStandardThreshold = 70f;

        // List of all frame results
        public List<FrameEvaluationResult> frameResults = new List<FrameEvaluationResult>();
        public List<FrameEvaluationResult> frames { get => frameResults; set => frameResults = value; }

        // Session status & warnings (Option A)
        public bool isSessionTooShort = false;
        public string warningMessage = "";

        // User profile & personalized progression (PlayerPrefs)
        public bool isPersonalRecord = false;
        public float deltaScore = 6f;
        public int streakDays = 8;
        public int currentLevel = 4;
        public int totalXP = 520;

        // 7 Skill Breakdown (0 - 100)
        public Dictionary<string, float> skillScores = new Dictionary<string, float>();
        public Dictionary<string, float> skillDeltas = new Dictionary<string, float>();
    }
}
