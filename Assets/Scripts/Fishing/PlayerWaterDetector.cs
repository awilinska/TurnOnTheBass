using System.Collections.Generic;
using UnityEngine;

namespace TurnOnTheBass
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerWaterDetector : MonoBehaviour
    {
        private readonly List<WaterZone> overlappingZones = new List<WaterZone>();

        public WaterZone CurrentZone { get; private set; }

        public bool IsNearWater => CurrentZone != null;

        private void OnTriggerEnter2D(Collider2D other)
        {
            WaterZone zone = other.GetComponent<WaterZone>();
            if (zone == null || overlappingZones.Contains(zone))
            {
                return;
            }

            overlappingZones.Add(zone);
            RefreshCurrentZone();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            WaterZone zone = other.GetComponent<WaterZone>();
            if (zone == null)
            {
                return;
            }

            overlappingZones.Remove(zone);
            RefreshCurrentZone();
        }

        private void LateUpdate()
        {
            RefreshCurrentZone();
        }

        private void RefreshCurrentZone()
        {
            overlappingZones.RemoveAll(zone => zone == null);

            if (overlappingZones.Count == 0)
            {
                CurrentZone = null;
                return;
            }

            Vector3 playerPosition = transform.position;
            float bestDistance = float.MaxValue;
            WaterZone bestZone = null;

            for (int index = 0; index < overlappingZones.Count; index++)
            {
                WaterZone zone = overlappingZones[index];
                float sqrDistance = (zone.transform.position - playerPosition).sqrMagnitude;
                if (sqrDistance >= bestDistance)
                {
                    continue;
                }

                bestDistance = sqrDistance;
                bestZone = zone;
            }

            CurrentZone = bestZone;
        }
    }
}
