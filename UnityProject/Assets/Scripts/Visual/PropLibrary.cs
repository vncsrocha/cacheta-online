using System.Collections.Generic;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Peças 3D dos cenários (geladeira, balcão, luminária...). Cada cenário tem um pacote .glb em
    /// Assets/Resources/Cenarios (baixado pelo Unity, veja BaixarCenarios no Editor) com uma peça por nó, já em metros,
    /// apoiada no chão (y = 0), centrada e com a frente virada para +Z. Sem o pacote, quem chama monta a peça com
    /// formas simples.
    /// O nome do material no .glb liga opções do shader: "__glowN" brilha sozinho com força N (lâmpadas, telas),
    /// "__2s" desenha as duas faces, "__cut" recorta pela transparência da textura, "__nolinha" fica sem contorno.
    /// </summary>
    public static class PropLibrary
    {
        public const string Folder = "Cenarios/";

        private static readonly Dictionary<string, GameObject> Packs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<Material, Material> Converted = new Dictionary<Material, Material>();

        private static GameObject Pack(string pack)
        {
            if (Packs.TryGetValue(pack, out var go)) return go;
            go = Resources.Load<GameObject>(Folder + pack);
            Packs[pack] = go;
            return go;
        }

        private static Transform Source(string pack, string prop)
        {
            var p = Pack(pack);
            return p == null ? null : ModelAvatar.FindDeep(p.transform, prop);
        }

        public static bool Has(string pack, string prop) => Source(pack, prop) != null;

        /// <summary>
        /// Põe uma cópia da peça dentro de <paramref name="parent"/>, com a base em <paramref name="pos"/>, girada
        /// <paramref name="yaw"/> graus em volta do eixo vertical e no tamanho do modelo vezes <paramref name="scale"/>.
        /// Nulo se o pacote não foi baixado ou não tem essa peça.
        /// </summary>
        public static GameObject Spawn(Transform parent, string pack, string prop, Vector3 pos, float yaw = 0f, float scale = 1f,
                                       bool shadows = true, string name = null)
            => Spawn(parent, pack, prop, pos, Quaternion.Euler(0, yaw, 0), Vector3.one * scale, shadows, name);

        public static GameObject Spawn(Transform parent, string pack, string prop, Vector3 pos, Quaternion rot, Vector3 scale,
                                       bool shadows = true, string name = null)
        {
            var src = Source(pack, prop);
            if (src == null) return null;
            var go = Object.Instantiate(src.gameObject, parent, false);
            go.name = name ?? prop;
            go.SetActive(true);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Toon(mats[i]);
                r.sharedMaterials = mats;
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        /// <summary>Acha uma parte da peça pelo nome do nó (por exemplo, as pás do ventilador, para girar).</summary>
        public static Transform Part(GameObject prop, string node) => prop == null ? null : ModelAvatar.FindDeep(prop.transform, node);

        /// <summary>Troca o material importado pelo de contorno, mantendo a textura (um por material de origem).</summary>
        private static Material Toon(Material src)
        {
            if (src == null || Palette.Toon == null) return src;
            if (Converted.TryGetValue(src, out var m) && m != null) return m;

            var tex = ModelAvatar.BaseTexture(src);
            Color tint = Color.white;
            if (src.HasProperty("baseColorFactor")) tint = src.GetColor("baseColorFactor");
            else if (src.HasProperty("_BaseColor")) tint = src.GetColor("_BaseColor");
            else if (src.HasProperty("_Color")) tint = src.color;
            m = new Material(Palette.Toon) { name = src.name + " (toon)", color = tint, mainTexture = tex };
            m.SetFloat("_OutlineWidth", 1.6f);

            string n = src.name ?? "";
            int glow = n.IndexOf("__glow", System.StringComparison.Ordinal);
            if (glow >= 0)
            {
                float k = 1.5f;
                int at = glow + 6, end = at;
                while (end < n.Length && (char.IsDigit(n[end]) || n[end] == '.')) end++;
                if (end > at) float.TryParse(n.Substring(at, end - at), System.Globalization.NumberStyles.Float,
                                             System.Globalization.CultureInfo.InvariantCulture, out k);
                m.SetColor("_EmissionColor", tint * k);
                if (tex != null) m.SetTexture("_EmissionMap", tex);
                m.SetFloat("_OutlineWidth", 0f);
            }
            if (n.Contains("__cut"))
            {
                m.SetFloat("_Cutoff", 0.5f);
                m.renderQueue = 2450;
            }
            if (n.Contains("__2s") || n.Contains("__cut"))
            {
                m.SetFloat("_Cull", 0f);
                m.SetFloat("_OutlineWidth", 0f);
            }
            if (n.Contains("__nolinha")) m.SetFloat("_OutlineWidth", 0f);

            Converted[src] = m;
            return m;
        }
    }

    /// <summary>Gira uma peça sem parar (pás do ventilador de teto).</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float DegreesPerSecond = 160f;
        private void Update() => transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
