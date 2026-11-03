using System.Reflection;
using System.Text.Json;
using ImGuiNET;
using Kf2;
using RecompOne.Runtime;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Events;

// Private settings only. No disc, cards, game loop, native window or player data.
string previous = Environment.CurrentDirectory;
string scratch = Path.Combine(Path.GetTempPath(), "kf2-controls-" + Guid.NewGuid());
Directory.CreateDirectory(scratch);
Environment.CurrentDirectory = scratch;
ImGui.CreateContext();
Runtime.Headless = true;
try
{
    void Check(bool ok, string name)
    { if (!ok) throw new Exception(name); Console.WriteLine("PASS: " + name); }
    bool Same(object a, object b) => JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
    ConfigManager.Load();
    KeyLayout.Configure(); KeyLayout.Install(); SeparateRun.Install(); ControlProfiles.Install();
    Runtime.View.SetInt(ControlProfiles.Key, 1); // launcher first-run queues Classic
    Event.Dispatch(new RuntimeReadyEvent());
    Check(Same(ConfigManager.Game.Keys, KeyLayout.ClassicLayout()), "first-run Classic keyboard baseline");
    Check(Same(ConfigManager.Game.Pad, new GamepadBindings()) &&
          Same(ConfigManager.Game.PadAnalog, GamepadBindings.DefaultAnalog()), "Classic upstream controller tables");
    Check(!SeparateRun.Enabled && !Runtime.View.Has(ControlProfiles.Key), "Classic applies once and consumes request");

    var core = typeof(KeyLayout).Assembly.GetType("Verdite.Core.Onboarding")!;
    var spec = core.GetMethod("Load")!.Invoke(null, ["KF2", new string[] { args[0] }])!;
    Check(spec != null, "shared setup specification loads");
    var apply = typeof(Setup).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static)!;
    core.GetProperty("LiveApply")!.SetValue(null, apply.CreateDelegate<Func<string, string?, bool>>());
    void Choose(int profile) => core.GetMethod("Apply")!.Invoke(null,
        [spec, new List<Dictionary<string,string?>> { new() { [ControlProfiles.Key] = profile == 0 ? null : profile.ToString() } }, "KF2"]);

    ConfigManager.Game.Keys.Cross = "R";
    ConfigManager.Game.Pad.Square = [2, 10];
    Choose(1);
    Check(ConfigManager.Game.Keys.Cross == "F" && Same(ConfigManager.Game.Pad, new GamepadBindings()),
        "rerun can explicitly select the same Classic preset after customization");
    Choose(0);
    Check(KeyLayout.IsApplied() && ConfigManager.Game.Keys.Cross == "F" && !SeparateRun.Enabled,
        "Keep after Classic preserves its keyboard secondary inputs and run behavior");
    Choose(2);
    Check(Same(ConfigManager.Game.Keys, KeyLayout.Layout()) && Same(ConfigManager.Game.Pad, SeparateRun.Layout()) &&
          SeparateRun.Enabled, "rerun Modern bindings and separate run");
    ConfigManager.Game.Keys.Cross = "R";
    ConfigManager.Game.Pad.Square = [2, 10];
    ConfigManager.SaveGame(); Runtime.SaveView();
    string custom = File.ReadAllText("settings.json");
    Choose(0);
    Event.Dispatch(new RuntimeReadyEvent());
    Check(File.ReadAllText("settings.json") == custom, "Keep and ordinary startup preserve custom bindings");
    ConfigManager.Load();
    Event.Dispatch(new RuntimeReadyEvent());
    Check(ConfigManager.Game.Keys.Cross == "R" && ConfigManager.Game.Pad.Square.SequenceEqual(new[] {2,10}),
        "custom bindings survive disk reload and startup");
    Choose(2);
    Check(ConfigManager.Game.Keys.Cross == "E" && ConfigManager.Game.Keys.L3 == "ShiftLeft",
        "same Modern preset can explicitly reset customized bindings");
    Console.WriteLine("Control setup checks passed.");
}
finally
{
    ImGui.DestroyContext();
    Environment.CurrentDirectory = previous;
    string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!Path.GetFullPath(scratch).StartsWith(temp, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Unexpected private test directory");
    Directory.Delete(scratch, recursive: true);
}
