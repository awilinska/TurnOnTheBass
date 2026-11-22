using TMPro;
using UnityEngine;

namespace TurnOnTheBass.UI
{
    [DisallowMultipleComponent]
    public sealed class RhythmResultsScreen : MonoBehaviour
    {
        private const float DismissDelaySeconds = 1.5f;

        [SerializeField] private TextMeshProUGUI accuracyText;
        [SerializeField] private TextMeshProUGUI maxComboText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI highScoreText;
        [SerializeField] private GameObject newHighScore;
        [SerializeField] private string accuracyFormat = "ACCURACY: {0}%";
        [SerializeField] private string maxComboFormat = "MAX COMBO: {0}";

        private double dismissAvailableAt;

        public bool CanDismiss => Time.unscaledTimeAsDouble >= dismissAvailableAt;

        private void OnEnable()
        {
            dismissAvailableAt = Time.unscaledTimeAsDouble + DismissDelaySeconds;
            if (TryGetComponent(out Animator animator) && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                // Apply the entrance pose before the first visible frame.
                animator.Update(0f);
            }
        }

        public void SetResults(float accuracy, int maxCombo, int score, int highScore, bool isNewHighScore)
        {
            if (accuracyText != null)
            {
                accuracyText.text = string.Format(accuracyFormat, (accuracy * 100f).ToString("0"));
            }

            if (maxComboText != null)
            {
                maxComboText.text = string.Format(maxComboFormat, maxCombo);
            }

            if (scoreText != null)
            {
                scoreText.text = $"SCORE: {score}";
            }

            if (highScoreText != null)
            {
                highScoreText.text = $"HIGHSCORE: {highScore}";
            }

            if (newHighScore != null)
            {
                newHighScore.SetActive(isNewHighScore);
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
