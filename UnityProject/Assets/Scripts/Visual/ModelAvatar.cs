using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Personagens com modelo 3D pronto (arquivos .glb em Assets/Resources/Personagens, importados pelo glTFast).
    /// Troca os materiais pelo shader de contorno mantendo a textura, ajusta a altura e liga a animação
    /// de sentado só na mesa. Se o modelo não existir, quem chama monta o personagem de formas simples.
    /// </summary>
    public static class ModelAvatar
    {
        public const string Folder = "Personagens/";

        /// <summary>Altura no jogo de uma pessoa de 1,75 m (mesma escala do personagem de formas simples e da mesa).</summary>
        public const float StandingHeight = 1.95f;
        public const float ReferenceHeight = 1.75f;

        public static bool Exists(string model)
            => !string.IsNullOrEmpty(model) && Resources.Load<GameObject>(Folder + model) != null;

        /// <summary>Instancia o modelo dentro de <paramref name="root"/>. Devolve false se não houver modelo.</summary>
        public static bool TryBuild(Transform root, string model, bool seated, float heightMeters = ReferenceHeight)
        {
            if (string.IsNullOrEmpty(model)) return false;
            var prefab = Resources.Load<GameObject>(Folder + model);
            if (prefab == null) return false;

            var go = Object.Instantiate(prefab, root, false);
            go.name = model;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            ToonMaterials(go);
            FitHeight(go, StandingHeight * (heightMeters > 0.5f ? heightMeters : ReferenceHeight) / ReferenceHeight);

            if (seated) Sit(go, model);
            else
            {
                var anim = go.GetComponentInChildren<Animation>();
                if (anim != null) { anim.playAutomatically = false; anim.Stop(); }
            }
            return true;
        }

        /// <summary>Toca o "sentado parado" e encaixa o quadril no assento da cadeira.</summary>
        private static void Sit(GameObject go, string model)
        {
            AnimationClip clip = null;
            var anim = go.GetComponentInChildren<Animation>();
            if (anim != null && anim.clip != null) clip = anim.clip;
            if (clip == null)
            {
                var clips = Resources.LoadAll<AnimationClip>(Folder + model);
                if (clips.Length > 0) clip = clips[0];
            }
            if (clip == null) return;

            // Pose da primeira imagem da animação, para medir onde o quadril fica
            clip.SampleAnimation(go, 0f);
            var hips = FindDeep(go.transform, "Hips");
            if (hips != null)
            {
                var target = go.transform.parent.TransformPoint(SeatHips);
                go.transform.position += target - hips.position;
            }

            if (clip.legacy)
            {
                if (anim == null) anim = go.AddComponent<Animation>();
                if (anim.GetClip(clip.name) == null) anim.AddClip(clip, clip.name);
                anim.clip = clip;
                anim.wrapMode = WrapMode.Loop;
                anim.Play(clip.name);
            }
            else
            {
                var animator = go.GetComponentInChildren<Animator>();
                if (animator == null) animator = go.AddComponent<Animator>();
                animator.applyRootMotion = false;
                UnityEngine.Playables.PlayableGraph graph;
                UnityEngine.Playables.AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
                go.AddComponent<GraphCleanup>().Graph = graph;
            }
        }

        /// <summary>Onde o quadril fica, em relação ao lugar do personagem (assento da cadeira).</summary>
        private static readonly Vector3 SeatHips = new Vector3(0f, 0.56f, -0.02f);

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Libera o PlayableGraph quando o personagem é destruído.</summary>
        private sealed class GraphCleanup : MonoBehaviour
        {
            public UnityEngine.Playables.PlayableGraph Graph;
            private void OnDestroy() { if (Graph.IsValid()) Graph.Destroy(); }
        }

        private static void ToonMaterials(GameObject go)
        {
            if (Palette.Toon == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    Texture tex = null;
                    if (src.HasProperty("baseColorTexture")) tex = src.GetTexture("baseColorTexture");
                    if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
                    if (tex == null && src.HasProperty("_MainTex")) tex = src.mainTexture;
                    Color tint = Color.white;
                    if (src.HasProperty("baseColorFactor")) tint = src.GetColor("baseColorFactor");
                    else if (src.HasProperty("_BaseColor")) tint = src.GetColor("_BaseColor");
                    else if (src.HasProperty("_Color")) tint = src.color;
                    var m = new Material(Palette.Toon) { color = tint, mainTexture = tex };
                    m.SetFloat("_OutlineWidth", 2.5f);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
        }

        /// <summary>Escala para a altura padrão e apoia os pés no chão (y = 0 do pai).</summary>
        private static void FitHeight(GameObject go, float height)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            if (b.size.y < 0.01f) return;
            float k = height / b.size.y;
            go.transform.localScale *= k;
            // recalcula depois da escala
            b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var p = go.transform.position;
            go.transform.position = new Vector3(p.x, p.y - (b.min.y - go.transform.parent.position.y), p.z);
        }
    }
}
