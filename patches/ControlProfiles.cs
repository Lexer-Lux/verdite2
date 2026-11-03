using RecompOne.Runtime;
using RecompOne.Runtime.Events;

namespace Kf2;

/// <summary>Setup queues a one-shot preset request; ordinary starts preserve custom bindings.</summary>
public static class ControlProfiles
{
    public const string Key = "kf2.controls.profile";
    public static int Selected => Runtime.View.GetInt(Key, 0);

    public static void Install() => Event.AddListener<RuntimeReadyEvent>(_ =>
    {
        int selected = Selected;
        if (selected != 0) Apply(selected);
    });

    public static void Apply(int selected)
    {
        if (selected < 0 || selected > 2) throw new ArgumentOutOfRangeException(nameof(selected));
        if (selected != 0)
        {
            bool modern = selected == 2;
            KeyLayout.ApplyChoice(modern);
            SeparateRun.ApplyPadLayout(modern);
            SeparateRun.Enabled = modern;
            Settings.PatchSettings.Set(SeparateRun.Setting, modern);
        }
        // Consume the request. Selecting the same preset again is a fresh action,
        // and finishing setup with Keep current controls never reapplies a preset.
        Runtime.View.Values.Remove(Key);
        Runtime.SaveView();
    }
}
