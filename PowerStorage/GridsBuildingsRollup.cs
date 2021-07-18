using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ICities;
using PowerStorage.Model;
using PowerStorage.Supporting;
using PowerStorage.Unity;
using UnityEngine;

namespace PowerStorage
{
    public class GridsBuildingsRollup : ThreadingExtensionBase
    {
        public static GameObject GridMesherObj;
        public static GameObject CollisionAdderObj;

        public static bool Enabled;
        
        private static int _buildingUpdatesLastTick;
        private static int _buildingUpdates;
        public static ThreadSafeListWithLock<BuildingAndIndex> MasterBuildingList { get; set; }
        public static List<BuildingElectricityGroup> BuildingsGroupedToNetworks { get; set; }

        public static BuildingElectricityGroup GetGroupContainingBuilding(BuildingAndIndex pair) => BuildingsGroupedToNetworks.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.Building.m_position == pair.Building.m_position));

        static GridsBuildingsRollup()
        {
            MasterBuildingList = new ThreadSafeListWithLock<BuildingAndIndex>();
            BuildingsGroupedToNetworks = new List<BuildingElectricityGroup>(ushort.MaxValue);
        }

        public static void AddCapacity(Vector3 pos, int kw)
        {
            var buildingGroup = BuildingsGroupedToNetworks.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.Building.m_position == pos));
            if (!buildingGroup?.BuildingsList.Any() ?? true)
            {
                PowerStorageLogger.LogWarning($"Building list was empty for AddCapacity {kw}KW. Power providing buildings should be on some kind of grid.", PowerStorageMessageType.Grid);
                return;
            }
            
            buildingGroup.CapacityKw += kw;
            PowerStorageLogger.Log($"{kw}KW added to {buildingGroup.CodeName} with {buildingGroup.BuildingsList.Count} members", PowerStorageMessageType.Grid);
        }
        
        public static void AddConsumption(Vector3 pos, int kw)
        {
            var buildingGroup = BuildingsGroupedToNetworks.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.Building.m_position == pos));
            if (!buildingGroup?.BuildingsList.Any() ?? true)
            {
                PowerStorageLogger.Log($"Building list was empty for AddConsumption {kw}KW", PowerStorageMessageType.Grid);
                return;
            }
            
            buildingGroup.ConsumptionKw += kw;
            PowerStorageLogger.Log($"{kw}KW removed from {buildingGroup.CodeName} with {buildingGroup.BuildingsList.Count} members", PowerStorageMessageType.Grid);
        }
        
        public override void OnBeforeSimulationFrame()
        {
            base.OnBeforeSimulationFrame();
            var frameLoopedAt256 = (int) Singleton<SimulationManager>.instance.m_currentFrameIndex & (int)byte.MaxValue;
            var basicallyHalf = frameLoopedAt256 * 128 >> 8; // Logic stolen from DistrictManager for breaking up the load over frames 
            if (basicallyHalf != 0 || frameLoopedAt256 % 2 == 0) 
                return;
            
            PowerStorageLogger.Log($"{DateTime.Now:HH:mm:ss} - {frameLoopedAt256} - {basicallyHalf}", PowerStorageMessageType.Simulation);

            foreach (var bg in BuildingsGroupedToNetworks)
            {
                HandleSimulationLoop(bg);
            }
            
            if (_buildingUpdates == _buildingUpdatesLastTick)
                return;
            
            
            if (Enabled && CollisionAdderObj == null)
            {
                CollisionAdderObj = new GameObject { name = "PowerStorageCollisionAdderObj" };
                var adder = CollisionAdderObj.AddComponent<CollisionAdder>();
                adder.BeginAdding();
            }

            if (Enabled && GridMesherObj == null && CollisionAdderObj != null)
            {
                if (CollisionAdder.Done)
                {
                    GridMesherObj = new GameObject { name = "PowerStorageGridMesherObj" };
                    var mesher = GridMesherObj.AddComponent<GridMesher>();
                    mesher.BeginAdding();
                }
            }
        }

        private static void HandleSimulationLoop(BuildingElectricityGroup beg)
        {
            beg.LastCycleTotalCapacityKw = beg.CapacityKw;
            beg.LastCycleTotalConsumptionKw = beg.ConsumptionKw;
            beg.CapacityKw = 0;
            beg.ConsumptionKw = 0;
            PowerStorageLogger.Log($"This Group made: {beg.LastCycleTotalCapacityKw} and spent: {beg.LastCycleTotalConsumptionKw}", PowerStorageMessageType.Grid);
        }

        public static readonly Type[] IncludedTypes =
        {
            typeof(PlayerBuildingAI), 
            typeof(ResidentialBuildingAI), 
            typeof(CommercialBuildingAI), 
            typeof(IndustrialBuildingAI), typeof(IndustrialExtractorAI), 
            typeof(OfficeBuildingAI),
            typeof(PowerPoleAI)
        };
        public static void UpdateGrid()
        {
            _buildingUpdates++;
        }
    }
}
