using System;
using UnityEngine.UI;

namespace Baseball.Presentation.Career
{
    /// <summary>투구 도착 뒤 계산된 제구와 타자 반응 문구만 표시한다.</summary>
    public sealed class PitchResultFeedbackPresenter
    {
        private readonly Text _commandText;
        private readonly Text _resultText;

        public PitchResultFeedbackPresenter(Text commandText, Text resultText)
        {
            _commandText = commandText != null
                ? commandText
                : throw new ArgumentNullException(nameof(commandText));
            _resultText = resultText != null
                ? resultText
                : throw new ArgumentNullException(nameof(resultText));
            Hide();
        }

        public void Show(string commandFeedback, string playFeedback)
        {
            _commandText.text = commandFeedback ?? string.Empty;
            _resultText.text = playFeedback ?? string.Empty;
            _commandText.gameObject.SetActive(true);
            _resultText.gameObject.SetActive(true);
        }

        public void Hide()
        {
            _commandText.gameObject.SetActive(false);
            _resultText.gameObject.SetActive(false);
        }
    }
}
