using System.Reflection;
using ImGuiNET;
using Kf2;
using RecompOne.Runtime;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Host.Window;
using Verdite.Core;
using Rt = RecompOne.Runtime.Runtime;

// Run against a privately built game assembly, in a temporary working directory.
// No disc, game loop, native window or player data is opened by this check.
string previous = Environment.CurrentDirectory;
string scratch = Path.Combine(Path.GetTempPath(), "kf2-distance-" + Guid.NewGuid());
Directory.CreateDirectory(scratch);
Environment.CurrentDirectory = scratch;
ImGui.CreateContext();
try
{
    void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    ConfigManager.Load();
    Rt.Headless = true;
    ConfigManager.Game.Muted = true;
    ConfigManager.Game.MasterVolume = 0;
    Rt.View.SetString("unrelated", "keep");
    RenderDistance.Install();
    ViewDistanceFade.Install();
    Event.Dispatch(new RuntimeReadyEvent());
    Check(RenderDistance.Tiles == 10.5f && !ViewDistanceFade.Enabled, "defaults");
    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        RenderDistance.Set(invalid);
        Check(RenderDistance.Tiles == RenderDistance.Stock, "non-finite input");
    }
    RenderDistance.Set(999);
    Check(RenderDistance.Tiles == 30, "upper retained bound");
    RenderDistance.Set(-1);
    Check(RenderDistance.Tiles == 10.5f, "lower native bound");

    EnhancementDistance.Set(7);
    Fog.Set(Fog.Depth);
    RenderDistance.Set(13.5f);
    var active = typeof(GpuWorld).GetProperty(nameof(GpuWorld.Active))!;
    active.SetValue(null, true); // A retained frame without booting a GPU or game.
    foreach (bool fade in new[] { false, true, false })
    {
        ViewDistanceFade.Set(fade);
        foreach (float reach in new[] { 10.5f, 13.5f, 15f, 24.5f, 30f })
        {
            RenderDistance.Set(reach);
            RetainedScene.BeginFrame(default);
            RenderDistance.Build(new RecompOne.Runtime.Memory.PSMemory());
            ViewDistanceFade.AtFrame();
            var frame = RetainedScene.Find(RetainedScene.Serial)!;
            Check(frame.FadeEdge == (fade ? reach * 2048 : 0), "fade follows live distance");
            Check(frame.FadeBand == (fade ? 2048 : 0), "independent toggle, no stale weight");
            Check(frame.FadeDepth == (reach > 15 ? DistanceFade.DepthLimit : 0) && RenderDistance.Tiles == reach,
                "physical depth safety is independent of smooth fade");
            Check(RetainedScene.FogDistanceScale == reach / RenderDistance.Stock,
                "authored fog range follows distance rather than smooth fade");
            Check(Fog.Mode == Fog.Depth && EnhancementDistance.Tiles == 7, "unrelated settings changed");
            float edge = reach * 2048;
            float Weight(float d) => DistanceFade.Weight(d, 0, d, frame.FadeEdge, frame.FadeBand, frame.FadeDepth);
            Check(Weight(edge - 2048) == 1, "near scenery remains solid");
            Check(Weight(edge - 1024) == (fade ? .5f : 1), "fade midpoint");
            Check(Weight(edge) == (fade ? 0 : 1), "fade edge");
            if (reach > 15) Check(Weight(70000) == 0, "pitched/elevated geometry cannot exceed physical depth range");
        }
    }
    ViewDistanceFade.Set(true);
    active.SetValue(null, false);
    RetainedScene.BeginFrame(default);
    RenderDistance.Build(new RecompOne.Runtime.Memory.PSMemory());
    ViewDistanceFade.AtFrame();
    Check(RetainedScene.Find(RetainedScene.Serial)!.FadeBand == 0, "fallback must not get a partial fade");
    Check(RenderDistance.Tiles == 30 && RenderDistance.EffectiveTiles == 15 && RetainedScene.FogDistanceScale == 1,
        "packet fallback preserves the chosen value while respecting its physical range");

    RenderDistance.Set(23.5f);
    ViewDistanceFade.Set(false);
    PortSetting[] settings = [PortSettings.RenderDist, PortSettings.SmoothDistanceFade];
    var session = SettingsSession.Open(settings);
    session.Step(settings[0], 1);
    session.Step(settings[1], 1);
    Check(RenderDistance.Tiles == 24 && ViewDistanceFade.Enabled, "live menu edits beyond original limit");
    session.Discard();
    Check(RenderDistance.Tiles == 23.5f && !ViewDistanceFade.Enabled, "discard both independently");
    SettingsStore.Install();
    PanelManager.DrawPanels();
    session = SettingsSession.Open(settings);
    session.Step(settings[1], 1);
    session.Save();
    Check(!Rt.View.Has(RenderDistance.Key), "saving fade must not write distance");
    session = SettingsSession.Open(settings);
    session.Step(settings[0], 1);
    session.Save();
    ConfigManager.Load();
    RenderDistance.Set(10.5f);
    ViewDistanceFade.Set(false);
    Event.Dispatch(new RuntimeReadyEvent());
    Check(RenderDistance.Tiles == 24 && ViewDistanceFade.Enabled, "persist and reload both keys");
    Check(Rt.View.GetString("unrelated") == "keep", "preserve unrelated keys");
    session = SettingsSession.Open(settings);
    session.Reset(settings[1]);
    session.Save();
    Check(!Rt.View.Has(ViewDistanceFade.Key) && Rt.View.GetFloat(RenderDistance.Key) == 24,
        "default fade removes only its own key");

    foreach (string value in new[] { "on", "false" })
    {
        ViewDistanceFade.Configure(value);
        Event.Dispatch(new RuntimeReadyEvent());
        Check(ViewDistanceFade.Enabled == (value == "on"), "diagnostic switch");
    }
    Console.WriteLine("PASS: bounds, independent frames, fade weights, fallback, save/discard, reload, reset, diagnostic switch");
}
finally
{
    ImGui.DestroyContext();
    Environment.CurrentDirectory = previous;
    Directory.Delete(scratch, true);
}
