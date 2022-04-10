//using ColossalFramework.UI;
//using ICities;
//using UnityEngine;
//using Object = UnityEngine.Object;

//namespace PowerStorage
//{ 
//    public static class UiHolder
//    {
//        public static UIComponent MainPanel;
//        public static Building? SelectedBuilding;
//        public static UIView View;
//    }

//    public class PowerStorageUiLoading : LoadingExtensionBase
//	{
//        private GameObject _powerStorageUiObj;

//        public override void OnLevelLoaded(LoadMode mode)
//		{
//            PowerStorageLogger.Log("Loading", PowerStorageMessageType.Loading);
//            if (_powerStorageUiObj != null) 
//                return;

//            if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame) 
//                return;
            
//            var view = UIView.GetAView();
//            UiHolder.View = view;

//            PowerStorageLogger.Log("Add UI", PowerStorageMessageType.Ui);
//            _powerStorageUiObj = new GameObject();
//            _powerStorageUiObj.AddComponent<PowerStorageUi>();
//        }

//		public override void OnLevelUnloading()
//        {
//            PowerStorageLogger.Log("OnLevelUnloading", PowerStorageMessageType.Loading);
//            if (_powerStorageUiObj == null) 
//                return;

//            Object.Destroy(_powerStorageUiObj);
//            _powerStorageUiObj = null;
//        }
//	}
    
//    public class PowerStorageUi : MonoBehaviour
//    {
//        private static readonly int Width = 800;
//        private static readonly int Height = 500;

//        private Rect _windowRect = new Rect(0, 0, Width, Height);

//        public const string PanelName = "PowerStoragePanel";
//        private bool _uiSetup;


//        public PowerStorageUi()
//        {
//            name = "PowerStorageUiObj";
//        }

//        void Start()
//        {

//        }

//        void Update()
//        {
//            AttachToExistingUi();
//        }
        
//        private void AttachToExistingUi()
//        {
//            if (_uiSetup)
//                return;
            
//            var c = UIView.Find("MainTop");
//            //PowerStorageLogger.Log($"View: {c?.name} - {c.isEnabled} - {c.isVisible}", PowerStorageMessageType.Ui);
//            if (c == null || !c.isEnabled || !c.isVisible)
//                return;

//            _uiSetup = true;
//            UiHolder.MainPanel = c;
//            PowerStorageLogger.Log($"Panel: {c.name}", PowerStorageMessageType.Ui);

//            //var pos = UiHolder.MainPanel.absolutePosition + new Vector3(UiHolder.MainPanel.size.x + 5, 0, 0);
//            //_button = UiHolder.MainPanel.AddUIComponent<UIButton>();
//            //_button.text = "Power Storage";
//            //_button.normalBgSprite = "ButtonMenu";
//            //_button.normalFgSprite = "ThumbStatistics";
//            //_button.hoveredTextColor = new Color32(7, 132, 255, 255);
//            //_button.pressedTextColor = new Color32(30, 30, 44, 255);
//            //_button.disabledTextColor = new Color32(7, 7, 7, 255);
//            //_button.autoSize = true;
//            //_button.absolutePosition = pos;
//            //_button.eventClick += ShowPanelButtonClick;
            
//            //var panel = UiHolder.MainPanel.AddUIComponent<UIPanel>();
//            //panel.Hide();
//            //panel.name = PanelName;
//            //panel.absolutePosition = _button.absolutePosition + new Vector3(0, _button.height + 5);
//            //panel.backgroundSprite = "MenuPanel2";
//            //panel.width = 200;
//            //panel.height = 200;
//            //panel.autoSize = true;
            
            
//            //var heading = panel.AddUIComponent<UILabel>();
//            //heading.text = "Power Storage";
//            //heading.relativePosition = new Vector3();
//            //heading.height = 25;
//            //heading.width = panel.width;
//            //heading.textAlignment = UIHorizontalAlignment.Center;
//        }

//        private void ShowPanelButtonClick(UIComponent sender, UIMouseEventParameter e)
//        {
//            var panel = UiHolder.MainPanel.Find<UIPanel>(PanelName);
//            panel.enabled = !panel.enabled;
//            if (panel.enabled)
//            {
//                panel.Show(true);
//                UiHolder.SelectedBuilding = null;
//            }
//            else
//            {
//                panel.Hide();
//            }
//        }

//        void OnGUI()
//        {
//            if (UiHolder.View.enabled)
//            {
//                if (_uiSetup)
//                {
//                    //PowerStorageLogger.Log($"UI BuildingSettings: e:{UiHolder.MainPanel.isEnabled} v:{UiHolder.MainPanel.isVisible}", PowerStorageMessageType.Ui);
                    
//                    _windowRect = GUILayout.Window(315, _windowRect, ShowPowerStorageWindow, "Power Storage - Grid Stats");
//                    if (_windowRect.x < -800 || _windowRect.y < -300 || _windowRect.x >= Screen.width || _windowRect.y >= Screen.height)
//                    {
//                        _windowRect.position = UiHolder.MainPanel.absolutePosition + new Vector3(UiHolder.MainPanel.size.x + 5, 0, 0);
//                    }
//                }
//            }
//        }

//        void ShowPowerStorageWindow(int windowId)
//        {
//            RenderGridScreen();
//        }

//        public Vector2 ScrollPosition2;
//        private NetNode _node;
//        private void RenderGridScreen()
//        {
//            var id = WorldInfoPanel.GetCurrentInstanceID();
//            if (id == InstanceID.Empty)
//                return;

//            var found = Grid.BackupGrid.TryGetValue(id.Building, out var grid);
//            if (!found)
//                return;


//            GUILayout.Label($"Object: {grid.BuildingId} - {grid.IsDisconnectedMode} - {grid.CurrentChargeKw} - PG: {grid.PulseGroup}");

//            if (GUILayout.Button($"Disconnected Mode ({grid.IsDisconnectedMode})"))
//            {
//                grid.IsDisconnectedMode = !grid.IsDisconnectedMode;
//            }
            
//            GUILayout.Label("Max Output Kw");
//            var suggestedOutputValue = GUILayout.TextField(grid.PotentialOutputOverrideKw.ToString());
//            if (int.TryParse(suggestedOutputValue, out var outputKw))
//            {
//                grid.PotentialOutputOverrideKw = outputKw;
//            }
//            GUILayout.Label("Max Draw Kw");
//            var suggestedDrawValue = GUILayout.TextField(grid.PotentialDrawOverrideKw.ToString());
//            if (int.TryParse(suggestedDrawValue, out var drawKw))
//            {
//                grid.PotentialDrawOverrideKw = drawKw;
//            }

//            //GUILayout.Label($"Consumption: {_examinedGroup.ConsumptionKw}KW");
//            //GUILayout.Space(12f);
//            //GUILayout.Label($"Capacity Last Week: {_examinedGroup.LastCycleTotalCapacityKw/1000f}MW");
//            //GUILayout.Label($"Consumption Last Week: {_examinedGroup.LastCycleTotalConsumptionKw/1000f}MW");
//            //GUILayout.Space(12f);
//            //GUILayout.Label($"Buildings: {_examinedGroup.BuildingsList.Count}");
            
//            //GUI.contentColor = Color.white;
//            //using (var scrollViewScope = new GUILayout.ScrollViewScope(ScrollPosition2, false, true, GUILayout.Width(1200), GUILayout.Height(300)))
//            //{
//            //    ScrollPosition2 = scrollViewScope.scrollPosition;
//            //    foreach (var o in _buildingList)
//            //    {
//            //        GUILayout.BeginHorizontal();
                    
//            //        GUILayout.Label(o.Building.Info.name);
//            //        GUILayout.FlexibleSpace();
//            //        GUILayout.Label($"Electricity Buffer: {o.Building.m_electricityBuffer.ToKw()}KW");
//            //        GUILayout.FlexibleSpace();
//            //        if (GUILayout.Button("View"))
//            //        {
//            //            var id = InstanceID.Empty;
//            //            id.Building = o.Index;
//            //            Singleton<CameraController>.instance.SetTarget(id, o.Building.m_position, true);
//            //            if (false)
//            //            {
//            //                UiHolder.SelectedBuilding = o.Building;
//            //                UiHolder.SelectedBuildingId = o.Index;
//            //            }
//            //        }

//            //        GUILayout.EndHorizontal();
//            //    }
//            //}

//            //GUILayout.BeginHorizontal();
//            //if (GUILayout.Button("Back"))
//            //{
//            //    UiHolder.SelectedNetwork = null;
//            //}
//            //GUILayout.EndHorizontal();

//            //GUI.DragWindow();
//        }
//    }
//}