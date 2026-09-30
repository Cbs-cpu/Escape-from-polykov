namespace Polykov.UI.Framework
{
    /// <summary>
    /// The Escape-from-Tarkov-like look shared by every menu: near-black panels, 1 px grey borders, beige-grey text in
    /// "Polykov Grid", and inverted (light background, dark text) hover/selection.
    /// </summary>
    public static class UiTheme
    {
        public static readonly UiColor Background = UiColor.Hex(0x0A0B0C);
        public static readonly UiColor Bar = UiColor.Hex(0x0E0F10, 0.96f);
        public static readonly UiColor Panel = UiColor.Hex(0x111213, 0.93f);
        public static readonly UiColor PanelSolid = UiColor.Hex(0x121314);
        public static readonly UiColor Header = UiColor.Hex(0x1B1D1E, 0.97f);
        public static readonly UiColor Border = UiColor.Hex(0x2E3133);
        public static readonly UiColor BorderLight = UiColor.Hex(0x575B5D);
        public static readonly UiColor Text = UiColor.Hex(0xC7C5B3);
        public static readonly UiColor TextBright = UiColor.Hex(0xE9E7DA);
        public static readonly UiColor TextDim = UiColor.Hex(0x8A897F);
        public static readonly UiColor TextDisabled = UiColor.Hex(0x4E4E49);
        public static readonly UiColor Ink = UiColor.Hex(0x0B0C0D);
        public static readonly UiColor Highlight = UiColor.Hex(0xC7C5B3);
        public static readonly UiColor Accent = UiColor.Hex(0xC4A962);
        public static readonly UiColor Good = UiColor.Hex(0x79A357);
        public static readonly UiColor Bad = UiColor.Hex(0xC34A3C);
        public static readonly UiColor Warn = UiColor.Hex(0xC99E48);
        public static readonly UiColor CellBack = UiColor.Hex(0x141617, 0.96f);
        public static readonly UiColor CellLine = UiColor.Hex(0x26292B);
        public static readonly UiColor Stripe = UiColor.Hex(0x1A1C1D);

        public const int SizeSmall = 13;
        public const int SizeBody = 15;
        public const int SizeLabel = 17;
        public const int SizeTitle = 22;
        /// <summary>Inventory cell size in reference pixels (Tarkov's grid is 63 px at 1080p).</summary>
        public const float Cell = 63f;
    }
}
