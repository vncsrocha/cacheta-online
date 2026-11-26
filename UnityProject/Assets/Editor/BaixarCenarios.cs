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
    /// Baixa os pacotes de peças 3D dos cenários (.glb) para Assets/Resources/Cenarios. Roda sozinho quando o Unity
    /// abre ou recompila e só baixa o que falta ou mudou de versão; o menu Carteado > Baixar cenários 3D baixa tudo de
    /// novo. Os .glb não vão para o git (são grandes); cada máquina baixa os seus. Sem o pacote, o cenário usa as
    /// peças de formas simples.
    /// </summary>
    [InitializeOnLoad]
    public static class BaixarCenarios
    {
        private const string Pasta = "Assets/Resources/Cenarios";
        // Arquivo começando com ponto: o Unity ignora (não cria .meta).
        private const string Registro = Pasta + "/.baixados.txt";

        /// <summary>
        /// Nome do pacote (o mesmo do cenário) e de onde baixar. Trocar a URL faz baixar a versão nova.
        /// Bar do Tio Beto, 26/11/2026: peças grátis do Poly Haven (CC0) e peças próprias feitas com IA (veja
        /// Tools/cenarios/LEIAME.md).
        /// </summary>
        private static readonly (string Nome, string Url)[] Pacotes =
        {
            ("BarDoTioBeto", "URL_BAR_DO_TIO_BETO"),
        };

        private static bool _rodando;

        static BaixarCenarios()
        {
            EditorApplication.delayCall += () => Baixar(forcar: false);
        }

        [MenuItem("Carteado/Baixar cenários 3D")]
        private static void BaixarTudo() => Baixar(forcar: true);

        private static string Caminho(string nome) => Path.Combine(Pasta, nome + ".glb");

        private static async void Baixar(bool forcar)
        {
            if (_rodando || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var reg = LerRegistro();
            var faltam = Pacotes.Where(m => forcar || !File.Exists(Caminho(m.Nome))
                                             || !reg.TryGetValue(m.Nome, out var url) || url != m.Url).ToList();
            if (faltam.Count == 0) return;

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
                        EditorUtility.DisplayProgressBar("Carteado", $"Baixando cenário {m.Nome} ({i + 1} de {faltam.Count})", i / (float)faltam.Count);
                        try
                        {
                            var bytes = await http.GetByteArrayAsync(m.Url);
                            File.WriteAllBytes(Caminho(m.Nome), bytes);
                            reg[m.Nome] = m.Url;
                            ok++;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[Carteado] Não consegui baixar o cenário {m.Nome}.glb: {e.Message}");
                        }
                    }
                }
                SalvarRegistro(reg);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _rodando = false;
                if (ok > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.Log($"[Carteado] {ok} cenário(s) 3D baixado(s) em {Pasta}.");
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
