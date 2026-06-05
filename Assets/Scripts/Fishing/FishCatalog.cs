using System.Collections.Generic;
using UnityEngine;

namespace TurnOnTheBass
{
    public sealed class FishCatalog
    {
        private readonly Dictionary<WaterBodyType, List<FishDefinition>> fishByWaterType =
            new Dictionary<WaterBodyType, List<FishDefinition>>();

        public FishCatalog(IEnumerable<FishDefinition> definitions)
        {
            fishByWaterType[WaterBodyType.Ocean] = new List<FishDefinition>();
            fishByWaterType[WaterBodyType.Lake] = new List<FishDefinition>();
            fishByWaterType[WaterBodyType.River] = new List<FishDefinition>();

            foreach (FishDefinition fish in definitions)
            {
                fishByWaterType[fish.Habitat].Add(fish);
            }
        }

        public FishDefinition GetRandomFish(WaterBodyType waterType)
        {
            List<FishDefinition> pool = fishByWaterType[waterType];
            if (pool.Count == 0)
            {
                return null;
            }

            return pool[Random.Range(0, pool.Count)];
        }

        public static FishCatalog CreateDefault()
        {
            return new FishCatalog(new[]
            {
                new FishDefinition("ocean_tuna", "Bluefin Tuna", WaterBodyType.Ocean, 35f),
                new FishDefinition("ocean_marlin", "Striped Marlin", WaterBodyType.Ocean, 48f),
                new FishDefinition("ocean_mahi", "Mahi-Mahi", WaterBodyType.Ocean, 14f),

                new FishDefinition("lake_bass", "Largemouth Bass", WaterBodyType.Lake, 4.2f),
                new FishDefinition("lake_pike", "Northern Pike", WaterBodyType.Lake, 9.5f),
                new FishDefinition("lake_trout", "Lake Trout", WaterBodyType.Lake, 6.1f),

                new FishDefinition("river_salmon", "Chinook Salmon", WaterBodyType.River, 12f),
                new FishDefinition("river_catfish", "Channel Catfish", WaterBodyType.River, 8.8f),
                new FishDefinition("river_carp", "Common Carp", WaterBodyType.River, 5.6f)
            });
        }
    }
}
