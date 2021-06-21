using System;
using System.Collections.Generic;

namespace PowerStorage
{ 
    public class BuildingElectricityGroup
    {
        private static readonly Random Rnd = new Random();
        private static readonly string[] CodeWords = { "Alfa","Bravo","Charlie","Delta","Echo","Foxtrot","Golf","Hotel","India","Juliett","Kilo","Lima","Mike","November","Oscar","Papa","Quebec","Romeo","Sierra","Tango","Uniform","Victor","Whiskey","X-ray","Yankee","Zulu" };
        public BuildingElectricityGroup()
        {
            CodeName =  $"{CodeWords[Rnd.Next(0, CodeWords.Length)]}-{CodeWords[Rnd.Next(0, CodeWords.Length)]}";
            BuildingsList = new List<BuildingAndIndex>();
        }

        public string CodeName;

        public DateTime LastBuildingUpdate;
        public int LastCycleTotalCapacityKw;
        public int LastCycleTotalConsumptionKw;
        public int CapacityKw;
        public int ConsumptionKw;
        public List<BuildingAndIndex> BuildingsList;
    }
}
