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

        private void Awake()
        {
            navigation.TabSelected += Show;
        }

        private void OnDestroy()
        {
            if (navigation != null)
                navigation.TabSelected -= Show;
        }

        private void Start()
        {
            Show(startTab);
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
