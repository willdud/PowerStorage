using System;
using System.IO;
using System.Linq;
using CitiesHarmony.API;
using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using UnityEngine;

namespace PowerStorage
{
    public class PowerStorage : IUserMod, ISerializableDataExtension
    {
        public static float LossRatio { get; set; } = 0.2f;
        public static int SafetyKwIntake { get; set; } = 2000;
        public static int SafetyKwDischarge { get; set; } = 2000;
        public static bool Chirp { get; set; } = true;
        public static bool DebugLog { get; set; } = false;
        public static bool Profile { get; set; } = false;
        public static PowerStorageMessageType ShownMessageTypes { get; set; } = PowerStorageMessageType.None;

        public string Name => "Power Storage";
        public string Description => "A portion of power generated is put aside to be drawn from when the grid is low.";

        private string FullSavePath => Path.Combine(Application.dataPath, ConfigFile);
        private const string ConfigFile = "PowerStorage.Config.json";


        public void OnSettingsUI(UIHelperBase helper)
        {
            LoadSettings();

            UISlider sliderPowerLoss = null;
            UISlider sliderChargeSafety = null;
            UISlider sliderDischargeSafety = null;

            var group = helper.AddGroup("Power Storage Settings");
            sliderPowerLoss = (UISlider)group.AddSlider("Power loss on conversion", 0, 1, 0.1f, LossRatio, (value) =>
            {
                if (sliderPowerLoss != null)
                {
                    sliderPowerLoss.tooltip = $"{LossRatio*100f}%";
                    sliderPowerLoss.RefreshTooltip();
                }
                    
                LossRatio = value;
            });

            sliderChargeSafety = (UISlider)group.AddSlider("Safety When Charging (KW)", 0, 10000, 500, SafetyKwIntake, (value) =>
            {
                if (sliderChargeSafety != null)
                {
                    sliderChargeSafety.tooltip = $"{SafetyKwIntake}KW";
                    sliderChargeSafety.RefreshTooltip();
                }
                SafetyKwIntake = Convert.ToInt32(value);
            });

            group.AddSpace(20);

            sliderDischargeSafety = (UISlider)group.AddSlider("Safety When Discharging (KW)", 0, 10000, 500, SafetyKwDischarge, (value) =>
            {
                if (sliderDischargeSafety != null)
                {
                    sliderDischargeSafety.tooltip = $"{SafetyKwDischarge}KW";
                    sliderDischargeSafety.RefreshTooltip();
                }
                SafetyKwDischarge = Convert.ToInt32(value);
            });

            group.AddSpace(20);
            
            group.AddCheckbox("Chrip About Low Power", Chirp, isChecked =>
            {
                Chirp = isChecked;
            });
            
            group.AddSpace(20);

            group.AddCheckbox("Profile Logging", Profile, isChecked =>
            {
                Profile = isChecked;
            });
            
            group.AddDropdown("Logging Type", Enum.GetNames(typeof(PowerStorageMessageType)), (int)ShownMessageTypes, (value) =>
            {
                switch (value)
                {
                    case 0:
                        DebugLog = false;
                        ShownMessageTypes = PowerStorageMessageType.None;
                        break;
                    case 1:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Charging;
                        break;
                    case 2:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Discharging;
                        break;
                    case 3:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Grid;
                        break;
                    case 4:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.NetworkMapping;
                        break;
                    case 5:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.NetworkMerging;
                        break;
                    case 6:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Network;
                        break;
                    case 7:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Ui;
                        break;
                    case 8:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Saving;
                        break;
                    case 9:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Loading;
                        break;
                    case 10:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Simulation;
                        break;
                    case 11:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Misc;
                        break;
                    case 12:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.All;
                        break;
                }
            });
        }
        
        public void LoadSettings()
        {
            try 
            {
                if (File.Exists(FullSavePath))
                {
                    var fileContent = File.ReadAllText(FullSavePath);
                    var psSettings = JsonUtility.FromJson<PowerStorageSettings>(fileContent);

                    LossRatio = psSettings.LossRatio;
                    SafetyKwIntake = psSettings.SafetyKwIntake;
                    SafetyKwDischarge = psSettings.SafetyKwDischarge;
                    if(psSettings.Chirp.HasValue)
                        Chirp = psSettings.Chirp.Value;
                    if(psSettings.Debug.HasValue)
                        DebugLog = psSettings.Debug.Value;
                    if(psSettings.Profile.HasValue)
                        Profile = psSettings.Profile.Value;
                }
            } 
            catch (Exception e) 
            {
                PowerStorageLogger.LogError(e.Message, PowerStorageMessageType.Loading);
                PowerStorageLogger.Log("Using default settings: " + e, PowerStorageMessageType.Loading);
            }
        }
        public void SaveSettings()
        {
            try
            {
                PowerStorageLogger.Log("Full Save Path: " + FullSavePath, PowerStorageMessageType.Saving);
                var psSettings = new PowerStorageSettings
                {
                    LossRatio = LossRatio,
                    SafetyKwIntake = SafetyKwIntake,
                    SafetyKwDischarge = SafetyKwDischarge,
                    Chirp = Chirp,
                    Debug = DebugLog,
                    Profile = Profile
                };
                File.WriteAllText(FullSavePath, JsonUtility.ToJson(psSettings));
            } 
            catch (Exception e) 
            {
                PowerStorageLogger.LogError(e.Message, PowerStorageMessageType.Saving);
                PowerStorageLogger.Log("Couldn't save settings", PowerStorageMessageType.Saving);
            }
        }
        
        public void OnCreated(ISerializableData serializedData)
        { }

        public void OnReleased()
        { }

        public void OnLoadData()
        {
            LoadSettings();
            AddColliderToBuildings();
        }

        public void OnSaveData()
        {
            SaveSettings();
        }

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(ElectricityManagerPatcher.DoPatching);
        }
        
        public void OnDisabled() 
        {
            if (HarmonyHelper.IsHarmonyInstalled) 
                ElectricityManagerPatcher.UnPatchAll();
        }

        public static BuildingAndIndex RegisterBuilding(ushort index, Building building)
        {
            var networkGameObject = new GameObject("PS-" + building.Info.name, typeof(SphereCollider), typeof(CollisionList));
            var sphereCollider = networkGameObject.GetComponent<SphereCollider>();
            sphereCollider.enabled = true;
            sphereCollider.isTrigger = true;
            sphereCollider.transform.position = building.m_position;
            sphereCollider.radius = building.Info.m_buildingAI.ElectricityGridRadius() + (19.125f * 2); // 19.125f comes from logic in ElectricityManager
            sphereCollider.gameObject.AddComponent<Rigidbody>();
            var rigid = sphereCollider.gameObject.GetComponent<Rigidbody>();
            if (rigid != null)
                rigid.isKinematic = true;
            
            networkGameObject.SetActive(true);

            var item = new BuildingAndIndex(index, building, networkGameObject);

            var existingMatch = GridsBuildingsRollup.MasterBuildingList.FirstOrDefault(p => p.Index == index);
            if (existingMatch != null)
            {
                PowerStorageLogger.Log($"Found, about to destroy {item.Building.Info.name}.", PowerStorageMessageType.Loading);
                GridsBuildingsRollup.MasterBuildingList.Remove(existingMatch);
                UnityEngine.Object.Destroy(existingMatch.GridGameObject);
            }
            
            GridsBuildingsRollup.MasterBuildingList.Add(item);

            PowerStorageLogger.Log($"building: {building.m_infoIndex}, {building.Info.name}, {building.m_position} | SphereCollider e: {sphereCollider.enabled}, p: {sphereCollider.transform.position} | {sphereCollider.transform.localPosition}", PowerStorageMessageType.Loading);

            return item;
        }
        
        public static void AddColliderToBuildings()
        {
            var watch = PowerStorageProfiler.Start("Add Collider To Buildings");
            var buffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            var buildings = buffer.Where(b => GridsBuildingsRollup.IncludedTypes.Any(it => it.IsInstanceOfType(b.Info.m_buildingAI))).ToList();
            PowerStorageLogger.Log($"Add Collider To Buildings ({buildings.Count})", PowerStorageMessageType.Loading);

            foreach (var building in buildings)
            {
                if (building.m_infoIndex == 0 && building.m_position == default)
                    continue;
                
                var index = (ushort) Array.IndexOf(buffer, building);
                if(!GridsBuildingsRollup.MasterBuildingList.Any(mbl => mbl.Index == index))
                    RegisterBuilding(index, building);
            }
            PowerStorageProfiler.Stop("Add Collider To Buildings", watch);
        }
    }
}
