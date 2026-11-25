using System;
using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace Cacheta.EditorTools
{
    /// <summary>
    /// Baixa os móveis 3D gratuitos (CC0) para Assets/Resources/Moveis. Roda sozinho quando o Unity abre e só baixa o
    /// que falta; o menu Carteado > Baixar móveis 3D baixa de novo. Os arquivos não vão para o git.
    /// </summary>
    [InitializeOnLoad]
    public static class BaixarMoveis
    {
        private const string Pasta = "Assets/Resources/Moveis";

        /// <summary>Arquivo (com extensão .bytes, que o Unity lê como TextAsset), tamanho esperado e de onde baixar.</summary>
        private static readonly (string Arquivo, int Bytes, string Url)[] Moveis =
        {
            // Poly Haven, "Plastic Monobloc Chair 01" (Kuutti Siitonen, CC0): só a malha do glTF, sem as texturas.
            ("CadeiraMonobloco.bytes", 124808, "https://dl.polyhaven.org/file/ph-assets/Models/gltf/4k/plastic_monobloc_chair_01/plastic_monobloc_chair_01.bin"),
        };

        private static bool _rodando;

        static BaixarMoveis()
        {
            EditorApplication.delayCall += () => Baixar(forcar: false);
        }

        [MenuItem("Carteado/Baixar móveis 3D")]
        private static void BaixarTudo() => Baixar(forcar: true);

        private static async void Baixar(bool forcar)
        {
            if (_rodando || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _rodando = true;
            int ok = 0;
            try
            {
                Directory.CreateDirectory(Pasta);
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) })
                {
                    foreach (var m in Moveis)
                    {
                        var caminho = Path.Combine(Pasta, m.Arquivo);
                        if (!forcar && File.Exists(caminho) && new FileInfo(caminho).Length == m.Bytes) continue;
                        try
                        {
                            var bytes = await http.GetByteArrayAsync(m.Url);
                            if (bytes.Length != m.Bytes)
                            {
                                Debug.LogWarning($"[Carteado] {m.Arquivo} veio com {bytes.Length} bytes (esperava {m.Bytes}); não salvei.");
                                continue;
                            }
                            File.WriteAllBytes(caminho, bytes);
                            ok++;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[Carteado] Não consegui baixar {m.Arquivo}: {e.Message}");
                        }
                    }
                }
            }
            finally
            {
                _rodando = false;
                if (ok > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.Log($"[Carteado] {ok} móvel(is) 3D baixado(s) em {Pasta}.");
                }
            }
        }
    }
}
