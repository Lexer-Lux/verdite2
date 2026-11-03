using System.Text.Json;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Hardware;
using RecompOne.Runtime.Host.Window;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Kf2;

/// <summary>Separate running from use without replacing the game's movement or menus.</summary>
public static class SeparateRun
{
    public const string Setting = "kf2.controls.separaterun";
    const string PadVersion = "kf2.controls.padlayout";
    const uint Pad = 0x80199554, PreviousPad = 0x80199556;
    const uint UseMask = 0x8006E568, MenuMask = 0x8006E56C;
    // L3 is unused by the original game. Bind it in the normal input table.
    const ushort RunBit = (Controller.L3 >> 8) | (Controller.L3 << 8);
    public static bool Enabled = true;

    public static GamepadBindings Layout() => new()
    {
        Cross = [0], Circle = [1, 6], Square = [10, 101], Triangle = [9, 100],
        L1 = [], R1 = [], L2 = [], R2 = [], L3 = [1], R3 = [],
        Start = [], Select = [4], Up = [11], Down = [12], Left = [13], Right = [14],
    };

    public static void ApplyPadLayout(bool modern = true)
    {
        ConfigManager.Game.Pad = modern ? Layout() : new GamepadBindings();
        ConfigManager.Game.PadAnalog = modern ? Layout() : GamepadBindings.DefaultAnalog();
        ConfigManager.SaveGame();
        Settings.PatchSettings.Set(PadVersion, 1);
    }

    public static void Install()
    {
        Localization.Merge("""
        { "strings": {
          "kf2.controls.separaterun": { "en": "Separate run input (L3 binding)", "pt-BR": "Botão de corrida separado (L3)", "es-419": "Botón separado para correr (L3)" },
          "kf2.controls.padlayout": { "en": "Apply Lexer's controller defaults", "pt-BR": "Aplicar controles padrão do Lexer", "es-419": "Aplicar controles predeterminados de Lexer" },
          "kf2.controls.run": { "en": "run (separate input)", "pt-BR": "correr (botão separado)", "es-419": "correr (botón separado)" },
          "kf2.controls.menu": { "en": "menu; back (Circle runs during play)", "pt-BR": "menu; voltar (Círculo corre durante o jogo)", "es-419": "menú; volver (Círculo corre durante el juego)" }
        } }
        """);
        Event.AddListener<RuntimeReadyEvent>(_ =>
        {
            Enabled = Settings.PatchSettings.Get(Setting, true);
            if (Settings.PatchSettings.Get(PadVersion, 0) >= 1) return;
            // Only known defaults migrate. Custom bindings and the second player stay theirs.
            bool changed = false;
            if (Same(ConfigManager.Game.Pad, new GamepadBindings()))
            { ConfigManager.Game.Pad = Layout(); changed = true; }
            if (Same(ConfigManager.Game.PadAnalog, GamepadBindings.DefaultAnalog()))
            { ConfigManager.Game.PadAnalog = Layout(); changed = true; }
            if (changed) ConfigManager.SaveGame();
            Settings.PatchSettings.Set(PadVersion, 1);
        });
        HookAttach.OnOverlayLoad("separate run", Attach);
    }

    static bool Same(GamepadBindings a, GamepadBindings b) =>
        JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);

    static readonly ModInfo Self = new() { Id = "kf2-separate-run", Name = "Separate run input" };
    static bool _registered;
    static bool Attach()
    {
        SymbolRegistry.Build();
        var run = SymbolRegistry.Resolve("game", null, 0x8002957C);
        var use = SymbolRegistry.Resolve("game", null, 0x80029CBC);
        if (run == null || use == null) return false;
        if (!_registered)
        {
            bool ok = HookManager.AddPre(Self, run, typeof(SeparateRun).GetMethod(nameof(BeforeRun))!, -1000);
            ok &= HookManager.AddPost(Self, run, typeof(SeparateRun).GetMethod(nameof(AfterRun))!, 1000);
            ok &= HookManager.AddPre(Self, use, typeof(SeparateRun).GetMethod(nameof(BeforeInteract))!, -1000);
            ok &= HookManager.AddPost(Self, use, typeof(SeparateRun).GetMethod(nameof(AfterInteract))!, 1000);
            if (!ok) { HookManager.RemoveMod(Self); return false; }
            _registered = true;
        }
        HookManager.Commit();
        bool installed = HookAttach.Installed(run) && HookAttach.Installed(use);
        Console.WriteLine($"[KF2] separate run: hooks committed={installed}");
        return installed;
    }

    static ushort _runNow, _runBefore, _runMask, _menuNow, _menuMask;

    public static void BeforeRun(CpuContext c, IMemory m)
    {
        _runMask = 0;
        if (!Enabled) return;
        ushort mask = (ushort)m.ReadU32(UseMask);
        ushort now = m.ReadU16(Pad), before = m.ReadU16(PreviousPad);
        bool running = (now & RunBit) != 0;
        _runNow = now; _runBefore = before; _runMask = mask;
        m.WriteU16(Pad, (ushort)((now & ~mask) | (running ? mask : 0)));
        m.WriteU16(PreviousPad, (ushort)((before & ~mask) | (running ? mask : 0)));
    }

    public static void AfterRun(CpuContext c, IMemory m)
    {
        Restore(m, Pad, _runMask, _runNow);
        Restore(m, PreviousPad, _runMask, _runBefore);
        _runMask = 0;
    }

    public static void BeforeInteract(CpuContext c, IMemory m)
    {
        _menuMask = 0;
        if (!Enabled) return;
        ushort now = m.ReadU16(Pad);
        // Circle also remains Back in the game's menus. Only the gameplay menu
        // opener ignores it when the same physical button is holding Run.
        if ((now & RunBit) == 0) return;
        _menuNow = now;
        _menuMask = (ushort)m.ReadU32(MenuMask);
        m.WriteU16(Pad, (ushort)(now & ~_menuMask));
    }

    public static void AfterInteract(CpuContext c, IMemory m)
    {
        Restore(m, Pad, _menuMask, _menuNow);
        _menuMask = 0;
    }

    static void Restore(IMemory m, uint address, ushort mask, ushort original)
    {
        if (mask != 0)
            m.WriteU16(address, (ushort)((m.ReadU16(address) & ~mask) | (original & mask)));
    }
}
