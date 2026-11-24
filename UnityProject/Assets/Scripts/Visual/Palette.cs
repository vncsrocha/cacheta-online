using System.Collections.Generic;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>Cores provisórias e criação de materiais (funciona no pipeline padrão e no URP).</summary>
    public static class Palette
    {
        public static readonly Color[] SkinTones =
        {
            new Color(0.98f, 0.84f, 0.71f), new Color(0.91f, 0.72f, 0.55f), new Color(0.78f, 0.56f, 0.39f),
            new Color(0.62f, 0.42f, 0.27f), new Color(0.45f, 0.29f, 0.18f), new Color(0.30f, 0.19f, 0.12f),
        };

        public static readonly Color[] HairColors =
        {
            new Color(0.08f, 0.06f, 0.05f), new Color(0.35f, 0.2f, 0.1f), new Color(0.72f, 0.5f, 0.25f),
            new Color(0.85f, 0.75f, 0.45f), new Color(0.75f, 0.75f, 0.75f), new Color(0.55f, 0.15f, 0.1f),
            new Color(0.9f, 0.3f, 0.6f), new Color(0.2f, 0.5f, 0.9f),
        };

        public static readonly Color[] ClothColors =
        {
            new Color(0.95f, 0.8f, 0.1f), new Color(0.1f, 0.55f, 0.3f), new Color(0.85f, 0.2f, 0.25f),
            new Color(0.15f, 0.3f, 0.65f), new Color(0.95f, 0.95f, 0.95f), new Color(0.15f, 0.15f, 0.18f),
            new Color(0.95f, 0.45f, 0.15f), new Color(0.6f, 0.3f, 0.75f), new Color(0.35f, 0.75f, 0.85f),
            new Color(0.76f, 0.64f, 0.42f), new Color(0.12f, 0.42f, 0.22f),
        };

        public static readonly Color Felt = new Color(0.07f, 0.36f, 0.2f);
        public static readonly Color Gold = new Color(1f, 0.75f, 0f);

        private static Shader _lit, _unlit, _toon;
        private static bool _toonSearched;

        /// <summary>Shader de contorno + sombreado chapado (Assets/Shaders/ToonOutline.shader). Nulo se não existir.</summary>
        public static Shader Toon
        {
            get
            {
                if (!_toonSearched)
                {
                    _toon = Shader.Find("Cacheta/ToonOutline");
                    if (_toon != null && !_toon.isSupported) _toon = null;
                    _toonSearched = true;
                }
                return _toon;
            }
        }
        private static readonly Dictionary<Color, Material> Cache = new Dictionary<Color, Material>();

        private static Shader Lit
        {
            get
            {
                if (_lit == null)
                    _lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
                return _lit;
            }
        }

        private static Shader Unlit
        {
            get
            {
                if (_unlit == null)
                    _unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Lit;
                return _unlit;
            }
        }

        /// <summary>Material colorido e iluminado (cache por cor).</summary>
        public static Material Mat(Color c)
        {
            if (Cache.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(Toon != null ? Toon : Lit) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            Cache[c] = m;
            return m;
        }

        /// <summary>Material com contorno e textura (estampas de roupa). Cada chamada cria um material novo.</summary>
        public static Material ToonTextured(Texture2D tex, Color tint)
        {
            var m = new Material(Toon != null ? Toon : Lit) { color = tint, mainTexture = tex };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            return m;
        }

        /// <summary>Listras horizontais (ao longo do comprimento de cápsulas e cilindros).</summary>
        public static Texture2D Stripes(Color a, Color b, int bands)
        {
            int h = Mathf.Max(2, bands * 2) * 8;
            var t = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color[4 * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < 4; x++)
                    px[y * 4 + x] = (y / 8) % 2 == 0 ? a : b;
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>Material sem luz com textura (cartas: ficam legíveis em qualquer iluminação).</summary>
        public static Material Textured(Texture2D tex)
        {
            var m = new Material(Unlit) { mainTexture = tex };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            return m;
        }

        /// <summary>Material sem luz de cor sólida (molduras e destaques). Cada chamada cria um material novo.</summary>
        public static Material UnlitColor(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Lit;
            var m = new Material(shader);
            SetColor(m, c);
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        /// <summary>Cria um primitivo colorido, filho de <paramref name="parent"/>, sem colisor.</summary>
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color,
                                      string name = null, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            return go;
        }
    }
}
