using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelButtonUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text levelNameText;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Button button;

    [Header("Stars")]
    [SerializeField] private Image[] starIcons;
    [SerializeField] private Sprite starFilled;
    [SerializeField] private Sprite starEmpty;

    private int levelIndex;

    public void Setup(int index, string name, bool unlocked, int stars)
    {
        levelIndex = index;

        if (levelNameText != null)
            levelNameText.text = name;

        if (lockIcon != null)
            lockIcon.SetActive(!unlocked);

        if (button != null)
            button.interactable = unlocked;

        UpdateStars(stars);
        button.onClick.AddListener(() => MainMenuController.Instance.OnLevelButtonPressed(levelIndex));
    }

    private void UpdateStars(int stars)
    {
        for (int i = 0; i < starIcons.Length; i++)
            starIcons[i].sprite = (i < stars) ? starFilled : starEmpty;
    }
}
