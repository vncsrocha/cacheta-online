using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace Cacheta.EditorTools
{
    /// <summary>
    /// Baixa os modelos 3D dos personagens (.glb) para Assets/Resources/Personagens. Roda sozinho quando o
    /// Unity abre ou recompila e só baixa o que falta ou mudou de versão; o menu Carteado > Baixar personagens 3D
    /// baixa tudo de novo. Os .glb não vão para o git (são grandes); cada máquina baixa os seus.
    /// </summary>
    [InitializeOnLoad]
    public static class BaixarPersonagens
    {
        private const string Pasta = "Assets/Resources/Personagens";
        // Arquivo começando com ponto: o Unity ignora (não cria .meta).
        private const string Registro = Pasta + "/.baixados.txt";

        /// <summary>Nome do arquivo (campo Model do perfil) e de onde baixar. Trocar a URL faz baixar a versão nova.</summary>
        private static readonly (string Nome, string Url)[] Modelos =
        {
            ("TioBeto", "https://d8j0ntlcm91z4.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/hf_20261125_034857_552bfbe1-d25b-4bbe-b825-41527af54a80.glb"),
            ("DonaCleide", "https://d8j0ntlcm91z4.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/hf_20261125_034858_9f7f46e0-6d78-4384-9432-92e35c70bf84.glb"),
            ("Marcela", "https://d8j0ntlcm91z4.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/hf_20261125_034859_a331e246-ae62-4114-96cb-22e3731bfbae.glb"),
            ("Ramiro", "https://d8j0ntlcm91z4.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/hf_20261125_034900_0a1934d0-d929-4db4-8c31-5d10cce6ba3c.glb"),
            ("Marquinho", "https://d8j0ntlcm91z4.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/hf_20261125_034901_95190428-d75f-4d97-aa12-4b4b6ff114bf.glb"),
            ("Ana", "https://d8j0ntlcm91z4.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/hf_20261125_034902_42ec6bfe-1600-45bf-9f03-a379577e375f.glb"),
            ("LittleTiger", "https://d8j0ntlcm91z4.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/hf_20261125_034903_c998ad22-4314-4dba-82f0-63b638bbc74a.glb"),
            ("LittleTigerRevelado", "https://d8j0ntlcm91z4.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/hf_20261125_034904_0a53b84c-e7aa-427f-a177-4f1f8f483bc4.glb"),
        };

        /// <summary>
        /// Sobe este número quando os .glb já baixados precisam ser importados de novo (por exemplo, quando faltava
        /// o módulo Image Conversion e o glTFast importou os modelos sem textura).
        /// </summary>
        private const string VersaoImportacao = "2";

        private static bool _rodando;

        static BaixarPersonagens()
        {
            EditorApplication.delayCall += () => Baixar(forcar: false);
        }

        [MenuItem("Carteado/Baixar personagens 3D")]
        private static void BaixarTudo() => Baixar(forcar: true);

        [MenuItem("Carteado/Reimportar personagens 3D")]
        private static void Reimportar()
        {
            int n = 0;
            foreach (var m in Modelos)
            {
                if (!File.Exists(Caminho(m.Nome))) continue;
                AssetDatabase.ImportAsset(Caminho(m.Nome).Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
                n++;
            }
            var reg = LerRegistro();
            reg["_importacao"] = VersaoImportacao;
            SalvarRegistro(reg);
            if (n > 0) Debug.Log($"[Carteado] {n} personagem(ns) 3D importado(s) de novo.");
        }

        private static string Caminho(string nome) => Path.Combine(Pasta, nome + ".glb");

        private static async void Baixar(bool forcar)
        {
            if (_rodando || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var reg = LerRegistro();
            var faltam = Modelos.Where(m => forcar || !File.Exists(Caminho(m.Nome))
                                            || !reg.TryGetValue(m.Nome, out var url) || url != m.Url).ToList();
            if (faltam.Count == 0)
            {
                // Já baixados, mas importados por uma versão antiga do projeto: importa de novo.
                if (!reg.TryGetValue("_importacao", out var v) || v != VersaoImportacao) Reimportar();
                return;
            }

            _rodando = true;
            int ok = 0;
            try
            {
                Directory.CreateDirectory(Pasta);
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    for (int i = 0; i < faltam.Count; i++)
                    {
                        var m = faltam[i];
                        EditorUtility.DisplayProgressBar("Carteado", $"Baixando personagem {m.Nome} ({i + 1} de {faltam.Count})", i / (float)faltam.Count);
                        try
                        {
                            var bytes = await http.GetByteArrayAsync(m.Url);
                            File.WriteAllBytes(Caminho(m.Nome), bytes);
                            reg[m.Nome] = m.Url;
                            ok++;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[Carteado] Não consegui baixar {m.Nome}.glb: {e.Message}");
                        }
                    }
                }
                reg["_importacao"] = VersaoImportacao;
                SalvarRegistro(reg);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _rodando = false;
                if (ok > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.Log($"[Carteado] {ok} personagem(ns) 3D baixado(s) em {Pasta}.");
                }
            }
        }

        private static Dictionary<string, string> LerRegistro()
        {
            var d = new Dictionary<string, string>();
            if (!File.Exists(Registro)) return d;
            foreach (var linha in File.ReadAllLines(Registro))
            {
                int i = linha.IndexOf('=');
                if (i > 0) d[linha.Substring(0, i)] = linha.Substring(i + 1);
            }
            return d;
        }

        private static void SalvarRegistro(Dictionary<string, string> d)
            => File.WriteAllLines(Registro, d.Select(kv => kv.Key + "=" + kv.Value));
    }
}
