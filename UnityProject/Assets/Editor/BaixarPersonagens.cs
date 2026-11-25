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

        /// <summary>
        /// Nome do arquivo (campo Model do perfil) e de onde baixar. Trocar a URL faz baixar a versão nova.
        /// Versão de 25/11/2026: os mesmos modelos com ossos nos dedos (3 por dedo, 30 a mais por personagem), para as
        /// mãos fecharem nas cartas; a Ana ganhou mãos novas (as do modelo original vieram fundidas).
        /// </summary>
        private static readonly (string Nome, string Url)[] Modelos =
        {
            ("TioBeto", "https://d2ol7oe51mr4n9.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/749a04c6-b4d4-42bb-a374-4f5f281272ae.glb"),
            ("DonaCleide", "https://d2ol7oe51mr4n9.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/9567ac2b-22c7-456a-9f84-fc859a544901.glb"),
            ("Marcela", "https://d2ol7oe51mr4n9.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/2ae3b899-cb0b-4f77-ad38-30bf1fffb221.glb"),
            ("Ramiro", "https://d2ol7oe51mr4n9.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/3f05b2fe-cabd-442d-8981-1161a2fcc2cd.glb"),
            ("Marquinho", "https://d2ol7oe51mr4n9.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/b7eda6cf-3287-4f81-b590-140c26f45a68.glb"),
            ("Ana", "https://d2ol7oe51mr4n9.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/57b688fe-72b7-43d7-a671-509f289a6f60.glb"),
            ("LittleTiger", "https://d2ol7oe51mr4n9.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/66a8efeb-e851-481c-a37e-f7c21f7a1b8e.glb"),
            ("LittleTigerRevelado", "https://d2ol7oe51mr4n9.cloudfront.net/user_33DfferL9wsklzrcrzf0ZWRxQMf/ca667b23-0625-4c6c-9b4e-3ebbc4662457.glb"),
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
