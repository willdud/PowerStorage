using System;

namespace PowerStorage
{
    [Serializable]
    public class GridMemberLastTickStats
    {
        public ushort BuildingId { get; set; }
        public bool IsOff { get; set; }
        public bool IsActive => ChargeProvidedKw > 0 || ChargeTakenKw > 0;
        public bool IsFull => CurrentChargeKw >= CapacityKw;

        public int ChargeTakenKw { get; set; }
        public int LossKw => (int)(ChargeTakenKw * PowerStorage.LossRatio);
        public int ChargeProvidedKw { get; set; }
        public int CurrentChargeKw { get; set; }
        public int CapacityKw { get; set; }
        public int PotentialOutputKw { get; set; }
    }
}
