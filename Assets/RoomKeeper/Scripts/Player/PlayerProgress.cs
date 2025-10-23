using TMPro;
using UnityEngine;

public class PlayerProgress : MonoBehaviour
{
    public static PlayerProgress Instance;

    public int level = 1;
    public int exp = 0;
    public int expToNext = 100;

    public TMP_Text expText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        if (expText)
            expText.text = $"Level: {level}\n" +
                $"EXP: {exp}/{expToNext}";
    }

    public void AddEXP(int amount)
    {
        exp += amount;
        if (expText)
            expText.text = $"Level: {level}\n" +
                $"EXP: {exp}/{expToNext}";

        if (exp >= expToNext)
            LevelUp();
    }

    private void LevelUp()
    {
        level++;
        exp -= expToNext;
        expToNext = Mathf.RoundToInt(expToNext * 1.2f);
        if (expText)
            expText.text = $"Level: {level}\n" +
                $"EXP: {exp}/{expToNext}";
    }

}
