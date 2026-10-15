using TMPro;
using UnityEngine;

namespace SDFcl.GamePlay.QuestSystem
{
    // One row in the quest log. Make a prefab with this component and two TMP texts.
    public class QuestLogEntryUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("Optional. Faded when the entry is upcoming / finished.")]
        [SerializeField] private CanvasGroup canvasGroup;

        public void Set(string title, string body, bool dimmed)
        {
            if (titleText != null) titleText.text = title;
            if (bodyText != null)
            {
                bodyText.text = body;
                bodyText.gameObject.SetActive(!string.IsNullOrEmpty(body));
            }
            if (canvasGroup != null) canvasGroup.alpha = dimmed ? 0.5f : 1f;
        }
    }
}
