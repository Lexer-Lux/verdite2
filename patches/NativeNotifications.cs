using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;
using Game = Recompiled.KingsField2_game;

namespace Kf2;

/// <summary>Source mods share the game's notification queue and original font renderer.</summary>
public static class NativeNotifications
{
    const byte CustomBase = 232;
    const uint Queue = 0x80192D28, Amounts = 0x80192D30;
    const uint Read = 0x80192D40, Write = 0x80192D41, State = 0x80192D42, Alpha = 0x80192D44;
    const uint Sprite = 0x800676F4;
    sealed record Message(string Owner, byte[] Text);
    static readonly Queue<Message> Pending = new();
    static readonly Dictionary<int, Message> Owned = new();
    static readonly ModInfo Self = new() { Id = "kf2-native-notifications", Name = "Native notification API" };
    static bool _registered;

    public static void Install() => HookAttach.OnOverlayLoad("native notifications", Attach);

    static bool Attach()
    {
        SymbolRegistry.Build();
        uint[] addresses = [0x8002A550, 0x8004A040, 0x80033FBC, 0x80033E78];
        string[] methods = [nameof(Flush), nameof(Loaded), nameof(Updated), nameof(Draw)];
        var symbols = addresses.Select(a => SymbolRegistry.Resolve("game", null, a)).ToArray();
        if (symbols.Any(s => s is null)) return false;
        if (!_registered)
        {
            bool ok = true;
            for (int i = 0; i < symbols.Length; i++)
                ok &= HookManager.AddPost(Self, symbols[i]!, typeof(NativeNotifications).GetMethod(methods[i])!);
            if (!ok) { HookManager.RemoveMod(Self); return false; }
            _registered = true;
        }
        HookManager.Commit();
        return symbols.All(s => HookAttach.Installed(s!));
    }

    /// <summary>Post up to 24 original-font glyphs, without a terminator. Bounded to 512 pending messages.</summary>
    public static void Post(string owner, ReadOnlySpan<byte> glyphs)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);
        if (owner.Length > 96) throw new ArgumentOutOfRangeException(nameof(owner));
        int end = glyphs.IndexOf((byte)0xFF);
        if (end >= 0) glyphs = glyphs[..end];
        if (glyphs.Length > 0 && Pending.Count < 512)
            Pending.Enqueue(new(owner, glyphs[..Math.Min(24, glyphs.Length)].ToArray()));
    }

    /// <summary>Convenience for English notifications; item names should use original glyph bytes.</summary>
    public static void PostText(string owner, string text)
    {
        byte[] glyphs = new byte[Math.Min(24, text.Length)];
        for (int i = 0; i < glyphs.Length; i++)
        {
            char c = char.ToUpperInvariant(text[i]);
            if (c != ' ' && (c < 'A' || c > 'Z'))
                throw new ArgumentException("Use original glyph bytes for characters outside A-Z and space", nameof(text));
            glyphs[i] = c == ' ' ? (byte)0x7F : (byte)(c - 'A');
        }
        Post(owner, glyphs);
    }

    public static void Flush(CpuContext c, IMemory m)
    {
        while (Pending.Count > 0)
        {
            int slot = m.ReadU8(Write) & 7;
            if (m.ReadU8(Queue + (uint)slot) != 0xFF) return;
            var saved = c.Snapshot();
            c.SP -= 0x100;
            // Equal consecutive notification IDs are coalesced by the native controller.
            c.A0 = (uint)(CustomBase + slot); c.A1 = 0;
            try { Game.func_80033F08(c, m); }
            finally { c.Restore(saved); }
            if (m.ReadU8(Queue + (uint)slot) != CustomBase + slot) return;
            Owned[slot] = Pending.Dequeue();
        }
    }

    public static void Loaded(CpuContext c, IMemory m) => Clear(m, null);

    /// <summary>Remove one disabled mod's messages without removing other mods or vanilla feedback.</summary>
    public static void Cancel(string owner)
    {
        if (Runtime.Mem is { } m) Clear(m, owner);
        else FilterPending(owner);
    }

    static void FilterPending(string? owner)
    {
        var keep = Pending.Where(p => owner != null && p.Owner != owner).ToArray();
        Pending.Clear();
        foreach (var message in keep) Pending.Enqueue(message);
    }

    static bool IsOwned(IMemory m, int slot) =>
        m.ReadU8(Queue + (uint)slot) == CustomBase + slot && Owned.ContainsKey(slot);

    static void Clear(IMemory m, string? owner)
    {
        FilterPending(owner);
        if (Owned.Count == 0) return;
        int head = m.ReadU8(Read) & 7;
        bool activeRemoved = IsOwned(m, head) && (owner == null || Owned[head].Owner == owner);
        var keep = new List<(byte Id, ushort Amount, Message? Message)>();
        for (int i = 0; i < 8; i++)
        {
            int slot = (head + i) & 7;
            byte id = m.ReadU8(Queue + (uint)slot);
            if (id == 0xFF) continue;
            Message? message = IsOwned(m, slot) ? Owned[slot] : null;
            if (message != null && (owner == null || message.Owner == owner)) continue;
            keep.Add((id, m.ReadU16(Amounts + (uint)slot * 2), message));
        }
        Owned.Clear();
        for (int i = 0; i < 8; i++)
        {
            byte id = i < keep.Count ? keep[i].Id : (byte)0xFF;
            if (i < keep.Count && keep[i].Message is { } message)
            { id = (byte)(CustomBase + i); Owned[i] = message; }
            m.WriteU8(Queue + (uint)i, id);
            m.WriteU16(Amounts + (uint)i * 2, i < keep.Count ? keep[i].Amount : (ushort)0);
        }
        m.WriteU8(Read, 0); m.WriteU8(Write, (byte)(keep.Count & 7));
        if (activeRemoved)
        {
            m.WriteU8(State, 0); m.WriteU8(Alpha, 0);
            for (uint i = 0; i < 6; i++) m.WriteU8(Sprite + i * 0x12, 0);
        }
    }

    public static void Updated(CpuContext c, IMemory m)
    {
        int slot = m.ReadU8(Read) & 7;
        if (IsOwned(m, slot))
            for (uint i = 0; i < 6; i++) m.WriteU8(Sprite + i * 0x12, 0);
    }

    public static void Draw(CpuContext c, IMemory m)
    {
        int slot = m.ReadU8(Read) & 7;
        if (!IsOwned(m, slot)) return;
        byte[] text = Owned[slot].Text;
        uint descriptor = m.ReadU32(0x8017E0A4), begin = m.ReadU32(descriptor + 8), end = m.ReadU32(descriptor + 4);
        if (begin > end || (uint)text.Length * 0x28 > end - begin) return;
        int width = m.ReadU16(Sprite + 10), x = (short)m.ReadU16(Sprite + 6) + (width - text.Length * 7) / 2;
        int y = (short)m.ReadU16(Sprite + 8);
        var saved = c.Snapshot();
        c.SP -= 0x100;
        uint record = c.SP + 0x40;
        m.WriteU16(record, (ushort)x); m.WriteU16(record + 2, (ushort)y);
        for (uint i = 0; i < text.Length; i++) m.WriteU8(record + 4 + i, text[i]);
        m.WriteU8(record + 4 + (uint)text.Length, 0xFF);
        // Original font allocation reads this cursor, then advances it AND the
        // descriptor. Other native HUD mods may have allocated packets first.
        m.WriteU32(0x8006E914, begin);
        c.A0 = 0x80064BF0; c.A1 = record;
        try { Game.func_80021E10(c, m); }
        finally { c.Restore(saved); }
        byte brightness = m.ReadU8(Alpha);
        uint last = m.ReadU32(descriptor + 8);
        for (uint p = begin; p < last; p += 0x28)
        {
            m.WriteU8(p + 4, brightness); m.WriteU8(p + 5, brightness); m.WriteU8(p + 6, brightness);
        }
    }
}
