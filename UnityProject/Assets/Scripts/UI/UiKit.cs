using System;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Kit de interface provisório (IMGUI): estilos, escala para 1920x1080 e controles reutilizáveis.
    /// Serve para validar fluxo e funções das telas. A interface final (com a arte) vai substituir isto,
    /// mas as telas continuam chamando as mesmas ações em <see cref="GameApp"/>.
    /// </summary>
    public static class UiKit
    {
        public const float RefH = 1080f;
        public static float Scale { get; private set; } = 1f;
        /// <summary>Tamanho virtual da tela (a altura é sempre 1080).</summary>
        public static float W { get; private set; } = 1920f;
        public static float H { get; private set; } = 1080f;

        public static GUIStyle Title, Subtitle, Text, Small, Center, Button, BigButton, Selected, Field, Tiny;
        private static Texture2D _white;

        public static void Begin()
        {
            Init();
            Scale = Screen.height / RefH;
            if (Screen.width / Scale < 1280f) Scale = Screen.width / 1280f; // telas muito estreitas
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
            W = Screen.width / Scale;
            H = Screen.height / Scale;
        }

        private static void Init()
        {
            if (_white != null) return;
            _white = Texture2D.whiteTexture;
            Text = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = true };
            Text.normal.textColor = Color.white;
            Small = new GUIStyle(Text) { fontSize = 20 };
            Tiny = new GUIStyle(Text) { fontSize = 17 };
            Center = new GUIStyle(Text) { alignment = TextAnchor.MiddleCenter };
            Title = new GUIStyle(Text) { fontSize = 96, fontStyle = FontStyle.Bold, wordWrap = false };
            Subtitle = new GUIStyle(Text) { fontSize = 40, fontStyle = FontStyle.Bold };
            Button = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            BigButton = new GUIStyle(Button) { fontSize = 34, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(30, 20, 10, 10) };
            Selected = new GUIStyle(Button);
            Selected.normal.textColor = Palette.Gold;
            Selected.hover.textColor = Palette.Gold;
            Field = new GUIStyle(GUI.skin.textField) { fontSize = 28, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 6, 6) };
        }

        // ---------------------------------------------------------------- desenho básico

        public static void Box(Rect r, Color c)
        {
            var before = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = before;
        }

        public static void Border(Rect r, Color c, float t)
        {
            Box(new Rect(r.x, r.y, r.width, t), c);
            Box(new Rect(r.x, r.yMax - t, r.width, t), c);
            Box(new Rect(r.x, r.y, t, r.height), c);
            Box(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        public static void Panel(Rect r, float alpha = 0.6f) => Box(r, new Color(0.05f, 0.04f, 0.06f, alpha));

        public static void Label(Rect r, string text, GUIStyle style, Color? color = null)
        {
            if (color.HasValue)
            {
                var before = style.normal.textColor;
                style.normal.textColor = color.Value;
                GUI.Label(r, text, style);
                style.normal.textColor = before;
            }
            else GUI.Label(r, text, style);
        }

        /// <summary>Texto com sombra (título do jogo, nomes sobre os personagens).</summary>
        public static void Shadowed(Rect r, string text, GUIStyle style, Color color)
        {
            Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, style, new Color(0, 0, 0, 0.6f));
            Label(r, text, style, color);
        }

        // ---------------------------------------------------------------- controles de formulário

        /// <summary>Linha "Rótulo  ◀ valor ▶" para escolher entre opções. Devolve o novo índice.</summary>
        public static int Selector(Rect r, string label, int index, int count, Func<int, string> name)
        {
            float lw = r.width * 0.42f;
            GUI.Label(new Rect(r.x, r.y, lw, r.height), label, Text);
            if (GUI.Button(new Rect(r.x + lw, r.y, r.height, r.height), "<", Button)) index = (index - 1 + count) % count;
            GUI.Label(new Rect(r.x + lw + r.height, r.y, r.width - lw - 2 * r.height, r.height), name(index), Center);
            if (GUI.Button(new Rect(r.xMax - r.height, r.y, r.height, r.height), ">", Button)) index = (index + 1) % count;
            return index;
        }

        public static T Selector<T>(Rect r, string label, T value, Func<T, string> name) where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int i = Array.IndexOf(values, value);
            i = Selector(r, label, i, values.Length, k => name(values[k]));
            return values[i];
        }

        /// <summary>Linha com amostras de cor clicáveis. Devolve o índice escolhido.</summary>
        public static int Swatches(Rect r, string label, int index, Color[] colors)
        {
            float lw = r.width * 0.42f;
            GUI.Label(new Rect(r.x, r.y, lw, r.height), label, Text);
            float size = Mathf.Min(r.height, (r.width - lw) / colors.Length - 6);
            for (int i = 0; i < colors.Length; i++)
            {
                var sr = new Rect(r.x + lw + i * (size + 6), r.y, size, size);
                Box(sr, colors[i]);
                Border(sr, i == index ? Palette.Gold : new Color(0, 0, 0, 0.5f), i == index ? 4 : 2);
                if (GUI.Button(sr, GUIContent.none, GUIStyle.none)) index = i;
            }
            return index;
        }

        public static float Slider(Rect r, string label, float value, float min, float max, string format = "{0:0}%", float mult = 100f)
        {
            float lw = r.width * 0.42f;
            GUI.Label(new Rect(r.x, r.y, lw, r.height), label, Text);
            value = GUI.HorizontalSlider(new Rect(r.x + lw, r.y + r.height / 2 - 6, r.width - lw - 110, 20), value, min, max);
            GUI.Label(new Rect(r.xMax - 100, r.y, 100, r.height), string.Format(format, value * mult), Text);
            return value;
        }

        public static bool Toggle(Rect r, string label, bool value)
        {
            float lw = r.width * 0.42f;
            GUI.Label(new Rect(r.x, r.y, lw, r.height), label, Text);
            if (GUI.Button(new Rect(r.x + lw, r.y, 160, r.height), value ? "Sim" : "Não", value ? Selected : Button)) value = !value;
            return value;
        }
    }
}
