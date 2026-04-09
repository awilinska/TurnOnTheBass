using UnityEngine;

namespace TurnOnTheBass
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class WaterZone : MonoBehaviour
    {
        [SerializeField] private WaterBodyType waterType = WaterBodyType.Lake;
        [SerializeField] private string zoneName = "Lake";

        public WaterBodyType WaterType => waterType;

        public string ZoneName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(zoneName))
                {
                    return zoneName;
                }

                return waterType.ToString();
            }
        }

        public void Configure(WaterBodyType type, string readableName)
        {
            waterType = type;
            zoneName = readableName;
        }

        private void Reset()
        {
            Collider2D area = GetComponent<Collider2D>();
            area.isTrigger = true;
        }
    }
}
