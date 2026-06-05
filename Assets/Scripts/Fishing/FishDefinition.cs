using System;
using UnityEngine;

namespace TurnOnTheBass
{
    [Serializable]
    public sealed class FishDefinition
    {
        [SerializeField] private string fishId = "fish";
        [SerializeField] private string displayName = "Unknown Fish";
        [SerializeField] private WaterBodyType habitat = WaterBodyType.Lake;
        [SerializeField, Min(0.1f)] private float baseSizeKg = 1f;

        public string FishId => fishId;
        public string DisplayName => displayName;
        public WaterBodyType Habitat => habitat;
        public float BaseSizeKg => baseSizeKg;

        public FishDefinition(
            string fishId,
            string displayName,
            WaterBodyType habitat,
            float baseSizeKg)
        {
            this.fishId = fishId;
            this.displayName = displayName;
            this.habitat = habitat;
            this.baseSizeKg = Mathf.Max(0.1f, baseSizeKg);
        }
    }
}
