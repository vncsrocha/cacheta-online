using System.Collections.Generic;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>Malhas feitas por código para móveis que os primitivos do Unity não fazem bem (peças torneadas).</summary>
    public static class MeshKit
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>
        /// Gira um perfil (x = raio, y = altura) em volta do eixo Y. O lado de fora da peça fica à esquerda de quem
        /// anda pelo perfil no plano raio-altura (por exemplo: tampo do centro para fora, borda de cima para baixo,
        /// fundo de fora para o centro). Repetir um ponto deixa uma quina viva ali.
        /// </summary>
        public static Mesh Lathe(string key, Vector2[] profile, int segments = 48)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int n = profile.Length, ring = segments + 1;
            var verts = new Vector3[n * ring];
            var uv = new Vector2[n * ring];
            for (int k = 0; k < n; k++)
                for (int j = 0; j < ring; j++)
                {
                    float a = j * Mathf.PI * 2f / segments;
                    verts[k * ring + j] = new Vector3(profile[k].x * Mathf.Cos(a), profile[k].y, profile[k].x * Mathf.Sin(a));
                    uv[k * ring + j] = new Vector2(j / (float)segments, k / (float)(n - 1));
                }
            var tris = new List<int>();
            for (int k = 0; k < n - 1; k++)
            {
                if ((profile[k] - profile[k + 1]).sqrMagnitude < 1e-10f) continue; // quina: sem faces entre os dois
                for (int j = 0; j < segments; j++)
                {
                    int a = k * ring + j, b = (k + 1) * ring + j, c = (k + 1) * ring + j + 1, d = k * ring + j + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(a); tris.Add(d); tris.Add(c);
                }
            }
            var mesh = new Mesh { name = key, vertices = verts, uv = uv, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Caixa de cantos arredondados (superelipsoide) de tamanho 1, centrada na origem. <paramref name="round"/> = 1
        /// é uma esfera e, quanto mais perto de 0, mais parece um cubo. Serve para palmas, unhas e peças macias.
        /// </summary>
        public static Mesh RoundBox(float round = 0.45f, int segments = 24)
        {
            string key = "roundbox " + round.ToString("0.00") + " " + segments;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int rings = segments / 2, ring = segments + 1;
            var verts = new Vector3[(rings + 1) * ring];
            var normals = new Vector3[verts.Length];
            for (int i = 0; i <= rings; i++)
            {
                float lat = Mathf.PI * i / rings - Mathf.PI / 2f;
                for (int j = 0; j <= segments; j++)
                {
                    float lon = 2f * Mathf.PI * j / segments;
                    var d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    verts[i * ring + j] = new Vector3(Pow(d.x, round), Pow(d.y, round), Pow(d.z, round)) * 0.5f;
                    // normal da superfície |x|^(2/e) + |y|^(2/e) + |z|^(2/e) = 1 nesse ponto
                    normals[i * ring + j] = new Vector3(Pow(d.x, 2f - round), Pow(d.y, 2f - round), Pow(d.z, 2f - round)).normalized;
                }
            }
            var tris = new List<int>();
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = i * ring + j, b = a + ring;
                    tris.Add(a); tris.Add(b); tris.Add(b + 1);
                    tris.Add(a); tris.Add(b + 1); tris.Add(a + 1);
                }
            var mesh = new Mesh { name = key, vertices = verts, normals = normals, triangles = tris.ToArray() };
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        private static float Pow(float v, float e) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), e);

        /// <summary>Cria um objeto com a malha e a cor (material de contorno do jogo).</summary>
        public static GameObject Piece(Mesh mesh, Transform parent, Vector3 localPos, Color color, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Palette.Mat(color);
            return go;
        }
    }
}
