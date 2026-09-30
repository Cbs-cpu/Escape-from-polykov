using Polykov.Weapons;

namespace Polykov.Lobby
{
    /// <summary>Spanish display names for slots, parts and stats. Unknown ids fall back to the id itself.</summary>
    public static class AttachmentLabels
    {
        public static string Slot(AttachmentSlot slot) => slot switch
        {
            AttachmentSlot.Muzzle => "BOCA",
            AttachmentSlot.Barrel => "CAÑÓN",
            AttachmentSlot.Grips => "CACHAS",
            _ => "CARGADOR",
        };

        public static string Part(string id) => id switch
        {
            null => "Ninguno",
            "" => "Ninguno",
            "barrel_standard" => "Cañón estándar",
            "barrel_threaded" => "Cañón roscado",
            "suppressor_45" => "Silenciador .45 ACP",
            "grips_wood" => "Cachas de madera",
            "magazine_7" => "Cargador estándar (7)",
            _ => id,
        };

        public static string Stat(StatKind kind) => kind switch
        {
            StatKind.Ergonomics => "Ergonomía",
            StatKind.Recoil => "Retroceso vertical",
            StatKind.Weight => "Peso",
            StatKind.Length => "Longitud",
            StatKind.Loudness => "Sonoridad",
            _ => "Fogonazo",
        };

        public static string Unit(StatKind kind) => kind switch
        {
            StatKind.Ergonomics => "",
            StatKind.Recoil => "°",
            StatKind.Weight => " kg",
            StatKind.Length => " cm",
            _ => " %",
        };

        public static string Format(StatKind kind, float value)
            => kind == StatKind.Recoil || kind == StatKind.Weight ? value.ToString("0.00") + Unit(kind)
                : kind == StatKind.Length ? value.ToString("0.0") + Unit(kind)
                : value.ToString("0") + Unit(kind);
    }
}
