using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// Cadeira de plástico monobloco (a branca de bar), modelo gratuito do Poly Haven (CC0, "Plastic Monobloc Chair
    /// 01", de Kuutti Siitonen). O Unity baixa a malha sozinho para Assets/Resources/Moveis/CadeiraMonobloco.bytes
    /// (veja BaixarMoveis no Editor); sem o arquivo, a mesa usa a cadeira de formas simples.
    /// A malha vem do .bin do glTF: posições, normais, UV e índices, nesta ordem, sem textura (a cor é nossa).
    /// </summary>
    public static class Monobloco
    {
        public const string Resource = "Moveis/CadeiraMonobloco";

        // Layout do .bin (glTF do Poly Haven, 1 malha só).
        private const int Verts = 3271, Indices = 10068;
        private const int NormalsAt = 39252, IndicesAt = 104672, Length = 124808;
        /// <summary>Altura do assento no modelo original (m).</summary>
        public const float ModelSeat = 0.45f;

        private static Mesh _mesh;
        private static bool _tried;

        public static bool Available => GetMesh() != null;

        private static Mesh GetMesh()
        {
            if (_tried) return _mesh;
            _tried = true;
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null) return null;
            var b = asset.bytes;
            if (b.Length != Length)
            {
                Debug.LogWarning($"[Carteado] {Resource} tem {b.Length} bytes, esperava {Length}. Usando a cadeira simples.");
                return null;
            }

            // glTF é mão direita; o Unity é mão esquerda: espelha o X e inverte a ordem dos triângulos.
            var pos = new Vector3[Verts];
            var nor = new Vector3[Verts];
            for (int i = 0; i < Verts; i++)
            {
                pos[i] = new Vector3(-F(b, i * 12), F(b, i * 12 + 4), F(b, i * 12 + 8));
                nor[i] = new Vector3(-F(b, NormalsAt + i * 12), F(b, NormalsAt + i * 12 + 4), F(b, NormalsAt + i * 12 + 8));
            }
            var tri = new int[Indices];
            for (int t = 0; t < Indices; t += 3)
            {
                tri[t] = U(b, IndicesAt + t * 2);
                tri[t + 1] = U(b, IndicesAt + (t + 2) * 2);
                tri[t + 2] = U(b, IndicesAt + (t + 1) * 2);
            }
            _mesh = new Mesh { name = "Cadeira monobloco", vertices = pos, normals = nor, triangles = tri };
            _mesh.RecalculateBounds();
            return _mesh;
        }

        private static float F(byte[] b, int at) => System.BitConverter.ToSingle(b, at);
        private static int U(byte[] b, int at) => b[at] | (b[at + 1] << 8);

        /// <summary>
        /// Cria a cadeira com o assento na altura pedida. A frente (sem encosto) fica em +Z local. Nulo se o modelo não
        /// foi baixado.
        /// </summary>
        public static GameObject Build(Transform parent, Color color, float seatHeight, string name = "Cadeira")
        {
            var mesh = GetMesh();
            if (mesh == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * (seatHeight / ModelSeat);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(color);
            return go;
        }
    }
}
