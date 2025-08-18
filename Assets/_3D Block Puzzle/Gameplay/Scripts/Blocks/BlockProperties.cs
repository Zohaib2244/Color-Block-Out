using UnityEngine;

public static class BlockProperties
{
    public static float bumpForce = 2f;
    public static float bumpTiltAmount = 25f;
    public static float bumpTiltDuration = 0.5f;
    public static float bumpDetectDirectionThreshold = 0.1f;
    public static float GetBlockRandomStartYValue()
    {
        // Generate a random start Y value for blocks
        return Random.Range(0.5f, 1.5f);
    }
    public static float GetBlockStartRandomAnimationDuration()
    {
        // Generate a random duration for block start animation
        return Random.Range(0.5f, 1f);
    }
}