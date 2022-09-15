using System;
using System.Linq;
using ColossalFramework;
using HarmonyLib;
using UnityEngine;

namespace PowerStorage
{
    public class PowerStorageAi : PowerPlantAI
    {
        public DateTime LastAnnoyance { get; set; }
        
        private bool _chargingChirpFlag;

        public PowerStorageAi()
        {
            LastAnnoyance = Singleton<SimulationManager>.instance.m_currentGameTime;
        }

        public override void CreateBuilding(ushort buildingId, ref Building data)
        {
            PowerStorageLogger.Log($"CREATE BUILDING OCCURED!!!!", PowerStorageMessageType.Charging);

            base.CreateBuilding(buildingId, ref data);
            m_resourceType = TransferManager.TransferReason.None;
            Grid.BackupGrid.TryAdd(buildingId, new GridMemberLastTickStats(buildingId)
            {
                CapacityKw = m_resourceCapacity.ToKw(),
                PotentialOutputKw = m_electricityProduction.ToKw(),
                PotentialDrawKw = m_resourceConsumption.ToKw()
            });
        }

        public override void ReleaseBuilding(ushort buildingId, ref Building data)
        {
            Grid.BackupGrid.TryRemove(buildingId);
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
            var myGridData = GetOrCreateGridData(buildingId);

            var lastOutputKw = myGridData.ChargeProvidedKw;

            GetElectricityProduction(out _, out var max); // Silly
            max = max.ToKw();

            var demandKw = myGridData.IsDisconnectedMode 
                ? max * -1                  // always draw as much as you can.
                : GetGlobalPowerDemand();   // draw your share
            
            // reset
            myGridData.ChargeTakenKw = 0;
            myGridData.ChargeProvidedKw = 0;

            int amountToAddKw = 0;
            int outputKw = 0;
            
            finalProductionRate = DrawElectricityFromGridIntoStorage(buildingData, demandKw, myGridData, ref amountToAddKw);
            
            if(myGridData.IsDisconnectedMode)
            {
                var localExcessKw = myGridData.IsDisconnectedMode ? TestLocalExcessPowerKw(buildingData, max, myGridData) : 0;
                demandKw = localExcessKw == 0 ? max 
                    : localExcessKw < lastOutputKw * 1.1 ? max : 0;
            }

            outputKw = PutElectricityIntoGrid(buildingId, buildingData, ref finalProductionRate, demandKw, outputKw, myGridData, max);

            ProduceByproducts(buildingData, ref finalProductionRate);

            buildingData.m_problems = myGridData.CurrentChargeKw < 1 
                ? Notification.AddProblems(buildingData.m_problems, Notification.Problem1.NoFuel) 
                : Notification.RemoveProblems(buildingData.m_problems, Notification.Problem1.NoFuel);

            //Death
            HandleDead(buildingId, ref buildingData, ref behaviour, totalWorkerCount);
            frameData.m_productionState += (byte) finalProductionRate;
            
            if ((finalProductionRate != 0 || outputKw > 0 || amountToAddKw > 0) && (buildingData.m_problems & Notification.Problem1.TurnedOff) !=  Notification.Problem1.TurnedOff)
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

        private int PutElectricityIntoGrid(ushort buildingId, Building buildingData, ref int finalProductionRate, int demandKw, int outputKw, GridMemberLastTickStats myGridData, int max)
        {
            if (demandKw > 0)
            {
                ChirpAboutThePowerIssues(buildingId);

                //Electricity / Production
                var demandWithSafetyKw = demandKw + PowerStorage.SafetyKwDischarge;
                var portionOfDemandKw = CalculateMyShare(buildingId, demandWithSafetyKw);
                PowerStorageLogger.Log("Portion of demand:" + portionOfDemandKw, PowerStorageMessageType.Discharging);
                outputKw = Math.Min(portionOfDemandKw, myGridData.CurrentChargeKw); // Kw
                outputKw = Math.Min(outputKw, max);
                outputKw = Math.Min(outputKw, myGridData.PotentialOutputOverrideKw);

                if ((buildingData.m_flags & (Building.Flags.Evacuating | Building.Flags.Active)) == Building.Flags.Active)
                {
                    finalProductionRate = outputKw / Math.Max(1, max) * 100;
                    myGridData.ChargeProvidedKw = outputKw;
                    myGridData.CurrentChargeKw -= outputKw;
                    
                    var instance = Singleton<DistrictManager>.instance;
                    var district = instance.GetDistrict(buildingData.m_position);
                    AddProductionCapacity(ref instance.m_districts.m_buffer[district], outputKw.KwToSilly());
                    PowerStorageLogger.Log($"[POWER STORAGE] Running On Battery: {myGridData.CurrentChargeKw}/{myGridData.CapacityKw} | used: {outputKw} | max was: {max}", PowerStorageMessageType.Discharging);
                    Singleton<ElectricityManager>.instance.TryDumpElectricity(buildingData.m_position, outputKw.KwToSilly(), outputKw.KwToSilly());
                }
            }

            return outputKw;
        }

        private int DrawElectricityFromGridIntoStorage(Building buildingData, int demandKw, GridMemberLastTickStats myGridData,  ref int amountToAddKw)
        {
            int finalProductionRate = 0;
            if (demandKw <= 0 && (buildingData.m_problems & Notification.Problem1.Electricity) != Notification.Problem1.Electricity)
            {
                _chargingChirpFlag = true;
                
                finalProductionRate = 0;
                if (myGridData.CurrentChargeKw < myGridData.CapacityKw)
                {
                    var excess = Math.Max(0, ((demandKw * -1) - PowerStorage.SafetyKwIntake));
                    var portionOfExcess = excess / Math.Max(1, Grid.BackupGrid.Map.Count - BackupGridFullCount());
                    amountToAddKw = Math.Min(myGridData.PotentialDrawOverrideKw, portionOfExcess);

                    if ((buildingData.m_flags & (Building.Flags.Evacuating | Building.Flags.Active)) == Building.Flags.Active)
                    {
                        if (myGridData.CurrentChargeKw + amountToAddKw > m_resourceCapacity.ToKw())
                        {
                            amountToAddKw = m_resourceCapacity.ToKw() - myGridData.CurrentChargeKw;
                        }

                        var instance = Singleton<DistrictManager>.instance;
                        var district = instance.GetDistrict(buildingData.m_position);
                        
                        var amountToSubtractKw = amountToAddKw;
                        var canDraw = Singleton<ElectricityManager>.instance.TryFetchElectricity(buildingData.m_position, amountToSubtractKw.KwToSilly(), amountToSubtractKw.KwToSilly());
                        amountToAddKw = canDraw.ToKw();
                        myGridData.ChargeTakenKw = amountToSubtractKw;
                        myGridData.CurrentChargeKw += (int)(amountToAddKw * (1 - PowerStorage.LossRatio));
                        
                        AddProductionConsumption(ref instance.m_districts.m_buffer[district], amountToSubtractKw.KwToSilly()); // silly numbers not KW/MW

                        PowerStorageLogger.Log($"[POWER STORAGE] Battery Charging: {myGridData.CurrentChargeKw}/{myGridData.CapacityKw} | added: {amountToAddKw}", PowerStorageMessageType.Charging);
                    }
                }
            }

            return finalProductionRate;
        }

        private static int TestLocalExcessPowerKw(Building buildingData, int maxKw, GridMemberLastTickStats myGridData)
        {
            int excessKw;
            var maxSilly = maxKw.KwToSilly();
            if ((buildingData.m_problems & Notification.Problem1.Electricity) == Notification.Problem1.Electricity)
            {
                excessKw = 0; // full output
            }
            else
            {
                //var canDraw = Singleton<ElectricityManager>.instance.TryFetchElectricity(buildingData.m_position, maxSilly, maxSilly); // test what we can draw
                //Singleton<ElectricityManager>.instance.TryDumpElectricity(buildingData.m_position, canDraw, canDraw); // quick put it back, that was just a test!

                //excessKw = canDraw.ToKw();
                //PowerStorageLogger.LogWarning("Can Draw: " + excessKw + "Kw", PowerStorageMessageType.Grid);
                
                var groups = Traverse.Create(Singleton<ElectricityManager>.instance).Field("m_pulseGroups").GetValue() as ElectricityManager.PulseGroup[];
                var units = Traverse.Create(Singleton<ElectricityManager>.instance).Field("m_pulseUnits").GetValue() as ElectricityManager.PulseUnit[];
                var cells = Traverse.Create(Singleton<ElectricityManager>.instance).Field("m_electricityGrid").GetValue() as ElectricityManager.Cell[];
                
                var num = Mathf.Clamp((int) (buildingData.m_position.x / 38.25 + 128.0), 0, byte.MaxValue);
                var index = Mathf.Clamp((int) (buildingData.m_position.z / 38.25 + 128.0), 0, byte.MaxValue) * 256 + num;
                var myCell = cells[index];

                GetElectricityGridPosition(buildingData.m_position, out var x, out var z);
                var myGroup = myCell.m_pulseGroup != ushort.MaxValue
                    ? groups[myCell.m_pulseGroup]
                    : GetPulseGroupFromUnit(groups, units, x, z);
                
                PowerStorageLogger.LogWarning("My Group: " + myGroup.m_origCharge + " Silly units. Merge index:" + myGroup.m_mergeIndex , PowerStorageMessageType.Grid);

                var productionOnThisGrid = SumMergedGroups(myGroup, groups);
                excessKw = Convert.ToInt32(productionOnThisGrid).ToKw();

                PowerStorageLogger.LogWarning("Can Draw: " + excessKw + "Kw." , PowerStorageMessageType.Grid);
            }

            return excessKw;
        }

        private void ChirpAboutThePowerIssues(ushort buildingId)
        {
            if (PowerStorage.Chirp && _chargingChirpFlag && buildingId == LowestBuildingId()) // only one message, so only one building sender
            {
                var theTime = Singleton<SimulationManager>.instance.m_currentGameTime;
                if (LastAnnoyance.AddDays(7) < theTime) // rate limit
                {
                    _chargingChirpFlag = false;
                    LastAnnoyance = theTime;
                    var cp = Singleton<MessageManager>.instance;
                    cp.QueueMessage(new CustomCitizenMessage(
                        Singleton<MessageManager>.instance.GetRandomResidentID(),
                        ChirpsAboutPowerLoss[theTime.Ticks % ChirpsAboutPowerLoss.Length],
                        null)
                    );
                }
            }
        }

        public override int GetElectricityRate(ushort buildingId, ref Building data)
        {
            var myGridData = GetOrCreateGridData(buildingId);

            var energyReserveKw = myGridData.CurrentChargeKw;
            var productionRate = (int) data.m_productionRate;
            int calculatedRate;
            if ((data.m_flags & (Building.Flags.Evacuating | Building.Flags.Active)) == Building.Flags.Active)
            {
                var budget = Singleton<EconomyManager>.instance.GetBudget(m_info.m_class);
                calculatedRate = PlayerBuildingAI.GetProductionRate(productionRate, budget);
                calculatedRate = Mathf.Min(calculatedRate, energyReserveKw / myGridData.CapacityKw * 100);
            }
            else
                calculatedRate = 0;

            var demandKw = GetGlobalPowerDemand();
            GetElectricityProduction(out int _, out var max);
            max = max.ToKw();
            var output = Math.Min(demandKw, energyReserveKw); // Kw
            output = Math.Min(output, max); 

            return calculatedRate * output / 100;
        }

        private GridMemberLastTickStats GetOrCreateGridData(ushort buildingId)
        {
            Grid.BackupGrid.TryGetValue(buildingId, out var myGridData);
            if (myGridData == null)
            {
                myGridData = new GridMemberLastTickStats(buildingId)
                {
                    CapacityKw = m_resourceCapacity.ToKw(),
                    PotentialOutputKw = m_electricityProduction.ToKw(),
                    PotentialDrawKw = m_resourceConsumption.ToKw()
                };
                Grid.BackupGrid.TryAdd(buildingId, myGridData);
            }

            return myGridData;
        }

        public override void ModifyMaterialBuffer(ushort buildingId, ref Building data, TransferManager.TransferReason material, ref int amountDelta)
        {
            if (m_resourceType == material) // don't let the game reduce the 'resource' as it would a coal plant.
                return;
            else
                base.ModifyMaterialBuffer(buildingId, ref data, material, ref amountDelta);
        }

        public override string GetLocalizedStats(ushort buildingId, ref Building data)
        {
            var myGridData = GetOrCreateGridData(buildingId);

            var str = LocaleFormatter.FormatGeneric("AIINFO_ELECTRICITY_PRODUCTION", (myGridData.ChargeProvidedKw + 500) /1000);
            return  $"Power Input: {(myGridData.ChargeTakenKw + 500) / 1000} MW" + Environment.NewLine 
                + $"Power Loss ({PowerStorage.LossRatio*100}%): {(myGridData.LossKw + 500) / 1000} MW" + Environment.NewLine 
                + $"Battery: {(myGridData.CurrentChargeKw + 500) / 1000}/{myGridData.CapacityKw / 1000} MW" + Environment.NewLine 
                + str;
        }

        private void ProduceByproducts(Building buildingData, ref int finalProductionRate)
        {
            if ((buildingData.m_flags & (Building.Flags.Evacuating | Building.Flags.Active)) == Building.Flags.Active)
            {
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
        
        private static int GetGlobalPowerDemand()
        {
            var districtManager = Singleton<DistrictManager>.instance;
            var allCityDistrict = districtManager.m_districts.m_buffer[0];

            var capacity = allCityDistrict.GetElectricityCapacity();
            var consumption = allCityDistrict.GetElectricityConsumption();

            var backgroundConsumption = BackupGridActiveConsumptionSum();
            var backgroundContribution = BackupGridActiveContributionSum();

            var demand = Math.Max(0, consumption - backgroundConsumption) - Math.Max(0, capacity - backgroundContribution);
            PowerStorageLogger.Log($"[POWER STORAGE] capacity:{capacity} | consumption:{consumption} | backgroundConsumption:{backgroundConsumption} | lastOutputOfAll:{ backgroundContribution } | demand:{demand}", PowerStorageMessageType.Grid);
            return demand;
        }
        
        private static int BackupGridActiveContributionSum()
        {
            var i = 0;
            foreach (var entry in Grid.BackupGrid.Map)
            {
                if (entry.Value is GridMemberLastTickStats memberData && memberData.IsActive)
                {
                    i += memberData.ChargeProvidedKw;
                }
            }
            return i;
        }

        private static int BackupGridActiveConsumptionSum()
        {
            var i = 0;
            foreach (var entry in Grid.BackupGrid.Map)
            {
                if (entry.Value is GridMemberLastTickStats memberData && !memberData.IsOff && memberData.IsActive)
                {
                    i += memberData.ChargeTakenKw;
                }
            }
            return i;
        }

        private static int BackupGridActiveCount()
        {
            var i = 0;
            foreach (var entry in Grid.BackupGrid.Map)
            {
                if (entry.Value is GridMemberLastTickStats memberData && !memberData.IsOff && memberData.IsActive)
                    i++;
            }
            return i;
        }

        private static int BackupGridFullCount()
        {
            var i = 0;
            foreach (var entry in Grid.BackupGrid.Map)
            {
                if (entry.Value is GridMemberLastTickStats memberData && (memberData.IsFull || memberData.IsOff))
                    i++;
            }
            return i;
        }

        private int CalculateMyShare(ushort buildingId, int demandWithSafetyKw)
        {
            var totalGridPotential = GetTotalGridPotential();
            if(totalGridPotential < demandWithSafetyKw)
                return int.MaxValue;

            var myGridData = GetOrCreateGridData(buildingId);

            var evenShareDemandKw = demandWithSafetyKw / Math.Max(1, BackupGridActiveCount());
            var myOffering = Math.Min(myGridData.CurrentChargeKw, myGridData.PotentialOutputKw);
            if(myOffering < evenShareDemandKw)
                return int.MaxValue;

            int previousEliminated;
            var newEliminated = 0;
            do
            {
                previousEliminated = newEliminated;
                var remainingAfterSlackersKw = GetEvenShareAboveValue(demandWithSafetyKw, evenShareDemandKw, out int participants, out newEliminated);
                evenShareDemandKw = remainingAfterSlackersKw / Math.Max(1, participants);
                
                PowerStorageLogger.Log($"demandWithSafetyKw:{demandWithSafetyKw}, remainingAfterSlackersKw:{remainingAfterSlackersKw}, evenShareDemandKw:{evenShareDemandKw}, participants:{participants}, previousEliminated:{previousEliminated}, newEliminated:{newEliminated}", PowerStorageMessageType.Grid);
                
                if (myOffering < evenShareDemandKw)
                    return int.MaxValue;

            } while (previousEliminated != newEliminated);

            return evenShareDemandKw;
        }

        private static int GetEvenShareAboveValue(int total, int value, out int participants, out int eliminated)
        {
            eliminated = 0;
            participants = 0;
            foreach (var entry in Grid.BackupGrid.Map)
            {
                if (entry.Value is GridMemberLastTickStats memberData && !memberData.IsOff)
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

        private static int GetTotalGridPotential()
        {
            var i = 0;
            foreach (var entry in Grid.BackupGrid.Map)
            {
                if (entry.Value is GridMemberLastTickStats memberData && !memberData.IsOff)
                {
                    i += Math.Min(memberData.PotentialOutputKw, memberData.CurrentChargeKw);
                }
            }
            return i;
        }
        
        private static ushort LowestBuildingId()
        {
            var i = ushort.MaxValue;
            foreach (var entry in Grid.BackupGrid.Map)
            {
                if (entry.Value is GridMemberLastTickStats memberData && memberData.BuildingId < i)
                {
                    i = memberData.BuildingId;
                }
            }
            return i;
        }
        
        private static string[] ChirpsAboutPowerLoss => new []
        {
            "@mayor my lights keep flickering, is everything alright?", 
            "@mayor thank you for installing those backup batteries, I've got homework to finish!",
            "My freezer just cut out, anybody want to split 4 pints of ice cream with me?",
            "Will the traffic lights still work if the stored power facilities run out of juice?",
            "Somebody tell the @mayor to invest in more green energy, we keep loosing power!"
        };

        private static void GetElectricityGridPosition(Vector3 pos, out int x, out int z)
        {
            x = Mathf.Clamp((int) ((double) pos.x / 38.25 + 128.0), 0, (int) byte.MaxValue);
            z = Mathf.Clamp((int) ((double) pos.z / 38.25 + 128.0), 0, (int) byte.MaxValue);
        }
        
        private static ElectricityManager.PulseGroup GetPulseGroupFromUnit(ElectricityManager.PulseGroup[] groups, ElectricityManager.PulseUnit[] units, int x, int z)
        {
            var unit = units.First(u => u.m_x == x && u.m_z == z);
            return groups[unit.m_group];
        }
        
        private static long SumMergedGroups(ElectricityManager.PulseGroup myGroup, ElectricityManager.PulseGroup[] groups)
        {
            return groups.Where(g => g.m_mergeIndex == myGroup.m_mergeIndex).Sum(g => g.m_origCharge);
        }
    }
}
