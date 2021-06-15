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

        public static BuildingElectricityGroup GetGroupContainingBuilding(ushort buildingId) => MasterBuildingsList.FirstOrDefault(bg => bg.BuildingsList.Contains(buildingId));

        static GridsBuildingsRollup()
        {
            MasterBuildingsList = new List<BuildingElectricityGroup>(ushort.MaxValue);
        }

        public static void AddCapacity(Vector3 pos, int kw)
        {
            var building = GetBuildingByPosition(pos);
            var buildingGroup = MasterBuildingsList.FirstOrDefault(bg => bg.BuildingsList.Contains(building));

            if (!buildingGroup?.BuildingsList.Any() ?? true)
            {
                PowerStorageLogger.LogError($"Building list was empty for AddCapacity {kw}KW. Power providing buildings should be on some kind of grid.");
                return;
            }
            
            buildingGroup.CapacityKw += kw;
            PowerStorageLogger.Log($"{kw}KW added to {buildingGroup.CodeName} with {buildingGroup.BuildingsList.Count} members");
        }
        
        public static void AddConsumption(Vector3 pos, int kw)
        {
            var building = GetBuildingByPosition(pos);
            var buildingGroup = MasterBuildingsList.FirstOrDefault(bg => bg.BuildingsList.Contains(building));

            if (!buildingGroup?.BuildingsList.Any() ?? true)
            {
                PowerStorageLogger.Log($"Building list was empty for AddConsumption {kw}KW");
                return;
            }
            
            buildingGroup.ConsumptionKw += kw;
            PowerStorageLogger.Log($"{kw}KW removed from {buildingGroup.CodeName} with {buildingGroup.BuildingsList.Count} members");
        }
        

        public override void OnBeforeSimulationFrame()
        {
            base.OnBeforeSimulationFrame();
            var frameLoopedAt256 = (int) Singleton<SimulationManager>.instance.m_currentFrameIndex & (int)byte.MaxValue;
            var basicallyHalf = frameLoopedAt256 * 128 >> 8; // Logic stolen from DistrictManager, every 512 frames, will come through here twice as each number 
            if (basicallyHalf != 0 || frameLoopedAt256 % 2 == 0) 
                return;

            foreach (var bg in MasterBuildingsList)
            {
                HandleSimulationLoop(bg);
            }

            if (_buildingUpdates == _buildingUpdatesLastTick)
                return;

            _buildingUpdatesLastTick = _buildingUpdates;
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
        }

        private static void HandleSimulationLoop(BuildingElectricityGroup beg)
        {
            beg.LastCycleTotalCapacityKw = beg.CapacityKw;
            beg.LastCycleTotalConsumptionKw = beg.ConsumptionKw;
            beg.CapacityKw = 0;
            beg.ConsumptionKw = 0;
            PowerStorageLogger.Log($"This Group made: {beg.LastCycleTotalCapacityKw} and spent: {beg.LastCycleTotalConsumptionKw}");
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
            PowerStorageLogger.Log("Merging networks");
            var now = Singleton<SimulationManager>.instance.m_currentGameTime;
            var buildings = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            foreach (var network in networks)
            {
                var networkAsIndexes = network.Select(b => (ushort)Array.IndexOf(buildings, b)).ToList();
                var mostMatches = 0;
                BuildingElectricityGroup bestMatch = null;
                foreach (var bg in MasterBuildingsList)
                {
                    var matches = networkAsIndexes.Count(b => bg.BuildingsList.Contains(b));
                    if (matches <= mostMatches) 
                        continue;

                    mostMatches = matches;
                    bestMatch = bg;
                }

                if (bestMatch != null)
                {
                    bestMatch.AiTypes = network.Select(n => n.Info.m_buildingAI.GetType().Name).Distinct().ToList();
                    bestMatch.BuildingsList = networkAsIndexes;
                    bestMatch.LastBuildingUpdate = now;
                }
                else
                {
                    MasterBuildingsList.Add(new BuildingElectricityGroup
                    {
                        AiTypes = network.Select(n => n.Info.m_buildingAI.GetType().Name).Distinct().ToList(),
                        BuildingsList = networkAsIndexes,
                        LastBuildingUpdate = now
                    });
                }
            }

            PowerStorageLogger.Log($"Merging networks ({MasterBuildingsList.Count})");
            for (var i = MasterBuildingsList.Count - 1; i >= 0; i--)
            {
                if(MasterBuildingsList[i].LastBuildingUpdate != now)
                    MasterBuildingsList.RemoveAt(i);
            }
        }

        private static float DistanceBetweenPoints(Building one, Building two)
        {	
            var a = Mathf.Abs(one.m_position.x - two.m_position.x);
            var b = Mathf.Abs(one.m_position.z - two.m_position.z);
            return Mathf.Sqrt(a*a + b*b);
        }

        private static void MapNetworks(List<Building> unmappedPoints, out List<List<Building>> networks) 
        {
            networks = new List<List<Building>>();
            while(unmappedPoints.Any())
            {
                PowerStorageLogger.Log($"MapNetworks unmappedPoints({unmappedPoints.Count})");
                var point = unmappedPoints.First();
                var network = MapNetwork(point, unmappedPoints);
                unmappedPoints = unmappedPoints.Except(network).ToList();
                networks.Add(network);
            }
            
            networks = JoinNetworksByNodes(networks);
        }

        private static List<List<Building>> JoinNetworksByNodes(List<List<Building>> networks)
        {
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

                PowerStorageLogger.Log($"Building {building.Info.name}. b:{buildingIndex} x:{building.m_position.x} z:{building.m_position.z}");
                PowerStorageLogger.Log($"Node {node.Info.name}. b:{node.m_building} x:{node.m_position.x} z:{node.m_position.z}");
                
                var nodesExplored = CollectBuildingIdsOnNetwork(node, new List<ushort>());
                var networksToCombine = new List<int> { networks.IndexOf(network) };
                for (var i = 0; i < networks.Count; i++)
                {
                    if (network == networks[i])
                        continue;

                    if (networks[i].Any(n => nodesExplored.Contains(n)))
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
            }

            PowerStorageLogger.Log($"Removing Networks-A ({networksToRemove.Count}) {string.Join(", ", networksToRemove.Select(i => i.ToString()).ToArray())}");
            PowerStorageLogger.Log($"Removing Networks-B ({networks.Count})");
            foreach (var i in networksToRemove.Distinct().OrderByDescending(n => n))
            {
                networks.RemoveAt(i);
            }

            foreach (var network in networksToAdd)
            {
                networks.Add(network);
            }
            
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
            if(!connectedNodesBuildings.Contains(building))
                connectedNodesBuildings.Add(building);

            return connectedNodesBuildings;
        }

        private static List<Building> MapNetwork(Building point, List<Building> unmappedPoints)
        {
            var network = new List<Building> { point };
            var newPoint = point;

            while (unmappedPoints.Any(p =>
            {
                var hasMatch = DistanceBetweenPoints(newPoint, p) <= newPoint.Info.m_buildingAI.ElectricityGridRadius() + p.Info.m_buildingAI.ElectricityGridRadius() + 19.125; // 19.125 added in ElectricityManager
                if(hasMatch)		
                    newPoint = p;
                return hasMatch;
            }))
            {
                unmappedPoints.Remove(newPoint);
                network.AddRange(MapNetwork(newPoint, unmappedPoints));
            }
            PowerStorageLogger.Log($"MapNetwork ({network.Count})");
            return network;
        }

        private static ushort GetBuildingByPosition(Vector3 pos)
        {
            return Singleton<BuildingManager>.instance.FindBuilding(pos, 0.01f, ItemClass.Service.None, ItemClass.SubService.None, Building.Flags.None, Building.Flags.None);
        }
    }
}
