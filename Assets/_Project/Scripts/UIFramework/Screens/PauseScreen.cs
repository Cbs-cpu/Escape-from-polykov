namespace Polykov.UI.Framework
{
    /// <summary>Player preferences as plain data (the host copies them from/to its settings store).</summary>
    public sealed class PauseSettings
    {
        public float Sensitivity = 0.08f, Fov = 62f, HeadBob = 1f, Shake = 1f, Volume = 1f;
        public bool InvertY, ToggleCrouch, Dismemberment = true;
        /// <summary>0 = off, 1 = reduced, 2 = full.</summary>
        public int Gore = 2;
        public string[] Presets = System.Array.Empty<string>();
        /// <summary>-1 = the MovementSettings asset, else index into <see cref="Presets"/>.</summary>
        public int Preset = -1;
        public bool CanReturnToLobby = true;
    }

    public enum PauseAction : byte { None, Resume, ReturnToLobby, ResetSettings }

    /// <summary>In-raid pause menu (Esc) in the lobby's Tarkov style: settings on the left, controls on the right.</summary>
    public static class PauseScreen
    {
        private static readonly (string key, string action)[] Controls =
        {
            ("WASD", "Moverse"), ("Shift", "Esprintar"), ("Alt", "Andar"), ("Espacio", "Saltar"), ("C", "Agacharse"),
            ("Q / E", "Inclinarse"), ("Clic izq.", "Disparar"), ("Clic der.", "Apuntar"), ("R", "Recargar"), ("B", "Seguro"),
            ("L", "Inspeccionar el arma"), ("T", "Comprobar recámara"), ("Esc", "Pausa"),
            ("F1", "Depuración"), ("F2", "Perfil de movimiento"), ("F3", "Dianas"), ("F4", "Munición"),
            ("F6", "Simular red (F7 empujar, F8 marcador)"), ("F9", "Silenciador"), ("F10", "Desmembrar (pruebas)"),
        };

        public static PauseAction Frame(Ui ui, PauseSettings s)
        {
            ui.Fill(new UiRect(0, 0, ui.Width, ui.Height), UiColor.Hex(0x050606, 0.78f));
            const float w = 1120f, h = 720f;
            var r = new UiRect((ui.Width - w) * 0.5f, (ui.Height - h) * 0.5f, w, h);
            ui.SpacedText(new UiRect(r.X, r.Y - 70f, w, 50f), "PAUSA", 40, UiFont.Bold, 12f, UiTheme.TextBright);

            var left = new UiRect(r.X, r.Y, 620f, h - 80f);
            var right = new UiRect(left.XMax + 16f, r.Y, w - 636f, h - 80f);
            UiRect inner = ui.Panel(left, "AJUSTES");
            float x = inner.X + 20f, y = inner.Y + 16f, cw = inner.W - 40f;

            y = Slider(ui, "sens", "Sensibilidad del ratón", x, y, cw, ref s.Sensitivity, 0.01f, 0.3f, "0.000");
            y = Slider(ui, "fov", "Campo de visión (FOV)", x, y, cw, ref s.Fov, 50f, 90f, "0");
            y = Slider(ui, "bob", "Balanceo de cámara", x, y, cw, ref s.HeadBob, 0f, 1f, "0.00");
            y = Slider(ui, "shake", "Sacudidas de cámara", x, y, cw, ref s.Shake, 0f, 1f, "0.00");
            y = Slider(ui, "vol", "Volumen", x, y, cw, ref s.Volume, 0f, 1f, "0.00");
            y += 6f;
            s.InvertY = ui.Toggle(new UiRect(x, y, cw * 0.5f, 26f), s.InvertY, "Invertir eje Y");
            s.ToggleCrouch = ui.Toggle(new UiRect(x + cw * 0.5f, y, cw * 0.5f, 26f), s.ToggleCrouch, "Agacharse alterna");
            y += 34f;
            s.Dismemberment = ui.Toggle(new UiRect(x, y, cw, 26f), s.Dismemberment, "Desmembramiento");
            y += 40f;

            ui.SectionTitle(new UiRect(x, y, cw, 22f), "SANGRE");
            y += 30f;
            string[] gore = { "DESACTIVADA", "REDUCIDA", "COMPLETA" };
            s.Gore = Segmented(ui, new UiRect(x, y, cw, 36f), gore, s.Gore);
            y += 52f;

            ui.SectionTitle(new UiRect(x, y, cw, 22f), "PERFIL DE MOVIMIENTO", "F2 alterna jugando");
            y += 30f;
            var presets = new string[s.Presets.Length + 1];
            presets[0] = "ASSET";
            for (int i = 0; i < s.Presets.Length; i++) presets[i + 1] = s.Presets[i].ToUpperInvariant();
            s.Preset = Segmented(ui, new UiRect(x, y, cw, 36f), presets, s.Preset + 1) - 1;
            y += 44f;
            ui.Paragraph(new UiRect(x, y, cw, 20f), "\"Asset\" usa MovementSettings.asset (editable en Play Mode).", UiTheme.SizeSmall);

            UiRect help = ui.Panel(right, "CONTROLES");
            float hy = help.Y + 12f;
            foreach (var (key, action) in Controls)
            {
                var row = new UiRect(help.X + 16f, hy, help.W - 32f, 26f);
                var keyBox = new UiRect(row.X, row.Y + 2f, 92f, 22f);
                ui.Fill(keyBox, UiColor.Hex(0x1E2021));
                ui.Frame(keyBox, UiTheme.Border);
                ui.Label(keyBox, key, UiTheme.SizeSmall, UiFont.Bold, UiAlign.Center, UiTheme.Text);
                ui.Label(new UiRect(row.X + 106f, row.Y, row.W - 106f, row.H), action, UiTheme.SizeBody, UiFont.Regular, UiAlign.Left, UiTheme.TextDim);
                hy += 29f;
            }

            float by = r.YMax - 60f;
            PauseAction result = PauseAction.None;
            if (ui.Button(new UiRect(r.X, by, 320f, 60f), "REANUDAR", ButtonStyle.Primary, null, 22)) result = PauseAction.Resume;
            if (s.CanReturnToLobby && ui.Button(new UiRect(r.X + 336f, by, 320f, 60f), "VOLVER AL LOBBY", ButtonStyle.Normal, null, UiTheme.SizeLabel))
                result = PauseAction.ReturnToLobby;
            if (ui.Button(new UiRect(r.XMax - 260f, by, 260f, 60f), "RESTABLECER AJUSTES", ButtonStyle.Normal, null, UiTheme.SizeBody))
                result = PauseAction.ResetSettings;
            return result;
        }

        private static float Slider(Ui ui, string id, string label, float x, float y, float w, ref float value, float min, float max, string format)
        {
            ui.Label(new UiRect(x, y, 240f, 30f), label, UiTheme.SizeBody, UiFont.Regular, UiAlign.Left, UiTheme.Text);
            value = ui.Slider(id, new UiRect(x + 250f, y, w - 250f, 30f), value, min, max, format);
            return y + 40f;
        }

        private static int Segmented(Ui ui, UiRect r, string[] options, int selected)
        {
            float w = r.W / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var b = new UiRect(r.X + i * w, r.Y, w - (i < options.Length - 1 ? 6f : 0f), r.H);
                if (ui.Button(b, options[i], i == selected ? ButtonStyle.Selected : ButtonStyle.Normal, null, UiTheme.SizeBody)) selected = i;
            }
            return selected;
        }
    }
}
