using System;
using System.Collections;
using System.Linq;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PowerStorage
{ 
    public static class UiHolder
    {
        public static UIComponent TsBar;
        public static float ButtonX;
        public static float ButtonY;
        public static int? SelectedNetwork;
        public static Building? SelectedBuilding;
        public static ushort SelectedBuildingId;
        public static UIView View;
    }

    public class PowerStorageUi : LoadingExtensionBase
	{
        private GameObject _powerStorageUiObj;

        public override void OnLevelLoaded(LoadMode mode)
		{
            PowerStorageLogger.Log("Loading", PowerStorageMessageType.Loading);
            if (_powerStorageUiObj == null)
            {
                if (mode == LoadMode.NewGame || mode == LoadMode.LoadGame)
                {
                    PowerStorageLogger.Log("Add UI", PowerStorageMessageType.Ui);
                    _powerStorageUiObj = new GameObject();
                    _powerStorageUiObj.AddComponent<Ui>();
                    _powerStorageUiObj.name = "PowerStorageUiObj";
                }
            }

            var view = UIView.GetAView();
            UiHolder.View = view;
            var c = UIView.Find("TSBar");;
            UiHolder.TsBar = c;
            PowerStorageLogger.Log($"TS: {c?.name}", PowerStorageMessageType.Ui);

            var pos = c.absolutePosition;
            UiHolder.ButtonX = (pos.x + c.width) * view.inputScale - 2;
            UiHolder.ButtonY = (pos.y + 10) * view.inputScale;
        }

		public override void OnLevelUnloading()
        {
            PowerStorageLogger.Log($"OnLevelUnloading", PowerStorageMessageType.Loading);
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
        private bool _showingFacilities = false;
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
            PowerStorageLogger.Log("Button clicked", PowerStorageMessageType.Ui);
            _showingWindow = !_showingWindow;
        }

        void OnGUI()
        {
            if (UiHolder.View.enabled)
            {
                if (!_uiSetup)
                {
                    PowerStorageLogger.Log("Setting up UI", PowerStorageMessageType.Ui);
                    SetupUi();
                }

                if (_showingWindow)
                {
                    _windowRect.position = new Vector2(25, 50);
                    _windowRect = GUILayout.Window(314, _windowRect, ShowPowerStorageWindow, "Power Storage - Grid Stats");
                }
            }
        }

        void ShowPowerStorageWindow(int windowId)
        {
            if (_showingFacilities)
            {
                RenderFacilitiesScreen();
            } 
            else if (UiHolder.SelectedNetwork.HasValue)
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
                    GUILayout.Label($"Capacity Last Week: {c.LastCycleTotalCapacityKw/1000f}MW");
                    GUILayout.Label($"Consumption Last Week: {c.LastCycleTotalConsumptionKw/1000f}MW");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Buildings: {c.BuildingsList.Count}");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"Updated: {c.LastBuildingUpdate:HH:mm:ss}");
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
            if (GUILayout.Button("Storage Facilities"))
            {
                _showingFacilities = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
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
        private Building[] _buildingList = new Building[0];
        private NetNode _node;

        private void RenderGridScreen(int index)
        {
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
                GUILayout.Label($"Capacity Last Week: {_examinedGroup.LastCycleTotalCapacityKw/1000f}MW");
                GUILayout.Label($"Consumption Last Week: {_examinedGroup.LastCycleTotalConsumptionKw/1000f}MW");
                GUILayout.Space(12f);
                GUILayout.Label($"Buildings: {_examinedGroup.BuildingsList.Count}");
                
                using (var scrollViewScope = new GUILayout.ScrollViewScope(ScrollPosition2, false, true, GUILayout.Width(1200), GUILayout.Height(450)))
                {
                    ScrollPosition2 = scrollViewScope.scrollPosition;
                    foreach (var buildingStruct in _buildingList)
                    {
                        GUILayout.BeginHorizontal();
                        
                        GUILayout.Label(buildingStruct.Info.name);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"Electricity Buffer: {buildingStruct.m_electricityBuffer.ToKw()}KW");
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("View"))
                        {
                            var buffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
                            var id = InstanceID.Empty;
                            var buildingIndex = (ushort) Array.IndexOf(buffer, buildingStruct);
                            id.Building = buildingIndex;
                            Singleton<CameraController>.instance.SetTarget(id, buildingStruct.m_position, true);
                            if (false)
                            {
                                UiHolder.SelectedBuilding = buildingStruct;
                                UiHolder.SelectedBuildingId = (ushort)Array.IndexOf(buffer, buildingStruct);
                            }
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
        
        private void RenderBuildingScreen(Building building)
        {
            if (Event.current.type == EventType.Layout)
            {            
                var nodes = Singleton<NetManager>.instance.m_nodes;
                _node = nodes.m_buffer.FirstOrDefault(n => n.m_building == UiHolder.SelectedBuildingId);
            }

            GUI.contentColor = Color.white;
            GUILayout.Label(building.Info.GetType().ToString());
            GUILayout.Space(12f);
            GUILayout.Label("Building: " + UiHolder.SelectedBuildingId);
            GUILayout.Label("Building Node: " + building.m_netNode);
            GUILayout.Label("Next Grid Node: " + _node.m_nextGridNode);
            GUILayout.Label("Node Connect Count: " + _node.m_connectCount);
            GUILayout.Label("Node Segment0: " + _node.m_segment0);
            GUILayout.Label("Node Segment1: " + _node.m_segment1);
            GUILayout.Label("Node Segment2: " + _node.m_segment2);
            GUILayout.Label("Node Segment3: " + _node.m_segment3);
            GUILayout.Label("Node Segment4: " + _node.m_segment4);
            GUILayout.Label("Node Segment5: " + _node.m_segment5);
            GUILayout.Label("Node Segment6: " + _node.m_segment6);
            GUILayout.Label("Node Segment7: " + _node.m_segment7);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back"))
            {
                UiHolder.SelectedBuilding = null;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        // ushort / GridMemberLastTickStats
        private Hashtable _snapshotOfBackupGrid = null;
        public Vector2 ScrollPosition3;
        private void RenderFacilitiesScreen()
        {
            if (Event.current.type == EventType.Layout)
            {
                _snapshotOfBackupGrid = (Hashtable)PowerStorageAi.BackupGrid.Clone();
            }

            using (var scrollViewScope = new GUILayout.ScrollViewScope(ScrollPosition3, false, true,
                GUILayout.Width(1200), GUILayout.Height(450)))
            {
                ScrollPosition3 = scrollViewScope.scrollPosition;

                GUI.contentColor = Color.white;
                foreach (DictionaryEntry entry in _snapshotOfBackupGrid)
                {
                    var id = (ushort) entry.Key;
                    var member = (GridMemberLastTickStats) entry.Value;

                    GUILayout.Label(id.ToString());
                    GUILayout.Label("Type: " + member.Building.Info.name);                    
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Is Active: " + member.IsActive);
                    GUILayout.Label("   Is Full: " + member.IsFull);
                    GUILayout.Label("   Is Off: " + member.IsOff);
                    GUILayout.EndHorizontal();
                    GUILayout.Label("Charge: " + member.CurrentChargeKw + "KW / " + member.CapacityKw + "KW");
                    GUILayout.Label("Potential Output: " + member.PotentialOutputKw + "KW");
                    GUILayout.Label("Currently Providing: " + member.ChargeProvidedKw + "KW");
                    GUILayout.Label("Currently Drawing: " + member.ChargeTakenKw + "KW - Loss: " + member.LossKw + "KW");
                    GUILayout.Space(12f);
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back"))
            {
                _showingFacilities = false;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
    }
}
