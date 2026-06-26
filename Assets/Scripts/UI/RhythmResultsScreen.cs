using TMPro;
using UnityEngine;

namespace TurnOnTheBass.UI
{
    [DisallowMultipleComponent]
    public sealed class RhythmResultsScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI accuracyText;
        [SerializeField] private TextMeshProUGUI maxComboText;
        [SerializeField] private string accuracyFormat = "ACCURACY: {0}%";
        [SerializeField] private string maxComboFormat = "MAX COMBO: {0}";

        public void SetResults(float accuracy, int maxCombo)
        {
            if (accuracyText != null)
            {
                accuracyText.text = string.Format(accuracyFormat, (accuracy * 100f).ToString("0"));
            }

            if (maxComboText != null)
            {
                maxComboText.text = string.Format(maxComboFormat, maxCombo);
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(accuracyFormat))
            {
                accuracyFormat = "ACCURACY: {0}%";
            }

            if (string.IsNullOrEmpty(maxComboFormat))
            {
                maxComboFormat = "MAX COMBO: {0}";
            }
        }
    }
}
