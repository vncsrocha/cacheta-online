using UnityEngine;

namespace Cacheta.App
{
    /// <summary>Ajustes do pós-efeito de um cenário (cada cenário escolhe os seus; sem ajuste, a imagem sai como está).</summary>
    public struct PostLook
    {
        /// <summary>Força do brilho estourado em volta das lâmpadas, telas e vidros acesos.</summary>
        public float Bloom;
        /// <summary>A partir de que brilho a cor vaza (1 = só o que passa do branco, como lâmpadas com brilho próprio).</summary>
        public float Threshold;
        public Color BloomTint;
        public float Exposure;
        /// <summary>Multiplica a cor final: um pouco mais de vermelho e menos azul esquenta a cena.</summary>
        public Color WhiteBalance;
        public float Saturation;
        public float Contrast;
        /// <summary>Escurecimento nos cantos (0 a 1) e a cor dele.</summary>
        public float Vignette;
        public Color VignetteColor;

        /// <summary>Boteco à tarde: lâmpadas que brilham, cor quente, cantos puxando para o marrom.</summary>
        public static PostLook Boteco => new PostLook
        {
            Bloom = 0.4f, Threshold = 1.15f, BloomTint = new Color(1f, 0.9f, 0.72f), Exposure = 0.9f,
            WhiteBalance = new Color(1.07f, 1.0f, 0.84f), Saturation = 1.12f, Contrast = 1.1f,
            Vignette = 0.5f, VignetteColor = new Color(0.45f, 0.28f, 0.16f),
        };
    }

    /// <summary>
    /// Pós-efeito da câmera no pipeline padrão (shader Hidden/Cacheta/PostFx): bloom, curva suave nos claros, cor e
    /// vinheta. O cenário liga com <see cref="Apply"/>; sem o shader (ou sem suporte), a imagem passa direto.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PostFx : MonoBehaviour
    {
        // Quatro reduções: o brilho fica perto da lâmpada (com mais, vira uma mancha larga no teto).
        private const int MaxMips = 4;
        private PostLook _look;
        private Material _mat;
        private readonly RenderTexture[] _mips = new RenderTexture[MaxMips];

        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int BloomTintId = Shader.PropertyToID("_BloomTint");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int ShoulderId = Shader.PropertyToID("_Shoulder");
        private static readonly int WhiteBalanceId = Shader.PropertyToID("_WhiteBalance");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int VignetteColorId = Shader.PropertyToID("_VignetteColor");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");

        /// <summary>Liga o pós-efeito na câmera com estes ajustes, ou desliga (null).</summary>
        public static void Apply(Camera cam, PostLook? look)
        {
            if (cam == null) return;
            var fx = cam.GetComponent<PostFx>();
            if (look == null)
            {
                if (fx != null) fx.enabled = false;
                return;
            }
            if (fx == null) fx = cam.gameObject.AddComponent<PostFx>();
            fx._look = look.Value;
            fx.enabled = true;
            // As lâmpadas e telas passam do branco (HDR); é isso que o bloom pega.
            cam.allowHDR = true;
        }

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null)
            {
                var shader = Shader.Find("Hidden/Cacheta/PostFx");
                if (shader == null || !shader.isSupported)
                {
                    Graphics.Blit(src, dst);
                    enabled = false;
                    return;
                }
                _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            var l = _look;
            float knee = Mathf.Max(0.05f, l.Threshold * 0.5f);
            _mat.SetVector(ThresholdId, new Vector4(l.Threshold, knee, 2f * knee, 0.25f / knee));
            _mat.SetFloat(IntensityId, l.Bloom);
            // As cores daqui vão como números (SetVector), não como cor: assim o Unity não as converte para o espaço
            // linear e o ajuste fica igual no projeto em Gamma ou em Linear (o acerto de cor é feito em gama).
            _mat.SetVector(BloomTintId, l.BloomTint);
            _mat.SetFloat(ExposureId, l.Exposure <= 0 ? 1f : l.Exposure);
            _mat.SetFloat(ShoulderId, 0.8f);
            _mat.SetVector(WhiteBalanceId, l.WhiteBalance.maxColorComponent <= 0 ? Color.white : l.WhiteBalance);
            _mat.SetFloat(SaturationId, l.Saturation <= 0 ? 1f : l.Saturation);
            _mat.SetFloat(ContrastId, l.Contrast <= 0 ? 1f : l.Contrast);
            _mat.SetFloat(VignetteId, l.Vignette);
            _mat.SetVector(VignetteColorId, l.VignetteColor);

            // Bloom: reduz a imagem pela metade algumas vezes (a primeira já separa só o que brilha) e volta somando.
            var format = src.format;
            int w = src.width / 2, h = src.height / 2, n = 0;
            RenderTexture cur = src;
            for (int i = 0; i < MaxMips && w >= 8 && h >= 8; i++, n++)
            {
                _mips[i] = RenderTexture.GetTemporary(w, h, 0, format);
                _mips[i].filterMode = FilterMode.Bilinear;
                Graphics.Blit(cur, _mips[i], _mat, i == 0 ? 0 : 1);
                cur = _mips[i];
                w /= 2; h /= 2;
            }
            for (int i = n - 2; i >= 0; i--)
                Graphics.Blit(_mips[i + 1], _mips[i], _mat, 2);

            if (n > 0) _mat.SetTexture(BloomTexId, _mips[0]);
            else _mat.SetFloat(IntensityId, 0f);
            Graphics.Blit(src, dst, _mat, 3);

            for (int i = 0; i < n; i++)
            {
                RenderTexture.ReleaseTemporary(_mips[i]);
                _mips[i] = null;
            }
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
        }
    }
}
