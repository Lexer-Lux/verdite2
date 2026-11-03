using System.Reflection;
using System.Text.Json;
using ImGuiNET;
using Kf2;
using RecompOne.Runtime;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Hardware;
using RecompOne.Runtime.Memory;
using Game = Recompiled.KingsField2_game;

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
    Analog.Install(); EnemyTurn.Install();
    Runtime.View.SetInt(ControlProfiles.Key, 1); // launcher first-run queues Classic
    Event.Dispatch(new RuntimeReadyEvent());
    Check(Analog.TurnSens == 3f && Analog.PitchSens == 3f && EnemyTurn.Multiplier == 3f,
        "absent settings use look x3 and proportional enemy turn x3");
    Check(!EnemyTurn.Track, "compensation does not add standing tracking");
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

    Runtime.View.SetFloat(Analog.TurnSensKey, 2.25f);
    Runtime.View.SetFloat(Analog.PitchSensKey, 1.75f);
    Runtime.View.SetFloat(EnemyTurn.Key, 1.5f);
    Runtime.View.SetInt("kf2.analog.defaults", 0); // old config must not reset custom values
    Runtime.View.SetBool(Analog.EaseKey, true);
    Runtime.SaveView();
    string customView = File.ReadAllText("interface.ini");
    Event.Dispatch(new RuntimeReadyEvent());
    Check(Analog.TurnSens == 2.25f && Analog.PitchSens == 1.75f && Analog.StickEase && EnemyTurn.Multiplier == 1.5f,
        "legacy config preserves custom sensitivities and enemy turn");
    Check(File.ReadAllText("interface.ini") == customView, "startup does not rewrite saved native preferences");

    // Exercise the original recompiled routines in private RAM, not a formula mock.
    Verdite.Core.Game.Configure(tag: "KF2");
    Verdite.Core.Mouse.Configure(Analog.MouseValues);
    Dispatcher.Register("game", new Recompiled.GameDispatchTable());
    var memory = new PSMemory();
    typeof(Runtime).GetProperty(nameof(Runtime.Mem))!.SetValue(null, memory);
    Dispatcher.Load("game"); // installs the actual analog and enemy-turn hooks
    var cpu = new CpuContext { SP = 0x801FFFF0 };
    Analog.Tank = false; Analog.StickEase = false; Analog.LookAccel = false;
    Controller.LeftX = Controller.LeftY = Controller.RightY = 128;
    Controller.RightX = 255;
    memory.WriteU32(0x8006E59C, Controller.Left);
    memory.WriteU32(0x8006E598, Controller.Right);
    memory.WriteU32(0x8006E584, Controller.R2);
    memory.WriteU32(0x8006E58C, Controller.L2);
    foreach (int rate in new[] { 28, 35 })
    foreach (int sensitivity in new[] { 1, 3 })
    {
        Analog.TurnSens = sensitivity;
        memory.WriteU32(0x8019955C, (uint)rate);
        memory.WriteU16(0x80199554, 0);
        memory.WriteU16(0x80199544, 0);
        memory.WriteU16(0x8019950E, 2048);
        Game.func_80028DB8(cpu, memory);
        Check(memory.ReadU16(0x8019950E) == 2048 - rate * sensitivity,
            $"native yaw rate {rate}, sensitivity {sensitivity}: {rate * sensitivity} units/tick");
    }
    Controller.RightX = 128; Controller.RightY = 255; Analog.PitchSens = 3;
    memory.WriteU16(0x80199546, 0); memory.WriteU16(0x8019950C, 0);
    Game.func_80028DB8(cpu, memory);
    Check(memory.ReadU16(0x8019950C) == 96, "native pitch at x3: 96 units/tick");

    const uint creature = 0x8016C544;
    memory.WriteU32(0x801994EC, unchecked((uint)-10000));
    memory.WriteU32(0x801994F4, 0); // bearing 1024
    int TurnTicks(float multiplier, uint target)
    {
        EnemyTurn.SetMultiplier(multiplier);
        memory.WriteU16(creature + 0x42, 0); memory.WriteU16(creature + 0x58, 0);
        for (int tick = 1; tick < 400; tick++)
        {
            cpu.A0 = creature; cpu.A1 = target; cpu.A2 = 11; cpu.A3 = 4; cpu.RA = 0x8003E750;
            EnemyTurn.BeforeTurn(cpu, memory);
            if (cpu.A2 != (target == 1024 ? (uint)(11 * multiplier) : 11u) ||
                cpu.A3 != (target == 1024 ? (uint)(4 * multiplier * multiplier) : 4u))
                throw new Exception($"Incorrect turn arguments: {target}, x{multiplier}, tick {tick}");
            cpu.A2 = 11; cpu.A3 = 4; // the committed hook scales the original arguments
            byte[] before = Enumerable.Range(0, 0x7C).Select(i => memory.ReadU8(creature + (uint)i)).ToArray();
            Game.func_8003C8D8(cpu, memory);
            for (int i = 0; i < before.Length; i++)
                if (i is not (0x42 or 0x43 or 0x58 or 0x59) && memory.ReadU8(creature + (uint)i) != before[i])
                    throw new Exception($"Turn changed unrelated creature field +{i:X}");
            if (memory.ReadU16(creature + 0x42) == target) return tick;
        }
        throw new Exception("native turn did not reach target");
    }
    int baseline = TurnTicks(1, 1024), compensated = TurnTicks(3, 1024);
    Check(compensated < baseline && Math.Abs(baseline / 3d - compensated) <= 1,
        $"native quarter turn: original {baseline} ticks, compensated {compensated} ticks");
    Check(TurnTicks(1, 512) == TurnTicks(3, 512), "non-player turn retains native timing");
    cpu.A0 = creature; cpu.A1 = 1024; cpu.A2 = 0; cpu.A3 = 4;
    EnemyTurn.BeforeTurn(cpu, memory);
    Check(cpu.A2 == 0, "zero-speed native actions remain zero-speed");
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
