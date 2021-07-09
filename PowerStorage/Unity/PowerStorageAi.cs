using System;
using System.Collections;
using System.Linq;
using ColossalFramework;
using PowerStorage.Model;
using PowerStorage.Supporting;
using UnityEngine;

namespace PowerStorage.Unity
{
    public class PowerStorageAi : PowerPlantAI
    {
        // ushort / GridMemberLastTickStats 
        public static Hashtable BackupGrid { get; internal set; }
        public DateTime LastAnnoyance { get; set; }
        
        private bool _chargingChirpFlag;

        public PowerStorageAi()
        {
            BackupGrid = new Hashtable();
            LastAnnoyance = Singleton<SimulationManager>.instance.m_currentGameTime;
        }
        
        public override void CreateBuilding(ushort buildingId, ref Building data)
        {
            base.CreateBuilding(buildingId, ref data);

            InitialiseBuilding(buildingId, data);
        }

        private GridMemberLastTickStats InitialiseBuilding(ushort buildingId, Building data)
        {
            m_resourceType = TransferManager.TransferReason.None;
            var pair = GridsBuildingsRollup.MasterBuildingList.FirstOrDefault(b => b.Building.m_position == data.m_position) ?? CollisionAdder.RegisterBuilding(buildingId, data);
            if (pair == null)
                return null;

            var newGrid = new GridMemberLastTickStats
            {
                BuildingPair = pair,
                CapacityKw = m_resourceCapacity.ToKw(),
                PotentialOutputKw = m_resourceConsumption.ToKw()
            };
            BackupGrid[buildingId] = newGrid;
            return newGrid;
        }

        public override void ReleaseBuilding(ushort buildingId, ref Building data)
        {
            BackupGrid.Remove(buildingId);
            base.ReleaseBuilding(buildingId, ref data);
        }
        
        protected override void AddProductionCapacity(ref District districtData, int electricityProduction)
        {
            districtData.m_productionData.m_tempElectricityCapacity += (uint) electricityProduction;
            districtData.m_productionData.m_tempRenewableElectricity += (uint) electricityProduction;
        }

        protected void AddProductionConsumption(ref District districtData, int electricityProduction)
        {
            districtData.m_playerConsumption.m_tempElectricityConsumption += (uint) electricityProduction;
        }

        public override void GetElectricityProduction(out int min, out int max)
        {
            min = 0;
            max = m_electricityProduction;
        }
        
        protected override void ProduceGoods(
            ushort buildingId,
            ref Building buildingData,
            ref Building.Frame frameData,
            int productionRate,
            int finalProductionRate,
            ref Citizen.BehaviourData behaviour,
            int aliveWorkerCount,
            int totalWorkerCount,
            int workPlaceCount,
            int aliveVisitorCount,
            int totalVisitorCount,
            int visitPlaceCount)
        {
            var myGridData = BackupGrid[buildingId] as GridMemberLastTickStats;
            if (myGridData?.BuildingPair?.GridGameObject == null)
                myGridData = InitialiseBuilding(buildingId, buildingData);
            
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(myGridData.BuildingPair);
            if(grid != null)
                myGridData.NeworkName = grid.CodeName;

            var energyCapacityKw = myGridData.CapacityKw;
            var energyReserveKw = myGridData.CurrentChargeKw;
            
            var demandKw = GetLocalPowerDemand(myGridData.BuildingPair);
            var instance = Singleton<DistrictManager>.instance;
            var district = instance.GetDistrict(buildingData.m_position);

            if (myGridData.IsOff)
            {
                PowerStorageLogger.Log($"Storage facility ({buildingId}) off", PowerStorageMessageType.Grid);
            }
            else if (demandKw <= 0)
            {
                _chargingChirpFlag = true;

                finalProductionRate = 0;
                myGridData.ChargeProvidedKw = 0;
                
                PowerStorageLogger.Log($"Negative demand, beginning charge {energyReserveKw} < {energyCapacityKw}", PowerStorageMessageType.Charging);
                if (energyReserveKw < energyCapacityKw)
                {
                    var excess = Math.Max(0, ((demandKw * -1) - PowerStorage.SafetyKwIntake));
                    var portionOfExcess = excess / Math.Max(1, LocalMembers(myGridData.BuildingPair) - BackupGridFullCount(myGridData.BuildingPair));
                    var amountToAddKw = Math.Min(m_resourceConsumption.ToKw(), portionOfExcess);    
                    var amountToSubtractKw = amountToAddKw;
                    amountToAddKw = (int)(amountToAddKw * (1 - PowerStorage.LossRatio));
                    PowerStorageLogger.Log($"Storage facility ({buildingId}) Storing:{amountToAddKw} | Pull from grid:{amountToSubtractKw}", PowerStorageMessageType.Charging);
                    if (myGridData.CurrentChargeKw + amountToAddKw > m_resourceCapacity.ToKw())
                    {
                        amountToAddKw = m_resourceCapacity.ToKw() - myGridData.CurrentChargeKw;
                        PowerStorageLogger.Log($"Storage facility ({buildingId}) full.", PowerStorageMessageType.Charging);
                    }

                    myGridData.ChargeTakenKw = amountToSubtractKw;
                    myGridData.CurrentChargeKw += amountToAddKw;
                    AddProductionConsumption(ref instance.m_districts.m_buffer[district], amountToSubtractKw.KwToSilly()); // silly numbers not KW/MW
                    Singleton<ElectricityManager>.instance.TryFetchElectricity(buildingData.m_position, amountToSubtractKw.KwToSilly(), amountToSubtractKw.KwToSilly());

                    PowerStorageLogger.Log($"Battery Charging: {myGridData.CurrentChargeKw}/{energyCapacityKw} | added: {amountToAddKw}", PowerStorageMessageType.Charging);
                }
            }
            else
            {
                ChirpAboutThePowerIssues(myGridData.BuildingPair);
                
                //Electricity / Production
                myGridData.ChargeTakenKw = 0;
                var demandWithSafetyKw = demandKw + PowerStorage.SafetyKwDischarge;
                var portionOfDemandKw = CalculateMyShare(myGridData.BuildingPair, demandWithSafetyKw);
                PowerStorageLogger.Log("Portion of demand:" + portionOfDemandKw, PowerStorageMessageType.Discharging);
                GetElectricityProduction(out _, out var max); // Silly
                max = max.ToKw();
                var outputKw = Math.Min(portionOfDemandKw, energyReserveKw);
                outputKw = Math.Min(outputKw, max);
                if ((buildingData.m_flags & (Building.Flags.Evacuating | Building.Flags.Active)) == Building.Flags.Active)
                {
                    finalProductionRate = outputKw / max * 100;
                    myGridData.ChargeProvidedKw = outputKw;
                    myGridData.CurrentChargeKw -= outputKw;

                    AddProductionCapacity(ref instance.m_districts.m_buffer[district], outputKw.KwToSilly());
                    PowerStorageLogger.Log($"Running On Battery: {energyReserveKw}/{energyCapacityKw} | used: {outputKw} | max was: {max}", PowerStorageMessageType.Discharging);
                    Singleton<ElectricityManager>.instance.TryDumpElectricity(buildingData.m_position, outputKw.KwToSilly(), outputKw.KwToSilly());

                    //Noise
                    var rate = finalProductionRate * m_noiseAccumulation / 100;
                    if (rate != 0)
                        Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, rate, buildingData.m_position, m_noiseRadius);

                    //Pollution
                    var pollution = finalProductionRate * m_pollutionAccumulation / 100;
                    if (pollution != 0)
                    {
                        var reducedPollution = UniqueFacultyAI.DecreaseByBonus(UniqueFacultyAI.FacultyBonus.Science, pollution);
                        Singleton<NaturalResourceManager>.instance.TryDumpResource(NaturalResourceManager.Resource.Pollution, reducedPollution, reducedPollution, buildingData.m_position, m_pollutionRadius);
                    }
                }
            }
            
            buildingData.m_problems = myGridData.CurrentChargeKw < 1 
                ? Notification.AddProblems(buildingData.m_problems, Notification.Problem.NoFuel) 
                : Notification.RemoveProblems(buildingData.m_problems, Notification.Problem.NoFuel);

            //Death
            HandleDead(buildingId, ref buildingData, ref behaviour, totalWorkerCount);
            frameData.m_productionState += (byte) finalProductionRate;
            
            if ((buildingData.m_problems & Notification.Problem.TurnedOff) !=  Notification.Problem.TurnedOff)
            {
                myGridData.IsOff = false;
                buildingData.m_flags |= Building.Flags.Active;
                if (m_supportEvents == 0 && buildingData.m_eventIndex == 0)
                    return;
                CheckEvents(buildingId, ref buildingData);
            }
            else
            {
                myGridData.IsOff = true;
                myGridData.ChargeProvidedKw = 0;
                myGridData.ChargeTakenKw = 0;
                buildingData.m_flags &= Building.Flags.ContentMask | Building.Flags.IncomingOutgoing | Building.Flags.CapacityFull | Building.Flags.Created | Building.Flags.Deleted | Building.Flags.Original | Building.Flags.CustomName | Building.Flags.Untouchable | Building.Flags.FixedHeight | Building.Flags.RateReduced | Building.Flags.HighDensity | Building.Flags.RoadAccessFailed | Building.Flags.Evacuating | Building.Flags.Completed | Building.Flags.Abandoned | Building.Flags.Demolishing | Building.Flags.ZonesUpdated | Building.Flags.Downgrading | Building.Flags.Collapsed | Building.Flags.Upgrading | Building.Flags.SecondaryLoading | Building.Flags.Hidden | Building.Flags.EventActive | Building.Flags.Flooded | Building.Flags.Filling | Building.Flags.Historical;
            }
        }

        private void ChirpAboutThePowerIssues(BuildingAndIndex pair)
        {
            if (!PowerStorage.Chirp || !_chargingChirpFlag || pair.Index != LowestIdBuilding(pair).Index) 
                return;

            var theTime = Singleton<SimulationManager>.instance.m_currentGameTime;
            if (LastAnnoyance.AddDays(7) >= theTime) // arbitrary rate limit
                return;

            _chargingChirpFlag = false;
            LastAnnoyance = theTime;
            var cp = Singleton<MessageManager>.instance;
            cp.QueueMessage(new CustomCitizenMessage(
                Singleton<MessageManager>.instance.GetRandomResidentID(),
                ChirpsAboutPowerLoss[theTime.Ticks % ChirpsAboutPowerLoss.Length],
                null)
            );
        }

        public override int GetElectricityRate(ushort buildingId, ref Building data)
        {
            var myGridData = BackupGrid[buildingId] as GridMemberLastTickStats ?? new GridMemberLastTickStats();
            var energyReserveKw = myGridData.CurrentChargeKw;
            var productionRate = (int) data.m_productionRate;
            int calculatedRate;
            if ((data.m_flags & (Building.Flags.Evacuating | Building.Flags.Active)) == Building.Flags.Active)
            {
                var budget = Singleton<EconomyManager>.instance.GetBudget(m_info.m_class);
                calculatedRate = GetProductionRate(productionRate, budget);
                calculatedRate = Mathf.Min(calculatedRate, energyReserveKw / myGridData.CapacityKw * 100);
            }
            else
                calculatedRate = 0;

            var position = data.m_position;
            var pair = GridsBuildingsRollup.MasterBuildingList.FirstOrDefault(b => b.Building.m_position == position) ?? CollisionAdder.RegisterBuilding(buildingId, data);
            var demandKw = GetLocalPowerDemand(pair);
            GetElectricityProduction(out _, out var max);
            max = max.ToKw();
            var output = Math.Min(demandKw, energyReserveKw); // Kw
            output = Math.Min(output, max); 

            return calculatedRate * output / 100;
        }
        
        public override void ModifyMaterialBuffer(ushort buildingId, ref Building data, TransferManager.TransferReason material, ref int amountDelta)
        {
            if (m_resourceType == material)
                return;
            base.ModifyMaterialBuffer(buildingId, ref data, material, ref amountDelta);
        }

        public override string GetLocalizedStats(ushort buildingId, ref Building data)
        {
            var myGridData = BackupGrid[buildingId] as GridMemberLastTickStats ?? new GridMemberLastTickStats();
            var str = LocaleFormatter.FormatGeneric("AIINFO_ELECTRICITY_PRODUCTION", (myGridData.ChargeProvidedKw + 500) /1000);
            return  $"Power Input: {(myGridData.ChargeTakenKw + 500) / 1000} MW" + Environment.NewLine 
                + $"Power Loss ({PowerStorage.LossRatio*100}%): {(myGridData.LossKw + 500) / 1000} MW" + Environment.NewLine 
                + $"Battery: {(myGridData.CurrentChargeKw + 500) / 1000}/{myGridData.CapacityKw / 1000} MW" + Environment.NewLine
                + str;
        }

        
        private int GetLocalPowerDemand(BuildingAndIndex pair)
        {
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(pair);
            if (grid == null)
            {
                PowerStorageLogger.Log("Power Storage building is not part of any grid.", PowerStorageMessageType.Grid);
                return 0;
            }

            var capacity = grid.LastCycleTotalCapacityKw;
            var consumption = grid.LastCycleTotalConsumptionKw;

            var storageDraw = BackupGridActiveConsumptionSum(pair);
            var storageProvide = BackupGridActiveContributionSum(pair);

            var demand = Math.Max(0, consumption - storageDraw) - Math.Max(0, capacity - storageProvide);
            PowerStorageLogger.Log($"{grid.CodeName} - capacity:{capacity} | consumption:{consumption} | storage draw:{storageDraw} | storage provide:{ storageProvide } | real demand:{demand}", PowerStorageMessageType.Grid);
            return demand;
        }
        
        private static int BackupGridActiveContributionSum(BuildingAndIndex building)
        {
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(building);
            var i = 0;
            foreach (DictionaryEntry entry in BackupGrid)
            {
                if (entry.Value is GridMemberLastTickStats memberData && memberData.IsActive && grid.BuildingsList.Any(MatchBuildingInList(memberData)))
                {
                    i += memberData.ChargeProvidedKw;
                }
            }
            return i;
        }


        private static int BackupGridActiveConsumptionSum(BuildingAndIndex building)
        {
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(building);
            var i = 0;
            foreach (DictionaryEntry entry in BackupGrid)
            {
                if (entry.Value is GridMemberLastTickStats memberData && !memberData.IsOff && memberData.IsActive && grid.BuildingsList.Any(MatchBuildingInList(memberData)))
                {
                    i += memberData.ChargeTakenKw;
                }
            }
            return i;
        }

        private static int BackupGridActiveCount(BuildingAndIndex building)
        {
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(building);
            var i = 0;
            foreach (DictionaryEntry entry in BackupGrid)
            {
                if (entry.Value is GridMemberLastTickStats memberData && !memberData.IsOff && memberData.IsActive && grid.BuildingsList.Any(MatchBuildingInList(memberData)))
                    i++;
            }
            return i;
        }

        private static int LocalMembers(BuildingAndIndex building)
        {
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(building);
            if (grid == null)
                return 1;

            var i = 0;
            foreach (DictionaryEntry entry in BackupGrid)
            {
                if (entry.Value is GridMemberLastTickStats memberData && grid.BuildingsList.Any(MatchBuildingInList(memberData)))
                    i++;
            }
            return i;
        }

        private static int BackupGridFullCount(BuildingAndIndex building)
        {
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(building);            
            if (grid == null)
                return 1;

            var i = 0;
            foreach (DictionaryEntry entry in BackupGrid)
            {
                if (entry.Value is GridMemberLastTickStats memberData && (memberData.IsFull || memberData.IsOff) && grid.BuildingsList.Any(MatchBuildingInList(memberData)))
                    i++;
            }
            return i;
        }

        private int CalculateMyShare(BuildingAndIndex pair, int demandWithSafetyKw)
        {
            var totalGridPotential = GetTotalGridPotential(pair);
            PowerStorageLogger.Log($"totalGridPotential ({totalGridPotential}) >= demandWithSafetyKw ({demandWithSafetyKw}) ?", PowerStorageMessageType.Discharging);
            if(totalGridPotential < demandWithSafetyKw)
                return int.MaxValue;

            var myGridData = BackupGrid[pair.Index] as GridMemberLastTickStats ?? new GridMemberLastTickStats();
            var evenShareDemandKw = demandWithSafetyKw / Math.Max(1, BackupGridActiveCount(pair));
            var myOffering = Math.Min(myGridData.CurrentChargeKw, myGridData.PotentialOutputKw);
            PowerStorageLogger.Log($"myOffering ({myOffering}) >= evenShareDemandKw ({evenShareDemandKw}) ?", PowerStorageMessageType.Discharging);
            if(myOffering < evenShareDemandKw)
                return int.MaxValue;
            
            int previousEliminated;
            var newEliminated = 0;
            do
            {
                previousEliminated = newEliminated;
                var remainingAfterSlackersKw = GetEvenShareAboveValue(demandWithSafetyKw, evenShareDemandKw, pair, out var participants, out newEliminated);
                evenShareDemandKw = remainingAfterSlackersKw / Math.Max(1, participants);
                
                PowerStorageLogger.Log($"demandWithSafetyKw:{demandWithSafetyKw}, remainingAfterSlackersKw:{remainingAfterSlackersKw}, evenShareDemandKw:{evenShareDemandKw}, participants:{participants}, previousEliminated:{previousEliminated}, newEliminated:{newEliminated}", PowerStorageMessageType.Discharging);
                
                if (myOffering < evenShareDemandKw)
                    return int.MaxValue;

            } while (previousEliminated != newEliminated);

            return evenShareDemandKw;
        }

        private static int GetEvenShareAboveValue(int total, int value, BuildingAndIndex building, out int participants, out int eliminated)
        {
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(building);
            eliminated = 0;
            participants = 0;
            foreach (DictionaryEntry entry in BackupGrid)
            {
                if (entry.Value is GridMemberLastTickStats memberData && !memberData.IsOff && grid.BuildingsList.Any(MatchBuildingInList(memberData)))
                {
                    var contribution = Math.Min(memberData.PotentialOutputKw, memberData.CurrentChargeKw);
                    if (contribution <= value)
                    {
                        total -= contribution;
                        eliminated++;
                    }
                    else
                        participants++;
                }
            }
            
            return total;
        }

        private static int GetTotalGridPotential(BuildingAndIndex building)
        {
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(building);
            var i = 0;
            foreach (DictionaryEntry entry in BackupGrid)
            {
                if (entry.Value is GridMemberLastTickStats memberData && !memberData.IsOff && grid.BuildingsList.Any(MatchBuildingInList(memberData)))
                {
                    i += Math.Min(memberData.PotentialOutputKw, memberData.CurrentChargeKw);
                }
            }
            return i;
        }
        
        private static BuildingAndIndex LowestIdBuilding(BuildingAndIndex building)
        {
            var buffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
            var grid = GridsBuildingsRollup.GetGroupContainingBuilding(building);
            var i = ushort.MaxValue;
            var returnValue = building;
            foreach (DictionaryEntry entry in BackupGrid)
            {
                if (!(entry.Value is GridMemberLastTickStats memberData)) 
                    continue;

                var index = memberData.BuildingPair.Index;
                if (index < i && grid.BuildingsList.Any(MatchBuildingInList(memberData)))
                {
                    i = index;
                    returnValue = memberData.BuildingPair;
                }
            }
            return returnValue;
        }
        public static Func<BuildingAndIndex, bool> MatchBuildingInList(GridMemberLastTickStats memberData)
        {
            return b => b.Building.m_position == memberData.BuildingPair.Building.m_position;
        }

        private static string[] ChirpsAboutPowerLoss => new []
        {
            "@mayor my lights keep flickering, is everything alright?", 
            "@mayor thank you for installing those backup batteries, I've got homework to finish!",
            "My freezer just cut out, anybody want to split 4 pints of ice cream with me?",
            "Will the traffic lights still work if the stored power facilities run out of juice?",
            "Somebody tell the @mayor to invest in more green energy, we keep loosing power!"
        };
    }
}
