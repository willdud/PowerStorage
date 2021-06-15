using System.Linq;
using ColossalFramework;
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
        public static int? SelectedNetwork;
        public static ushort? SelectedBuilding;
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
                    _powerStorageUiObj.name = "PowerStorageUiObj";
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
            PowerStorageLogger.Log($"OnLevelUnloading");
            if (_powerStorageUiObj == null) 
                return;

            Object.Destroy(_powerStorageUiObj);
            _powerStorageUiObj = null;
        }
	}

    public class Ui : MonoBehaviour
    {
        private Rect _windowRect = new Rect(Screen.width - 1200, Screen.height - 650, 1200, 500);
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
            if (UiHolder.SelectedNetwork.HasValue)
            {
                if (UiHolder.SelectedBuilding.HasValue)
                {
                    RenderBuildingScreen(UiHolder.SelectedBuilding.Value);
                }
                else
                {
                    RenderGridScreen(UiHolder.SelectedNetwork.Value);
                }
            }
            else
            {
                RenderStatsScreen();
            }
        }

        public Vector2 ScrollPosition1;
        BuildingElectricityGroup[] _begClone = new BuildingElectricityGroup[0];
        private void RenderStatsScreen()
        {
            if(Event.current.type == EventType.Layout)
                _begClone = GridsBuildingsRollup.MasterBuildingsList.ToArray();

            var index = 0;
            using (var scrollViewScope = new GUILayout.ScrollViewScope(ScrollPosition1, false, true, GUILayout.Width(1200), GUILayout.Height(450)))
            {
                ScrollPosition1 = scrollViewScope.scrollPosition;

                foreach (var c in _begClone)
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
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Show"))
                    {
                        UiHolder.SelectedNetwork = index;
                    }
                    index++;
                    GUILayout.EndHorizontal();
                }
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

        
        public Vector2 ScrollPosition2;
        private BuildingElectricityGroup _examinedGroup = null;
        private ushort[] _buildingList = new ushort[0];
        private void RenderGridScreen(int index)
        {
            var buffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            if (Event.current.type == EventType.Layout)
            {
                _examinedGroup = GridsBuildingsRollup.MasterBuildingsList.ElementAt(index);
                _buildingList = _examinedGroup.BuildingsList.ToArray();
            }

            if (_examinedGroup != null)
            {
                GUI.contentColor = Color.white;
                
                GUILayout.Label(_examinedGroup.CodeName);
                GUILayout.Label($"Capacity: {_examinedGroup.CapacityKw}KW");
                GUILayout.Label($"Consumption: {_examinedGroup.ConsumptionKw}KW");
                GUILayout.Space(12f);
                GUILayout.Label($"Capacity Last Week: {_examinedGroup.LastCycleTotalCapacityKw}KW");
                GUILayout.Label($"Consumption Last Week: {_examinedGroup.LastCycleTotalConsumptionKw}KW");
                GUILayout.Space(12f);
                GUILayout.Label($"Buildings: {_examinedGroup.BuildingsList.Count}");
                
                using (var scrollViewScope = new GUILayout.ScrollViewScope(ScrollPosition2, false, true, GUILayout.Width(1200), GUILayout.Height(450)))
                {
                    ScrollPosition2 = scrollViewScope.scrollPosition;
                    foreach (var building in _buildingList)
                    {
                        var buildingStruct = buffer[building];
                        GUILayout.BeginHorizontal();

                        GUILayout.Label(building.ToString());
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(buildingStruct.Info.name);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(buildingStruct.Info.m_buildingAI.name);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(buildingStruct.Info.m_buildingAI.name);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("View"))
                        {
                            var id = InstanceID.Empty;
                            id.Building = building;
                            Singleton<CameraController>.instance.SetTarget(id, buildingStruct.m_position, true);
                            UiHolder.SelectedBuilding = building;
                        }

                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back"))
            {
                UiHolder.SelectedNetwork = null;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
        
        private void RenderBuildingScreen(ushort buildingIndex)
        {
            var buffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            var nodes = Singleton<NetManager>.instance.m_nodes;

            var building = buffer[buildingIndex];
            var node = nodes.m_buffer.FirstOrDefault(n => n.m_building == buildingIndex);
            if (Event.current.type == EventType.Layout)
            {
            }

            GUI.contentColor = Color.white;
            GUILayout.Label(building.Info.GetType().ToString());
            GUILayout.Space(12f);
            GUILayout.Label("Building: " + buildingIndex);
            GUILayout.Label("Building Node: " + building.m_netNode);
            GUILayout.Label("Next Grid Node: " + node.m_nextGridNode);
            GUILayout.Label("Node Connect Count: " + node.m_connectCount);
            GUILayout.Label("Node Segment0: " + node.m_segment0);
            GUILayout.Label("Node Segment1: " + node.m_segment1);
            GUILayout.Label("Node Segment2: " + node.m_segment2);
            GUILayout.Label("Node Segment3: " + node.m_segment3);
            GUILayout.Label("Node Segment4: " + node.m_segment4);
            GUILayout.Label("Node Segment5: " + node.m_segment5);
            GUILayout.Label("Node Segment6: " + node.m_segment6);
            GUILayout.Label("Node Segment7: " + node.m_segment7);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back"))
            {
                UiHolder.SelectedBuilding = null;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
    }
}
