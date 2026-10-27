using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DanceUp.UI
{
    public enum AppTab
    {
        Home,
        Lessons,
        Progress,
        Profile
    }

    /// <summary>Thanh điều hướng dưới: báo tab được bấm và tô sáng tab đang chọn.</summary>
    public class BottomNavigation : MonoBehaviour
    {
        [Serializable]
        private class TabItem
        {
            public AppTab tab;
            public Button button;
            public Image icon;
            public TMP_Text label;
            public GameObject activeDot;
            public Sprite activeIcon;
            public Sprite inactiveIcon;
        }

        [SerializeField] private TabItem[] tabs;
        [SerializeField] private Button cameraButton;
        [SerializeField] private Color activeColor = new Color(0f, 0.706f, 1f);
        [SerializeField] private Color inactiveColor = new Color(0.659f, 0.698f, 0.737f);

        public event Action<AppTab> TabSelected;
        public event Action CameraRequested;

        private void Awake()
        {
            foreach (var item in tabs)
            {
                var captured = item.tab;
                item.button.onClick.AddListener(() => TabSelected?.Invoke(captured));
            }

            if (cameraButton != null)
                cameraButton.onClick.AddListener(() => CameraRequested?.Invoke());
        }

        public void SetActiveTab(AppTab active)
        {
            foreach (var item in tabs)
            {
                var isActive = item.tab == active;
                var sprite = isActive && item.activeIcon != null ? item.activeIcon : item.inactiveIcon;

                if (sprite != null)
                    item.icon.sprite = sprite;

                item.label.color = isActive ? activeColor : inactiveColor;

                if (item.activeDot != null)
                    item.activeDot.SetActive(isActive);
            }
        }
    }
}
