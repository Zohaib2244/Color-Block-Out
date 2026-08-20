using UnityEngine;
using Voodoo.Utils;

public static class GameConstants
{
    private static int currentLevelIndex = 0;
    public static bool inputEnabled = true;

    public static int highestUnlockedLevelIndex
    {
        get { return PlayerPrefs.GetInt("HighestUnlockedLevelIndex", 0); }
        private set { PlayerPrefs.SetInt("HighestUnlockedLevelIndex", value); }
    }

    public static int CurrentLevelIndex
    {
        get { return currentLevelIndex; }
        set
        {
            currentLevelIndex = value;
            int totalLevels = GameManager.Instance != null ? GameManager.Instance.TotalLevels : 0;
            if (value > highestUnlockedLevelIndex && value < totalLevels) highestUnlockedLevelIndex = value;
        }
    }

    public static void InitializeGame()
    {
        Application.targetFrameRate = 60;
        Settings.OnVibrationSettingChanged.AddListener((value) => Vibrations.canVibrate = value);
        GameUIManager.Instance.ShowScreen(ScreenType.Loading, 1);
    }
}
