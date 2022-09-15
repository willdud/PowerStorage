using System;
using ColossalFramework;
using UnityEngine;

namespace PowerStorage
{
    public class SubStationAi : PowerPlantAI
    {
        public SubStationAi()
        {
        }

        public override void CreateBuilding(ushort buildingId, ref Building data)
        {
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
            Grid.BackupGrid.TryGetValue(buildingId, out var myGridData);
            if(myGridData == null)
            {
                myGridData = new GridMemberLastTickStats(buildingId)
                {
                    CapacityKw = m_resourceCapacity.ToKw(),
                    PotentialOutputKw = m_electricityProduction.ToKw(),
                    PotentialDrawKw = m_resourceConsumption.ToKw()
                };
                Grid.BackupGrid.TryAdd(buildingId, myGridData);
            }                
            
            var energyCapacityKw = myGridData.CapacityKw;
            var energyReserveKw = myGridData.CurrentChargeKw;
            var instance = Singleton<DistrictManager>.instance;
            var district = instance.GetDistrict(buildingData.m_position);
            
            myGridData.ChargeTakenKw = 0;

            GetElectricityProduction(out _, out var max); // Silly
            max = Math.Min(myGridData.PotentialDrawOverrideKw, max.ToKw());
            
            //Charge
            int amountToAddKw = 0;
            if ((buildingData.m_problems & Notification.Problem1.Electricity) != Notification.Problem1.Electricity)
            {
                finalProductionRate = 0;
                myGridData.ChargeProvidedKw = 0;

                if (energyReserveKw < energyCapacityKw)
                {
                    amountToAddKw = max - PowerStorage.SafetyKwIntake;
                    amountToAddKw = (int)(amountToAddKw * (1 - PowerStorage.LossRatio));

                    if ((buildingData.m_flags & (Building.Flags.Evacuating | Building.Flags.Active)) == Building.Flags.Active)
                    {
                        if (myGridData.CurrentChargeKw + amountToAddKw > m_resourceCapacity.ToKw())
                        {
                            amountToAddKw = m_resourceCapacity.ToKw() - myGridData.CurrentChargeKw;
                        }

                        myGridData.ChargeTakenKw = amountToAddKw;
                        myGridData.CurrentChargeKw += amountToAddKw;
                        AddProductionConsumption(ref instance.m_districts.m_buffer[district], amountToAddKw.KwToSilly()); // silly numbers not KW/MW
                        
                        PowerStorageLogger.Log($"[POWER STORAGE] Battery Charging: {myGridData.CurrentChargeKw}/{energyCapacityKw} | added: {amountToAddKw}", PowerStorageMessageType.Charging);
                    }
                }
            }


            //Electricity / Production
            var outputKw = Math.Min(max, energyReserveKw); // Kw
            outputKw = Math.Min(outputKw, myGridData.PotentialOutputOverrideKw);
            if ((buildingData.m_flags & (Building.Flags.Evacuating | Building.Flags.Active)) == Building.Flags.Active)
            {
                finalProductionRate = outputKw / Math.Max(1, max) * 100;
                myGridData.ChargeProvidedKw = outputKw;
                myGridData.CurrentChargeKw -= outputKw;

                AddProductionCapacity(ref instance.m_districts.m_buffer[district], outputKw.KwToSilly());
                PowerStorageLogger.Log($"[POWER STORAGE] Running On Battery: {energyReserveKw}/{energyCapacityKw} | used: {outputKw} | max was: {max}", PowerStorageMessageType.Discharging);
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

        public override int GetElectricityRate(ushort buildingId, ref Building data)
        {
            Grid.BackupGrid.TryGetValue(buildingId, out var myGridData);
            if(myGridData == null)
            {
                myGridData = new GridMemberLastTickStats(buildingId)
                {
                    CapacityKw = m_resourceCapacity.ToKw(),
                    PotentialOutputKw = m_electricityProduction.ToKw(),
                    PotentialDrawKw = m_resourceConsumption.ToKw()
                };
                Grid.BackupGrid.TryAdd(buildingId, myGridData);
            }

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
            
            GetElectricityProduction(out int _, out var max);
            max = max.ToKw();
            var output = Math.Min(max, energyReserveKw); // Kw
            return calculatedRate * output / 100;
        }
        
        public override void ModifyMaterialBuffer(ushort buildingId, ref Building data, TransferManager.TransferReason material, ref int amountDelta)
        {
            if (m_resourceType == material)
                return;
            else
                base.ModifyMaterialBuffer(buildingId, ref data, material, ref amountDelta);
        }

        public override string GetLocalizedStats(ushort buildingId, ref Building data)
        {
            Grid.BackupGrid.TryGetValue(buildingId, out var myGridData);
            
            if(myGridData == null)
            {
                myGridData = new GridMemberLastTickStats(buildingId)
                {
                    CapacityKw = m_resourceCapacity.ToKw(),
                    PotentialOutputKw = m_electricityProduction.ToKw(),
                    PotentialDrawKw = m_resourceConsumption.ToKw()
                };
                Grid.BackupGrid.TryAdd(buildingId, myGridData);
            }

            var str = LocaleFormatter.FormatGeneric("AIINFO_ELECTRICITY_PRODUCTION", (myGridData.ChargeProvidedKw + 500) /1000);
            return  $"Power Input: {(myGridData.ChargeTakenKw + 500) / 1000} MW" + Environment.NewLine 
                + $"Power Loss ({PowerStorage.LossRatio*100}%): {(myGridData.LossKw + 500) / 1000} MW" + Environment.NewLine 
                + $"Capacity: {(myGridData.CurrentChargeKw + 500) / 1000}/{myGridData.CapacityKw / 1000} MW" + Environment.NewLine 
                + str;
        }
    }
}
