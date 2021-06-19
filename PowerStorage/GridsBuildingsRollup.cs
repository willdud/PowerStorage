using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework;
using ICities;
using UnityEngine;

namespace PowerStorage
{
    public class GridsBuildingsRollup : ThreadingExtensionBase
    {
        private static int _buildingUpdatesLastTick = 0;
        private static int _buildingUpdates = 0;
        public static List<BuildingElectricityGroup> MasterBuildingsList { get; set; }

        public static BuildingElectricityGroup GetGroupContainingBuilding(Building building) => MasterBuildingsList.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.m_position == building.m_position));

        static GridsBuildingsRollup()
        {
            MasterBuildingsList = new List<BuildingElectricityGroup>(ushort.MaxValue);
        }

        public static void AddCapacity(Vector3 pos, int kw)
        {
            var buildingGroup = MasterBuildingsList.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.m_position == pos));
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
            var buildingGroup = MasterBuildingsList.FirstOrDefault(bg => bg.BuildingsList.Any(b => b.m_position == pos));
            if (!buildingGroup?.BuildingsList.Any() ?? true)
            {
                PowerStorageLogger.Log($"Building list was empty for AddConsumption {kw}KW", PowerStorageMessageType.Network);
                return;
            }
            
            buildingGroup.ConsumptionKw += kw;
            PowerStorageLogger.Log($"{kw}KW removed from {buildingGroup.CodeName} with {buildingGroup.BuildingsList.Count} members", PowerStorageMessageType.Grid);
        }
        

        public override void OnBeforeSimulationFrame()
        {
            base.OnBeforeSimulationFrame();
            var frameLoopedAt256 = (int) Singleton<SimulationManager>.instance.m_currentFrameIndex & (int)byte.MaxValue;
            var basicallyHalf = frameLoopedAt256 * 128 >> 8; // Logic stolen from DistrictManager, every 512 frames, will come through here twice as each number 
            if (basicallyHalf != 0 || frameLoopedAt256 % 2 == 0) 
                return;
            
            PowerStorageLogger.Log($"{DateTime.Now:HH:mm:ss} - {frameLoopedAt256} - {basicallyHalf}", PowerStorageMessageType.Simulation);

            foreach (var bg in MasterBuildingsList)
            {
                HandleSimulationLoop(bg);
            }

            if (_buildingUpdates == _buildingUpdatesLastTick)
                return;

            _buildingUpdatesLastTick = _buildingUpdates;
            
            var watch = PowerStorageProfiler.Start("Whole network process");
            var buildings = Singleton<BuildingManager>.instance.m_buildings;
            var unvisitedPoints = buildings.m_buffer.Where(b =>
                {
                    if (b.m_buildIndex <= 0)
                        return false;

                    var aiType = b.Info.m_buildingAI.GetType();
                    return IncludedTypes.Any(it => it.IsAssignableFrom(aiType));
                }
            ).ToList();
            MapNetworks(unvisitedPoints, out var networks);
            MergeNetworksWithMaster(networks);
            PowerStorageProfiler.Stop("Whole network process", watch);
        }

        private static void HandleSimulationLoop(BuildingElectricityGroup beg)
        {
            beg.LastCycleTotalCapacityKw = beg.CapacityKw;
            beg.LastCycleTotalConsumptionKw = beg.ConsumptionKw;
            beg.CapacityKw = 0;
            beg.ConsumptionKw = 0;
            PowerStorageLogger.Log($"This Group made: {beg.LastCycleTotalCapacityKw} and spent: {beg.LastCycleTotalConsumptionKw}", PowerStorageMessageType.Grid);
        }

        private static readonly Type[] IncludedTypes =
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

        private static void MergeNetworksWithMaster(List<List<Building>> networks)
        {
            PowerStorageLogger.Log($"Merging networks ({MasterBuildingsList.Count})", PowerStorageMessageType.NetworkMerging);
            var watch = PowerStorageProfiler.Start("MergeNetworksWithMaster");
            var now = Singleton<SimulationManager>.instance.m_currentGameTime;
            foreach (var network in networks)
            {
                var mostMatches = 0;
                BuildingElectricityGroup bestMatch = null;
                foreach (var bg in MasterBuildingsList)
                {
                    var matches = network.Count(b => bg.BuildingsList.Any(ib => b.m_position == ib.m_position));
                    if (matches <= mostMatches) 
                        continue;

                    mostMatches = matches;
                    bestMatch = bg;
                }

                if (bestMatch != null)
                {
                    PowerStorageLogger.Log($"Existing network updated - Cap: {bestMatch.LastCycleTotalCapacityKw}KW - Con: {bestMatch.LastCycleTotalConsumptionKw}KW", PowerStorageMessageType.NetworkMerging);
                    bestMatch.BuildingsList = network;
                    bestMatch.LastBuildingUpdate = now;
                }
                else
                {
                    PowerStorageLogger.Log("New Network Added", PowerStorageMessageType.NetworkMerging);
                    MasterBuildingsList.Add(new BuildingElectricityGroup
                    {
                        BuildingsList = network,
                        LastBuildingUpdate = now
                    });
                }
            }
            PowerStorageProfiler.Lap("MergeNetworksWithMaster", watch);
            for (var i = MasterBuildingsList.Count - 1; i >= 0; i--)
            {
                if(MasterBuildingsList[i].LastBuildingUpdate != now)
                    MasterBuildingsList.RemoveAt(i);
            }
            PowerStorageProfiler.Stop("MergeNetworksWithMaster", watch);
            PowerStorageLogger.Log($"Done Merging networks ({MasterBuildingsList.Count})", PowerStorageMessageType.NetworkMerging);
        }

        private static float DistanceBetweenPoints(Building one, Building two)
        {	
            var a = Mathf.Abs(one.m_position.x - two.m_position.x);
            var b = Mathf.Abs(one.m_position.z - two.m_position.z);
            return Mathf.Sqrt(a*a + b*b);
        }

        private static void MapNetworks(List<Building> unmappedPoints, out List<List<Building>> networks) 
        {
            var watch = PowerStorageProfiler.Start("MapNetworks");
            networks = new List<List<Building>>();
            while(unmappedPoints.Any())
            {
                PowerStorageLogger.Log($"MapNetworks unmappedPoints({unmappedPoints.Count})", PowerStorageMessageType.NetworkMapping);
                var point = unmappedPoints.First();
                var network = MapNetwork(point, unmappedPoints);
                unmappedPoints = unmappedPoints.Except(network).ToList();
                networks.Add(network);
                PowerStorageProfiler.Lap("MapNetworks", watch);
            }
            PowerStorageProfiler.Stop("MapNetworks", watch);

            networks = JoinNetworksByNodes(networks);
        }

        private static List<List<Building>> JoinNetworksByNodes(List<List<Building>> networks)
        {
            var watch = PowerStorageProfiler.Start("JoinNetworksByNodes");

            var buildings = Singleton<BuildingManager>.instance.m_buildings;
            var nodes = Singleton<NetManager>.instance.m_nodes;
            var networksToAdd = new List<List<Building>>();
            var networksToRemove = new List<int>();

            foreach (var network in networks)
            foreach (var building in network)
            {
                if (!(building.Info.m_buildingAI is PowerPoleAI))
                        continue;

                if(networksToRemove.Contains(networks.IndexOf(network)))
                    continue;

                var buildingIndex = Array.IndexOf(buildings.m_buffer, building);
                var node = nodes.m_buffer.FirstOrDefault(n => n.m_building == buildingIndex);

                PowerStorageLogger.Log($"Building {building.Info.name}. b:{buildingIndex} x:{building.m_position.x} z:{building.m_position.z}", PowerStorageMessageType.NetworkMapping);
                PowerStorageLogger.Log($"Node {node.Info.name}. b:{node.m_building} x:{node.m_position.x} z:{node.m_position.z}", PowerStorageMessageType.NetworkMapping);
                
                var watch2 = PowerStorageProfiler.Start("CollectBuildingIdsOnNetwork");
                var nodesExplored = CollectBuildingIdsOnNetwork(node, new List<ushort>());
                PowerStorageProfiler.Stop("CollectBuildingIdsOnNetwork", watch2);

                var networksToCombine = new List<int> { networks.IndexOf(network) };
                for (var i = 0; i < networks.Count; i++)
                {
                    if (network == networks[i])
                        continue;

                    if (networks[i].Any(n => nodesExplored.Any(b => n.m_position == b.m_position)))
                        networksToCombine.Add(i);
                }

                if (networksToCombine.Count <= 1) 
                    continue;
                
                var newMegaNetwork = new List<Building>();
                foreach (var n in networksToCombine)
                {
                    if(newMegaNetwork.Any())
                        networksToRemove.Add(n);
                    newMegaNetwork.AddRange(networks[n]);
                }
                networksToAdd.Add(newMegaNetwork);
                PowerStorageProfiler.Lap("JoinNetworksByNodes", watch);
            }

            PowerStorageLogger.Log($"Removing Networks-A ({networksToRemove.Count}) {string.Join(", ", networksToRemove.Select(i => i.ToString()).ToArray())}", PowerStorageMessageType.NetworkMapping);
            PowerStorageLogger.Log($"Removing Networks-B ({networks.Count})", PowerStorageMessageType.NetworkMapping);
            foreach (var i in networksToRemove.Distinct().OrderByDescending(n => n))
            {
                networks.RemoveAt(i);
            }

            foreach (var network in networksToAdd)
            {
                networks.Add(network);
            }
            
            
            PowerStorageProfiler.Stop("JoinNetworksByNodes", watch);
            return networks;
        }
        
        private static List<Building> CollectBuildingIdsOnNetwork(NetNode node, List<ushort> visitedSegments)
        {
            var segments = Singleton<NetManager>.instance.m_segments.m_buffer;
            var nodes = Singleton<NetManager>.instance.m_nodes.m_buffer;
            var connectedNodesBuildings = new List<Building>();
            
            void Get(ushort segment)
            {
                if (segment <= 0 || visitedSegments.Contains(segment)) return;

                visitedSegments.Add(segment);
                var s = segments[segment].m_startNode;
                var e = segments[segment].m_endNode;
                var startNode = nodes[s];
                var endNode = nodes[e];
                connectedNodesBuildings.AddRange(CollectBuildingIdsOnNetwork(startNode, visitedSegments));
                connectedNodesBuildings.AddRange(CollectBuildingIdsOnNetwork(endNode, visitedSegments));
            }

            Get(node.m_segment0);
            Get(node.m_segment1);
            Get(node.m_segment2);
            Get(node.m_segment3);
            Get(node.m_segment4);
            Get(node.m_segment5);
            Get(node.m_segment6);
            Get(node.m_segment7);

            var building = Singleton<BuildingManager>.instance.m_buildings.m_buffer[node.m_building];
            if(!connectedNodesBuildings.Any(cnb => cnb.m_position == building.m_position))
                connectedNodesBuildings.Add(building);

            return connectedNodesBuildings;
        }

        private static List<Building> MapNetwork(Building point, List<Building> unmappedPoints)
        {
            var network = new List<Building> { point };
            var newPoint = point;

            while (unmappedPoints.Any(p =>
            {
                var hasMatch = DistanceBetweenPoints(newPoint, p) <= newPoint.Info.m_buildingAI.ElectricityGridRadius() + p.Info.m_buildingAI.ElectricityGridRadius() + (19.125 * 2); // 19.125 added in ElectricityManager
                if(hasMatch)		
                    newPoint = p;
                return hasMatch;
            }))
            {
                unmappedPoints.Remove(newPoint);
                network.AddRange(MapNetwork(newPoint, unmappedPoints));
            }
            PowerStorageLogger.Log($"MapNetwork ({network.Count})", PowerStorageMessageType.NetworkMapping);
            return network;
        }
    }
}
