using ImGuiNET;
using RecompOne.Runtime.Host.Window;

namespace Kf2.Settings;

/// <summary>
/// The render distance and the enhancement distance, at the head of Video ▸
/// Experimental. Last in the pane, after the remaster.
/// </summary>
public sealed class DistancePage : IPatchPage
{
    public string Id => "distance";
    public string Title => "Experimental";
    public int Order => 31;

    const string Strings = """
    {
      "strings": {
        "kf2.renderdistance.label": {
          "en": "View distance (tiles)",
          "pt-BR": "Distância de visão (ladrilhos)",
          "es-419": "Distancia de visión (baldosas)"
        },
        "kf2.renderdistance.tooltip": {
          "en": "Draws the map further ahead. The game supports 10.5 to 15. Fog can still hide distant scenery.",
          "pt-BR": "Desenha o mapa mais à frente. O jogo permite de 10,5 a 15. A neblina ainda pode ocultar o cenário distante.",
          "es-419": "Dibuja el mapa más adelante. El juego permite de 10,5 a 15. La niebla todavía puede ocultar el escenario distante."
        },
        "kf2.distancefade.label": {
          "en": "Smooth distance fade",
          "pt-BR": "Transição suave de distância",
          "es-419": "Transición suave de distancia"
        },
        "kf2.distancefade.tooltip": {
          "en": "Gradually hides scenery near the view limit. Requires GPU geometry. Keeps fog and view distance unchanged.",
          "pt-BR": "Oculta gradualmente o cenário perto do limite de visão. Requer geometria na GPU. Mantém a neblina e a distância de visão.",
          "es-419": "Oculta gradualmente el escenario cerca del límite de visión. Requiere geometría en GPU. Mantiene la niebla y la distancia de visión."
        },
        "kf2.enhancedistance.label": {
          "en": "Enhancement distance",
          "pt-BR": "Distância das melhorias",
          "es-419": "Distancia de las mejoras"
        },
        "kf2.enhancedistance.tooltip": {
          "en": "How far from the camera the enhancements reach, in floor tiles. Past it a surface is drawn the game's own way: no per-pixel lighting, authored lights, texture filtering, ripples, ambient occlusion or reflections. Perspective correction, sub-pixel and the Z-buffer stay on. Experimental.",
          "pt-BR": "Até que distância da câmera as melhorias chegam, em ladrilhos do piso. Além dela uma superfície é desenhada como o jogo a desenha: sem iluminação por pixel, luzes criadas, filtragem de texturas, marolas, oclusão de ambiente ou reflexos. A correção de perspectiva, o subpixel e o Z-buffer continuam ativos. Experimental.",
          "es-419": "Hasta qué distancia de la cámara llegan las mejoras, en baldosas del piso. Más allá, una superficie se dibuja como la dibuja el juego: sin iluminación por píxel, luces creadas, filtrado de texturas, ondas, oclusión ambiental ni reflejos. La corrección de perspectiva, el subpíxel y el Z-buffer siguen activos. Experimental."
        },
        "kf2.enhancedistance.all": {
          "en": "Everywhere",
          "pt-BR": "Em toda parte",
          "es-419": "En todas partes"
        }
      }
    }
    """;

    public DistancePage() => Localization.Merge(Strings);

    public void Draw()
    {
        var flags = ImGuiSliderFlags.AlwaysClamp;

        float reach = RenderDistance.Tiles;
        if (ImGui.SliderFloat(Localization.T("kf2.renderdistance.label"), ref reach,
                              RenderDistance.Stock, RenderDistance.Max, "%.1f", flags))
        {
            RenderDistance.Set(reach);
            PatchSettings.Set(RenderDistance.Key, RenderDistance.Tiles);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Localization.T("kf2.renderdistance.tooltip"));

        bool fade = ViewDistanceFade.Enabled;
        if (ImGui.Checkbox(Localization.T("kf2.distancefade.label"), ref fade))
        {
            ViewDistanceFade.Set(fade);
            PatchSettings.Set(ViewDistanceFade.Key, fade);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Localization.T("kf2.distancefade.tooltip"));

        float plain = EnhancementDistance.Tiles > 0f ? EnhancementDistance.Tiles : EnhancementDistance.Max;
        string pf = EnhancementDistance.Tiles > 0f ? "%.1f" : Escape(Localization.T("kf2.enhancedistance.all"));
        if (ImGui.SliderFloat(Localization.T("kf2.enhancedistance.label"), ref plain,
                              EnhancementDistance.Min, EnhancementDistance.Max, pf, flags))
        {
            EnhancementDistance.Set(plain);
            PatchSettings.Set(EnhancementDistance.Key, EnhancementDistance.Tiles);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(Localization.T("kf2.enhancedistance.tooltip"));
    }

    static string Escape(string s) => s.Replace("%", "%%");
}
