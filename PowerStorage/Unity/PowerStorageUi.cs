using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using PowerStorage.Model;
using PowerStorage.Supporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PowerStorage.Unity
{ 
    public static class UiHolder
    {
        public static UIComponent ElecInfo;
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
            if (_powerStorageUiObj != null) 
                return;

            if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame) 
                return;
            
            var view = UIView.GetAView();
            UiHolder.View = view;

            PowerStorageLogger.Log("Add UI", PowerStorageMessageType.Ui);
            _powerStorageUiObj = new GameObject();
            _powerStorageUiObj.AddComponent<Ui>();
            _powerStorageUiObj.name = "PowerStorageUiObj";
        }

		public override void OnLevelUnloading()
        {
            PowerStorageLogger.Log("OnLevelUnloading", PowerStorageMessageType.Loading);
            if (_powerStorageUiObj == null) 
                return;

            Object.Destroy(_powerStorageUiObj);
            _powerStorageUiObj = null;
        }
	}

    public class Ui : MonoBehaviour
    {
        private Rect _windowRect = new Rect(Screen.width - 1200, Screen.height - 650, 1000, 500);
        private bool _showingWindow = false;
        private bool _showingFacilities = false;
        private bool _showingColliders = false;
        private bool _uiSetup = false;
        private UIButton _button;

        private void SetupUi()
        {
            var c = UIView.Find("(Library) ElectricityInfoViewPanel");

            if (c == null || !c.isEnabled || !c.isVisible)
                return;

            _uiSetup = true;
            UiHolder.ElecInfo = c;
            PowerStorageLogger.Log($"Elec: {c.name}", PowerStorageMessageType.Ui);
            var pos = UiHolder.ElecInfo.absolutePosition + new Vector3(UiHolder.ElecInfo.size.x + 5, 0, 0);
            UiHolder.ButtonX = (pos.x + c.width) * UiHolder.View.inputScale - 2;
            UiHolder.ButtonY = (pos.y + 10) * UiHolder.View.inputScale;

            _button = UiHolder.ElecInfo.AddUIComponent<UIButton>();
            _button.text = "Power Storage";
            _button.normalBgSprite = "ButtonMenu";
            _button.normalFgSprite = "ThumbStatistics";
            _button.hoveredTextColor = new Color32(7, 132, 255, 255);
            _button.pressedTextColor = new Color32(30, 30, 44, 255);
            _button.disabledTextColor = new Color32(7, 7, 7, 255);
            _button.autoSize = true;
            _button.absolutePosition = pos;
            _button.eventClick += ButtonClick;
        }

        private void ButtonClick(UIComponent sender, UIMouseEventParameter e)
        {
            if (_showingWindow)
            {
                _showingFacilities = false;
                _showingColliders = false;
                UiHolder.SelectedBuilding = null;
                UiHolder.SelectedNetwork = null;
            }
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
                else
                {
                    PowerStorageLogger.Log($"UI ElecInfo: e:{UiHolder.ElecInfo.isEnabled} v:{UiHolder.ElecInfo.isVisible}", PowerStorageMessageType.Ui);
                    if (UiHolder.ElecInfo != null && UiHolder.ElecInfo.isVisible)
                        _button.Show();
                    else
                        _button.Hide();

                    if (_showingWindow)
                    {
                        _windowRect = GUILayout.Window(315, _windowRect, ShowPowerStorageWindow, "Power Storage - Grid Stats");
                        if (_windowRect.x < -800 || _windowRect.y < -300 || _windowRect.x >= Screen.width || _windowRect.y >= Screen.height)
                        {
                            _windowRect.position = new Vector2(Screen.width - 1200, Screen.height - 650);
                        }
                    }
                }
            }
        }

        void ShowPowerStorageWindow(int windowId)
        {
            if (!GridsBuildingsRollup.Enabled)
            {
                if (GUILayout.Button(GridsBuildingsRollup.Enabled ? "On" : "Off"))
                {
                    GridsBuildingsRollup.Enabled = !GridsBuildingsRollup.Enabled;
                }
            }
            else
            {
                GUILayout.Label($"{CollisionAdder.Progress}/{CollisionAdder.Total}");
            }

            if (_showingFacilities)
            {
                RenderFacilitiesScreen();
            } 
            else if (_showingColliders)
            {
                RenderCollidersScreen();
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
        private KeyValuePair<int, BuildingElectricityGroup>[] _begClone = new KeyValuePair<int, BuildingElectricityGroup>[0];
        private void RenderStatsScreen()
        {
            if(Event.current.type == EventType.Layout)
                _begClone = GridsBuildingsRollup.BuildingsGroupedToNetworks.Select((g, i) => new KeyValuePair<int, BuildingElectricityGroup>(i, g)).OrderByDescending(bg => bg.Value.BuildingsList.Count).ToArray();
            
            using (var scrollViewScope = new GUILayout.ScrollViewScope(ScrollPosition1, false, true, GUILayout.Width(1200), GUILayout.Height(450)))
            {
                ScrollPosition1 = scrollViewScope.scrollPosition;

                foreach (var beg in _begClone)
                {
                    var c = beg.Value;
                    if (c == null || !c.BuildingsList.Any())
                        continue;

                    if(c.LastCycleTotalCapacityKw == c.LastCycleTotalConsumptionKw)
                        GUI.contentColor = Color.white;
                    else if (c.LastCycleTotalConsumptionKw > c.LastCycleTotalCapacityKw)
                        GUI.contentColor = Color.red;
                    else
                        GUI.contentColor = Color.green;

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
                        UiHolder.SelectedNetwork = beg.Key;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Storage Facilities"))
            {
                _showingFacilities = true;
                _showingColliders = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Colliders"))
            {
                _showingFacilities = false;
                _showingColliders = true;
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
        private BuildingAndIndex[] _buildingList = new BuildingAndIndex[0];
        private NetNode _node;

        private void RenderGridScreen(int index)
        {
            if (Event.current.type == EventType.Layout)
            {
                _examinedGroup = GridsBuildingsRollup.BuildingsGroupedToNetworks.ElementAt(index);
                _buildingList = _examinedGroup.BuildingsList.ToArray();
            }

            if (_examinedGroup != null)
            {
                if(_examinedGroup.LastCycleTotalCapacityKw == _examinedGroup.LastCycleTotalConsumptionKw)
                    GUI.contentColor = Color.white;
                else if (_examinedGroup.LastCycleTotalConsumptionKw > _examinedGroup.LastCycleTotalCapacityKw)
                    GUI.contentColor = Color.red;
                else
                    GUI.contentColor = Color.green;
                
                GUILayout.Label(_examinedGroup.CodeName);
                GUILayout.Label($"Capacity: {_examinedGroup.CapacityKw}KW");
                GUILayout.Label($"Consumption: {_examinedGroup.ConsumptionKw}KW");
                GUILayout.Space(12f);
                GUILayout.Label($"Capacity Last Week: {_examinedGroup.LastCycleTotalCapacityKw/1000f}MW");
                GUILayout.Label($"Consumption Last Week: {_examinedGroup.LastCycleTotalConsumptionKw/1000f}MW");
                GUILayout.Space(12f);
                GUILayout.Label($"Buildings: {_examinedGroup.BuildingsList.Count}");
                
                GUI.contentColor = Color.white;
                using (var scrollViewScope = new GUILayout.ScrollViewScope(ScrollPosition2, false, true, GUILayout.Width(1200), GUILayout.Height(300)))
                {
                    ScrollPosition2 = scrollViewScope.scrollPosition;
                    foreach (var o in _buildingList)
                    {
                        GUILayout.BeginHorizontal();
                        
                        GUILayout.Label(o.Building.Info.name);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"Electricity Buffer: {o.Building.m_electricityBuffer.ToKw()}KW");
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("View"))
                        {
                            var id = InstanceID.Empty;
                            id.Building = o.Index;
                            Singleton<CameraController>.instance.SetTarget(id, o.Building.m_position, true);
                            if (false)
                            {
                                UiHolder.SelectedBuilding = o.Building;
                                UiHolder.SelectedBuildingId = o.Index;
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
        private Hashtable _snapshotOfBackupGrid;
        public Vector2 ScrollPosition3;
        private void RenderFacilitiesScreen()
        {
            if (Event.current.type == EventType.Layout)
            {
                _snapshotOfBackupGrid = (Hashtable)PowerStorageAi.BackupGrid.Clone();
            }

            using (var scrollViewScope = new GUILayout.ScrollViewScope(ScrollPosition3, false, true, GUILayout.Width(1200), GUILayout.Height(450)))
            {
                ScrollPosition3 = scrollViewScope.scrollPosition;

                GUI.contentColor = Color.white;
                foreach (DictionaryEntry entry in _snapshotOfBackupGrid)
                {
                    var buildingIndex = (ushort) entry.Key;
                    var member = (GridMemberLastTickStats) entry.Value;
                    
                    if(member.ChargeProvidedKw == 0 && member.ChargeTakenKw == 0)
                        GUI.contentColor = Color.white;
                    else if (member.ChargeProvidedKw > 0)
                        GUI.contentColor = Color.red;
                    else
                        GUI.contentColor = Color.green;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Id: " + buildingIndex);
                    GUILayout.Label("Network Name: " + member.NeworkName);
                    GUILayout.Label("Type: " + member.BuildingPair.Building.Info.name);   
                    if (GUILayout.Button("View"))
                    {
                        var id = InstanceID.Empty;
                        id.Building = buildingIndex;
                        Singleton<CameraController>.instance.SetTarget(id, member.BuildingPair.Building.m_position, true);
                        if (false)
                        {
                            UiHolder.SelectedBuilding = member.BuildingPair.Building;
                            UiHolder.SelectedBuildingId = member.BuildingPair.Index;
                        }
                    }
                    GUILayout.EndHorizontal();                
                    
                    GUI.contentColor = Color.white;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Is Active: " + member.IsActive);
                    GUILayout.Label("Is Full: " + member.IsFull);
                    GUILayout.Label("Is Off: " + member.IsOff);
                    GUILayout.EndHorizontal();

                    GUILayout.Label("Charge: " + member.CurrentChargeKw + "KW / " + member.CapacityKw + "KW");
                    GUILayout.Label("Potential Output: " + member.PotentialOutputKw + "KW");
                    GUILayout.Label("Currently Providing: " + member.ChargeProvidedKw + "KW");
                    GUILayout.Label("Currently Drawing: " + member.ChargeTakenKw + "KW - Loss: " + member.LossKw + "KW");
                    GUILayout.Space(22f);
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
        
        public Vector2 ScrollPosition4;
        private static BuildingAndIndex[] _snapshotOfColliders;
        private void RenderCollidersScreen()
        {
            if (Event.current.type == EventType.Layout)
            {
                _snapshotOfColliders = GridsBuildingsRollup.MasterBuildingList.Take(50).ToArray();
            }

            using (var scrollViewScope = new GUILayout.ScrollViewScope(ScrollPosition4, false, true, GUILayout.Width(1200), GUILayout.Height(450)))
            {
                ScrollPosition4 = scrollViewScope.scrollPosition;

                GUI.contentColor = Color.white;
                foreach (var bi in _snapshotOfColliders)
                {
                    var entry = bi.GridGameObject.GetComponent<BoxCollider>();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Name: " + entry.name);
                    GUILayout.Label("Tag: " + entry.tag); 
                    GUILayout.Label("Enabled: " + entry.enabled);
                    GUILayout.Label("Size: " + entry.size + " | Center:" + entry.center);
                    GUILayout.Label("Bounds: " + entry.bounds.size + " | Center:" + entry.bounds.center);
                    GUILayout.Label("Transform: pos:" + entry.transform?.position + " | lpos:" + entry.transform?.localPosition);
                    GUILayout.Label("Rigid: pos:" + entry.attachedRigidbody?.position + " | detect:" + entry.attachedRigidbody?.detectCollisions);
                    GUILayout.Label("Is Trigger: " + entry.isTrigger);
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back"))
            {
                _showingColliders = false;
                _showingFacilities = false;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
    }
}
