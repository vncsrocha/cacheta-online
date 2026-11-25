using System;
using UnityEngine;

namespace Cacheta.App
{
    /// <summary>
    /// A planta de um cenário, no mesmo formato do artefato "Plantas dos cenários" (onde o Vinicius edita): medidas da
    /// sala, vãos nas paredes e cada peça com posição, tamanho e giro. Coordenadas em metros, iguais às da Unity: a mesa
    /// na origem, você em -Z e a parede do fundo em +Z.
    /// Os arquivos ficam em Resources/Plantas (a planta atual) e Resources/Plantas/Originais (o ponto de partida, que é
    /// o que o código monta). O <see cref="EnvironmentBuilder"/> aplica a diferença entre as duas.
    /// </summary>
    [Serializable]
    public class Planta
    {
        public string id;
        public string nome;
        public string chefe;
        public PlantaSala sala;
        public PlantaAbertura[] aberturas;
        public PlantaObjeto[] objetos;
        public string notas;

        public PlantaObjeto Find(string objId)
        {
            if (objetos == null) return null;
            foreach (var o in objetos) if (o != null && o.id == objId) return o;
            return null;
        }

        public static Planta Load(EnvironmentId env) => Read("Plantas/" + env);
        public static Planta LoadOriginal(EnvironmentId env) => Read("Plantas/Originais/" + env);

        private static Planta Read(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) return null;
            try
            {
                return JsonUtility.FromJson<Planta>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Planta {path} com erro: {e.Message}");
                return null;
            }
        }

        /// <summary>Cor "#rrggbb" da planta.</summary>
        public static Color Color(string hex, Color fallback)
            => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;
    }

    [Serializable]
    public class PlantaSala
    {
        public float largura;   // de parede a parede (x)
        public float fundo;     // z da parede do fundo
        public float frente;    // z da frente da sala (atrás de você, sem parede)
        public float pe;        // pé-direito
    }

    [Serializable]
    public class PlantaAbertura
    {
        public string lado;     // fundo, esquerda, direita
        public float de, ate;   // no fundo é x; nas laterais é z
        public string tipo;     // enrolar, livre, janela
        public float altura;
        public float peitoril;
    }

    [Serializable]
    public class PlantaObjeto
    {
        public string id;
        public string nome;
        public float x, z, w, d, rot, h;
        public string cor;
        public string onde;     // chao, parede, teto, fora
        public string fonte;    // gratis (pacote livre) ou ia (peça marcante feita com IA)
        public string forma;    // ret ou circ
        public string nota;
    }
}
