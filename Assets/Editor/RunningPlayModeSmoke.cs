#if UNITY_EDITOR
/// <summary>Stable automation entry point, upgraded to the continuous-running
/// regression instead of the retired stop-and-clear arena flow.</summary>
public static class RunningPlayModeSmoke
{
    public static void Run()=>RunningContinuousPlayModeSmoke.Run();
}
#endif
