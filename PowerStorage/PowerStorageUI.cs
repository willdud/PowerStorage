using System.Linq;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace PowerStorage
{ 
    public static class UiHolder
    {
        public static UIComponent TsBar;
        public static float ButtonX;
        public static float ButtonY;
        public static UIView View;
    }

    public class PowerStorageUi : LoadingExtensionBase
	{
        private GameObject _powerStorageUiObj;

        public override void OnLevelLoaded(LoadMode mode)
		{
            PowerStorageLogger.Log("Loading");
            if (_powerStorageUiObj == null)
            {
                if (mode == LoadMode.NewGame || mode == LoadMode.LoadGame)
                {
                    PowerStorageLogger.Log("Add UI");
                    _powerStorageUiObj = new GameObject();
                    _powerStorageUiObj.AddComponent<Ui>();
                }
            }

            var view = UIView.GetAView();
            UiHolder.View = view;
            var c = UIView.Find("TSBar");;
            UiHolder.TsBar = c;
            PowerStorageLogger.Log($"TS: {c?.name}");

            var pos = c.absolutePosition;
            UiHolder.ButtonX = (pos.x + c.width) * view.inputScale - 2;
            UiHolder.ButtonY = (pos.y + 10) * view.inputScale;
        }

		public override void OnLevelUnloading()
        {
            if (_powerStorageUiObj == null) 
                return;

            Object.Destroy(_powerStorageUiObj);
            _powerStorageUiObj = null;
        }
	}

    public class Ui : MonoBehaviour
    {
        private Rect _windowRect = new Rect(Screen.width - 1200, Screen.height - 450, 1200, 300);
        private bool _showingWindow = false;
        private bool _uiSetup = false;
        private UIButton _button;

        private void SetupUi()
        {
            _uiSetup = true;
            _button = UiHolder.TsBar.AddUIComponent<UIButton>();
            _button.text = "PS";
            _button.tooltip = "Power Storage";
            _button.autoSize = true;
            _button.eventClick += ButtonClick;
        }

        private void ButtonClick(UIComponent sender, UIMouseEventParameter e)
        {
            PowerStorageLogger.Log("Button clicked");
            _showingWindow = !_showingWindow;
        }

        void OnGUI()
        {
            if (UiHolder.View.enabled)
            {
                if (!_uiSetup)
                {
                    PowerStorageLogger.Log("Setting up UI");
                    SetupUi();
                }

                if (_showingWindow)
                {
                    _windowRect = GUILayout.Window(314, _windowRect, ShowPowerStorageWindow, "Power Storage - Grid Stats");
                }
            }
        }

        void ShowPowerStorageWindow(int windowId)
        {
            var clone = GridsBuildingsRollup.MasterBuildingsList.ToArray();
            foreach (var c in clone)
            {
                if (c == null || !c.BuildingsList.Any())
                    continue;
                
                GUILayout.BeginHorizontal();
                GUILayout.Label(c.CodeName);
                GUILayout.FlexibleSpace();
                GUI.contentColor = Color.white;
                GUILayout.Label($"Capacity: {c.CapacityKw}KW");
                GUILayout.Label($"Consumption: {c.ConsumptionKw}KW");
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Capacity Last Week: {c.LastCycleTotalCapacityKw}KW");
                GUILayout.Label($"Consumption Last Week: {c.LastCycleTotalConsumptionKw}KW");
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Buildings: {c.BuildingsList.Count}");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label($"-- AI: {string.Join(", ", c.AiTypes.ToArray())}");
                GUILayout.EndHorizontal();
            }
            
            GUILayout.BeginHorizontal();            
            if (GUILayout.Button("Close"))
            {
                _button.state = UIButton.ButtonState.Normal;
                _showingWindow = false;
            }
            GUILayout.EndHorizontal();
            
            GUI.DragWindow();
        }
    }
}
