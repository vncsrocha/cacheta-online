using System;
using Cacheta.Core;
using Cacheta.Core.Bots;
using UnityEngine;

namespace Cacheta.App
{
    public enum GameMode
    {
        /// <summary>Cacheta tradicional, com a regra da casa.</summary>
        CachetaTradicional,
        /// <summary>Variante própria do jogo (nome provisório). Regras ainda a definir.</summary>
        Cachetex,
    }

    /// <summary>Ambientes onde a partida acontece. Quem cria a sala escolhe.</summary>
    public enum EnvironmentId
    {
        QuintalAoEntardecer,
        Boteco,
        Quiosque,
    }

    public enum Gender { Masculino, Feminino, Neutro }
    public enum HairStyle { Careca, Curto, BlackPower, Comprido, Moicano, Coque }
    public enum HatStyle { Nenhum, Bone, Panama, Touca }
    public enum BodyType { Magro, Medio, Barrigudo }
    public enum ShirtPattern { Lisa, Listrada }
    public enum Bottoms { Bermuda, Calca }
    public enum Shoes { Chinelo, Tenis }

    /// <summary>Perfil do jogador: nome e aparência do personagem. Salvo localmente (depois, na conta online).</summary>
    [Serializable]
    public class PlayerProfile
    {
        public string Name = "Jogador";
        public Gender Gender = Gender.Neutro;
        public int SkinTone;      // índice em Palette.SkinTones
        public HairStyle Hair = HairStyle.Curto;
        public int HairColor;     // índice em Palette.HairColors
        public int ShirtColor;    // índice em Palette.ClothColors
        public int PantsColor = 3;
        public HatStyle Hat = HatStyle.Nenhum;
        public bool Glasses;
        public bool Mustache;
        public BodyType Body = BodyType.Medio;
        public ShirtPattern Pattern = ShirtPattern.Lisa;
        public int ShirtColor2 = 1;
        public Bottoms Bottom = Bottoms.Bermuda;
        public Shoes Shoes = Shoes.Chinelo;
        public bool Chain;

        public PlayerProfile Clone() => (PlayerProfile)MemberwiseClone();

        /// <summary>Perfil aleatório para os bots.</summary>
        public static PlayerProfile Random(string name, System.Random rng)
        {
            return new PlayerProfile
            {
                Name = name,
                Gender = (Gender)rng.Next(3),
                SkinTone = rng.Next(Palette.SkinTones.Length),
                Hair = (HairStyle)rng.Next(Enum.GetValues(typeof(HairStyle)).Length),
                HairColor = rng.Next(Palette.HairColors.Length),
                ShirtColor = rng.Next(Palette.ClothColors.Length),
                PantsColor = rng.Next(Palette.ClothColors.Length),
                Hat = (HatStyle)rng.Next(Enum.GetValues(typeof(HatStyle)).Length),
                Glasses = rng.Next(4) == 0,
                Mustache = rng.Next(4) == 0,
                Body = (BodyType)rng.Next(3),
                Pattern = (ShirtPattern)rng.Next(2),
                ShirtColor2 = rng.Next(Palette.ClothColors.Length),
                Bottom = (Bottoms)rng.Next(2),
                Shoes = (Shoes)rng.Next(2),
                Chain = rng.Next(3) == 0,
            };
        }

        /// <summary>O Tio Beto, primeiro personagem desenhado (proporção "meio-termo").</summary>
        public static PlayerProfile TioBeto() => new PlayerProfile
        {
            Name = "Tio Beto", Gender = Gender.Masculino, SkinTone = 3, Hair = HairStyle.Curto, HairColor = 0,
            ShirtColor = 0, ShirtColor2 = 10, Pattern = ShirtPattern.Listrada, PantsColor = 9, Bottom = Bottoms.Bermuda,
            Shoes = Shoes.Chinelo, Body = BodyType.Barrigudo, Mustache = true, Chain = true,
        };
    }

    /// <summary>Opções do jogo (menu Opções).</summary>
    [Serializable]
    public class GameSettings
    {
        // Áudio (0..1)
        public float MasterVolume = 0.8f;
        public float MusicVolume = 0.7f;
        public float SfxVolume = 0.8f;
        // Gráficos
        public bool Fullscreen = true;
        public int ResolutionIndex = -1; // -1 = resolução atual da tela
        public int QualityLevel = -1;    // -1 = padrão do projeto
        public bool VSync = true;
        // Jogo
        public float BotDelay = 0.9f;
        public bool ShowHints = true;
        public bool ConfirmBater = false;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();
    }

    /// <summary>Configuração de uma sala: o que quem cria a sala escolhe.</summary>
    [Serializable]
    public class RoomConfig
    {
        public GameMode Mode = GameMode.CachetaTradicional;
        public EnvironmentId Environment = EnvironmentId.QuintalAoEntardecer;
        public int Bots = 3;
        public BotDifficulty Difficulty = BotDifficulty.Medio;
        public bool Online;          // ainda não implementado: por enquanto toda sala é local contra bots
        public string Code = "";     // código para os amigos entrarem (online)

        /// <summary>Regras de cada modo. O Cachetex começa igual à tradicional até definirmos as regras dele.</summary>
        public RuleSet BuildRules()
        {
            var r = RuleSet.Default();
            if (Mode == GameMode.Cachetex)
            {
                // TODO(Cachetex): regras próprias. Ponto de partida para testar: sem limite de curinga por jogo.
                r.MaxJokersPerMeld = 0;
            }
            return r;
        }
    }

    public static class Names
    {
        public static string Of(GameMode m) => m == GameMode.Cachetex ? "Cachetex" : "Cacheta tradicional";

        public static string Of(EnvironmentId e)
        {
            switch (e)
            {
                case EnvironmentId.Boteco: return "Boteco da esquina";
                case EnvironmentId.Quiosque: return "Quiosque na praia";
                default: return "Quintal ao entardecer";
            }
        }

        public static string Of(BotDifficulty d)
            => d == BotDifficulty.Facil ? "Fácil" : d == BotDifficulty.Medio ? "Médio" : "Difícil";

        public static string Of(HairStyle h)
        {
            switch (h)
            {
                case HairStyle.BlackPower: return "Black power";
                default: return h.ToString();
            }
        }

        public static string Of(BodyType b) => b == BodyType.Medio ? "Médio" : b.ToString();
        public static string Of(Bottoms b) => b == Bottoms.Calca ? "Calça" : "Bermuda";
        public static string Of(Shoes s) => s == Shoes.Tenis ? "Tênis" : "Chinelo";

        public static string Of(HatStyle h)
        {
            switch (h)
            {
                case HatStyle.Bone: return "Boné";
                case HatStyle.Panama: return "Panamá";
                default: return h.ToString();
            }
        }
    }

    /// <summary>Salva perfil e opções em PlayerPrefs como JSON.</summary>
    public static class SaveSystem
    {
        private const string ProfileKey = "cacheta.profile";
        private const string SettingsKey = "cacheta.settings";

        public static PlayerProfile LoadProfile() => Load(ProfileKey, () => new PlayerProfile());
        public static GameSettings LoadSettings() => Load(SettingsKey, () => new GameSettings());
        public static void Save(PlayerProfile p) => SaveJson(ProfileKey, p);
        public static void Save(GameSettings s) => SaveJson(SettingsKey, s);

        public static bool HasProfile => PlayerPrefs.HasKey(ProfileKey);

        private static T Load<T>(string key, Func<T> fallback)
        {
            try
            {
                if (PlayerPrefs.HasKey(key))
                {
                    var obj = JsonUtility.FromJson<T>(PlayerPrefs.GetString(key));
                    if (obj != null) return obj;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Não consegui ler {key}: {e.Message}");
            }
            return fallback();
        }

        private static void SaveJson(string key, object o)
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(o));
            PlayerPrefs.Save();
        }
    }
}
