using RecompOne.Runtime;
using RecompOne.Runtime.Events;

namespace Kf2;

/// <summary>The main view's optional one-tile distance transition. This only
/// publishes renderer weights: it never changes visibility, fog or enhancement distance.</summary>
public static class ViewDistanceFade
{
    public const string Key = "kf2.distancefade.on";
    public const bool Default = false;
    public const float Band = 2048f;

    static bool? _forced;
    public static bool Enabled { get; private set; } = Default;

    public static void Configure(string? on)
    {
        _forced = on?.Trim().ToLowerInvariant() switch
        {
            "1" or "on" or "true" => true,
            "0" or "off" or "false" => false,
            _ => null,
        };
    }

    public static void Install() => Event.AddListener<RuntimeReadyEvent>(_ =>
    {
        Set(_forced ?? RecompOne.Runtime.Runtime.View.GetBool(Key, Default));
        Console.WriteLine($"[KF2] smooth distance fade: {(Enabled ? "on" : "off")}");
    });

    public static void Set(bool enabled) => Enabled = enabled;

    /// <summary>After BeginFrame resets the frame, and GPU geometry chooses its path.
    /// Leave the mirror and first-person arm to the renderer's existing exclusions.
    /// Re-publish zeroes when disabled so a live toggle cannot leave a stale fade.</summary>
    public static void AtFrame() => RetainedScene.SetFade(
        Enabled && GpuWorld.Active ? RenderDistance.Tiles * 2048f : 0f,
        Enabled && GpuWorld.Active ? Band : 0f, 0f);
}
