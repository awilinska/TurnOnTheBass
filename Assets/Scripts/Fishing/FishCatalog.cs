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
                new FishDefinition("ocean_tuna", "Bluefin Tuna", WaterBodyType.Ocean, 35f, 0.58f, 126f, 32, "ocean_tuna"),
                new FishDefinition("ocean_marlin", "Striped Marlin", WaterBodyType.Ocean, 48f, 0.72f, 146f, 34, "ocean_marlin", 0.05f),
                new FishDefinition("ocean_mahi", "Mahi-Mahi", WaterBodyType.Ocean, 14f, 0.42f, 118f, 28, "ocean_mahi"),

                new FishDefinition("lake_bass", "Largemouth Bass", WaterBodyType.Lake, 4.2f, 0.35f, 110f, 24, "lake_bass"),
                new FishDefinition("lake_pike", "Northern Pike", WaterBodyType.Lake, 9.5f, 0.52f, 122f, 30, "lake_pike"),
                new FishDefinition("lake_trout", "Lake Trout", WaterBodyType.Lake, 6.1f, 0.48f, 116f, 28, "lake_trout"),

                new FishDefinition("river_salmon", "Chinook Salmon", WaterBodyType.River, 12f, 0.62f, 132f, 32, "river_salmon", 0.03f),
                new FishDefinition("river_catfish", "Channel Catfish", WaterBodyType.River, 8.8f, 0.45f, 112f, 26, "river_catfish"),
                new FishDefinition("river_carp", "Common Carp", WaterBodyType.River, 5.6f, 0.4f, 108f, 24, "river_carp")
            });
        }
    }
}
