namespace Polykov.UI.Framework
{
    /// <summary>
    /// Drawing primitives. The same UI code runs in Unity (IMGUI backend) and offline (Skia backend used to render
    /// previews of the menus without Unity), so screens must only draw through this interface.
    /// </summary>
    public interface IUiBackend
    {
        void Fill(UiRect r, UiColor c);
        void Line(UiVec a, UiVec b, UiColor c, float width);
        /// <summary>Single line vertically centred in <paramref name="r"/> unless <paramref name="wrap"/> (top aligned, word wrap).</summary>
        void Text(UiRect r, string text, UiFont font, int size, UiColor c, UiAlign align, bool wrap = false);
        float TextWidth(string text, UiFont font, int size);
        void Image(UiRect r, UiImage image, UiColor tint);
        /// <summary>Clips everything drawn until the matching <see cref="PopClip"/> (nesting intersects).</summary>
        void PushClip(UiRect r);
        void PopClip();
    }
}
