using System.Collections.Generic;
using Cacheta.Core;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Gera as texturas provisórias das cartas por código (fonte em bitmap própria), sem depender de fontes
    /// nem de assets. Quando o desenho oficial das cartas existir, basta trocar <see cref="Face"/> e <see cref="Back"/>.
    /// </summary>
    public static class CardTextures
    {
        public const int W = 128, H = 192;

        private static readonly Dictionary<int, Texture2D> Faces = new Dictionary<int, Texture2D>();
        private static Texture2D _back;

        // Glifos 5x7, linha de cima primeiro.
        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##.." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
        };

        // Naipes 9x9.
        private static readonly Dictionary<Suit, string[]> SuitGlyphs = new Dictionary<Suit, string[]>
        {
            [Suit.Copas] = new[] { ".##...##.", "####.####", "#########", "#########", ".#######.", "..#####..", "...###...", "....#....", "........." },
            [Suit.Ouros] = new[] { "....#....", "...###...", "..#####..", ".#######.", "#########", ".#######.", "..#####..", "...###...", "....#...." },
            [Suit.Espadas] = new[] { "....#....", "...###...", "..#####..", ".#######.", "#########", "#########", ".##.#.##.", "....#....", "...###..." },
            [Suit.Paus] = new[] { "...###...", "...###...", ".#.###.#.", "#########", "#########", ".#.#.#.#.", "....#....", "....#....", "...###..." },
        };

        public static Texture2D Face(Card c)
        {
            int key = c.Rank * 4 + (int)c.Suit; // as duas cópias do baralho usam a mesma textura
            if (Faces.TryGetValue(key, out var t) && t != null) return t;

            var px = NewPixels(Color.white);
            var ink = c.Suit.IsRed() ? new Color(0.82f, 0.1f, 0.12f) : new Color(0.08f, 0.08f, 0.1f);
            Frame(px, new Color(0.75f, 0.75f, 0.75f), 3);

            string label = c.RankLabel;
            // Canto superior esquerdo: valor e naipe
            DrawText(px, label, 10, 10, 4, ink);
            DrawGlyph(px, SuitGlyphs[c.Suit], 10, 44, 3, ink);
            // Canto inferior direito (espelhado de posição, não de desenho, para ficar simples)
            int labelW = label.Length * 6 * 4 - 4;
            DrawText(px, label, W - 10 - labelW, H - 10 - 28, 4, ink);
            DrawGlyph(px, SuitGlyphs[c.Suit], W - 10 - 27, H - 10 - 28 - 34, 3, ink);
            // Centro: naipe grande
            DrawGlyph(px, SuitGlyphs[c.Suit], (W - 63) / 2, (H - 63) / 2 + 6, 7, ink);
            // Figuras ganham uma moldura colorida provisória
            if (c.Rank >= 11) FrameRect(px, 30, 52, W - 60, H - 96, Palette.Gold, 3);

            t = Build(px, $"carta_{c}");
            Faces[key] = t;
            return t;
        }

        public static Texture2D Back
        {
            get
            {
                if (_back != null) return _back;
                var px = NewPixels(new Color(0.55f, 0.08f, 0.12f));
                var gold = new Color(1f, 0.8f, 0.4f);
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (((x + y) % 16 == 0 || (x - y + 1024) % 16 == 0) && x > 12 && x < W - 12 && y > 12 && y < H - 12)
                            px[(H - 1 - y) * W + x] = Color.Lerp(px[(H - 1 - y) * W + x], gold, 0.5f);
                Frame(px, Color.white, 6);
                FrameRect(px, 12, 12, W - 24, H - 24, gold, 2);
                _back = Build(px, "carta_verso");
                return _back;
            }
        }

        // ---------------------------------------------------------------- pixels (coordenadas com y para baixo)

        private static Color[] NewPixels(Color fill)
        {
            var px = new Color[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
            return px;
        }

        private static void Set(Color[] px, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            px[(H - 1 - y) * W + x] = c;
        }

        private static void FillRect(Color[] px, int x, int y, int w, int h, Color c)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    Set(px, x + i, y + j, c);
        }

        private static void FrameRect(Color[] px, int x, int y, int w, int h, Color c, int t)
        {
            FillRect(px, x, y, w, t, c);
            FillRect(px, x, y + h - t, w, t, c);
            FillRect(px, x, y, t, h, c);
            FillRect(px, x + w - t, y, t, h, c);
        }

        private static void Frame(Color[] px, Color c, int t) => FrameRect(px, 0, 0, W, H, c, t);

        private static void DrawGlyph(Color[] px, string[] rows, int x, int y, int scale, Color c)
        {
            for (int r = 0; r < rows.Length; r++)
                for (int k = 0; k < rows[r].Length; k++)
                    if (rows[r][k] == '#') FillRect(px, x + k * scale, y + r * scale, scale, scale, c);
        }

        private static void DrawText(Color[] px, string text, int x, int y, int scale, Color c)
        {
            foreach (char ch in text)
            {
                if (Glyphs.TryGetValue(ch, out var g)) DrawGlyph(px, g, x, y, scale, c);
                x += 6 * scale;
            }
        }

        private static Texture2D Build(Color[] px, string name)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }
    }
}
