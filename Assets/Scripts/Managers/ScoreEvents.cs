using System;

public static class ScoreEvents
{
    public static event Action<int> OnScoreChanged;

    public static void RaiseScoreChanged(int newScore)
    {
        OnScoreChanged?.Invoke(newScore);
    }
}
