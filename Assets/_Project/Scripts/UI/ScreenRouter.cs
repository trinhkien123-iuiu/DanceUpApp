using System;
using UnityEngine;

namespace DanceUp.UI
{
    /// <summary>Bật một trang theo tab và đồng bộ trạng thái thanh điều hướng.</summary>
    public class ScreenRouter : MonoBehaviour
    {
        [Serializable]
        private class TabScreen
        {
            public AppTab tab;
            public GameObject screen;
        }

        [SerializeField] private BottomNavigation navigation;
        [SerializeField] private TabScreen[] screens;
        [SerializeField] private AppTab startTab = AppTab.Home;

        [SerializeField] private GameObject practiceScreen;
        [SerializeField] private GameObject sessionEvaluationScreen;

        private void Awake()
        {
            if (navigation == null)
            {
                navigation = FindObjectOfType<BottomNavigation>(true);
            }

            if (screens == null || screens.Length == 0)
            {
                AutoDiscoverScreens();
            }

            if (navigation != null)
            {
                navigation.TabSelected += Show;
                navigation.CameraRequested += OnCameraRequested;
            }
        }

        private void AutoDiscoverScreens()
        {
            var discovered = new System.Collections.Generic.List<TabScreen>();
            var all = FindObjectsOfType<Transform>(true);
            foreach (var t in all)
            {
                if (t.name.Equals("HomeScreen", StringComparison.OrdinalIgnoreCase))
                    discovered.Add(new TabScreen { tab = AppTab.Home, screen = t.gameObject });
                else if (t.name.Equals("LessonsScreen", StringComparison.OrdinalIgnoreCase))
                    discovered.Add(new TabScreen { tab = AppTab.Lessons, screen = t.gameObject });
                else if (t.name.Equals("ProgressScreen", StringComparison.OrdinalIgnoreCase))
                    discovered.Add(new TabScreen { tab = AppTab.Progress, screen = t.gameObject });
                else if (t.name.Equals("ProfileScreen", StringComparison.OrdinalIgnoreCase))
                    discovered.Add(new TabScreen { tab = AppTab.Profile, screen = t.gameObject });
            }
            if (discovered.Count > 0)
            {
                screens = discovered.ToArray();
            }
        }

        private void OnDestroy()
        {
            if (navigation != null)
            {
                navigation.TabSelected -= Show;
                navigation.CameraRequested -= OnCameraRequested;
            }
        }

        private void OnCameraRequested()
        {
            if (practiceScreen != null)
            {
                practiceScreen.SetActive(true);
                var controller = practiceScreen.GetComponent<DanceUp.Features.Practice.PracticeScreenController>();
                if (controller != null)
                {
                    controller.OpenPracticeScreen();
                    return;
                }
            }

            // Fallback: tìm PracticeScreenController trong scene ngay cả khi GameObject đang bị tắt
            var found = FindObjectsOfType<DanceUp.Features.Practice.PracticeScreenController>(true);
            if (found != null && found.Length > 0)
            {
                found[0].OpenPracticeScreen();
            }
        }

        private void Start()
        {
            EnsureLessonScreenController();
            Show(startTab);
        }

        private void EnsureLessonScreenController()
        {
            var lessons = FindObjectOfType<DanceUp.Features.Classroom.LessonScreenController>(true);
            if (lessons == null && screens != null)
            {
                foreach (var item in screens)
                {
                    if (item.tab == AppTab.Lessons && item.screen != null)
                    {
                        if (item.screen.GetComponent<DanceUp.Features.Classroom.LessonScreenController>() == null)
                        {
                            item.screen.AddComponent<DanceUp.Features.Classroom.LessonScreenController>();
                        }
                        break;
                    }
                }
            }
        }

        public void Show(AppTab tab)
        {
            if (!HasScreen(tab))
                return;

            foreach (var item in screens)
            {
                if (item.screen != null)
                    item.screen.SetActive(item.tab == tab);
            }

            navigation.SetActiveTab(tab);
        }

        private bool HasScreen(AppTab tab)
        {
            foreach (var item in screens)
            {
                if (item.tab == tab && item.screen != null)
                    return true;
            }

            return false;
        }
    }
}
