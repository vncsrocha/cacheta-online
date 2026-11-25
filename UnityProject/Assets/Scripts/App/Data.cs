using System.Collections.Generic;
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
        /// <summary>Cacheta Clássica com power-ups (os power-ups ainda não estão no jogo). Antigo "Cachetex".</summary>
        Carteado,
    }

    /// <summary>
    /// Ambientes onde a partida acontece: um cenário para cada chefe do Rei do Carteado. Na Cacheta Clássica quem cria a
    /// sala escolhe qualquer um.
    /// </summary>
    public enum EnvironmentId
    {
        BarDoTioBeto,
        SalaoDeBingo,
        GaragemDaMarcela,
        QuintalDoRamiro,
        QuiosqueDoMarquinho,
        PatioDaUniversidade,
        SalaVip,
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
        /// <summary>Modelo 3D pronto em Resources/Personagens (vazio = monta com formas simples).</summary>
        public string Model = "";
        /// <summary>Altura da pessoa em metros (1,75 = altura padrão). Só vale para os modelos 3D.</summary>
        public float Height = 1.75f;

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
            Model = "TioBeto", Height = 1.70f,
        };

        // Os outros chefes do Rei do Carteado. Também jogam como bots comuns (sem o poder de chefe).
        // Se o .glb ainda não estiver em Resources/Personagens, o avatar é montado com as formas simples.
        public static PlayerProfile DonaCleide() => new PlayerProfile
        {
            Name = "Dona Cleide", Gender = Gender.Feminino, SkinTone = 2, Hair = HairStyle.Curto, HairColor = 4,
            ShirtColor = 2, Bottom = Bottoms.Calca, PantsColor = 2, Shoes = Shoes.Tenis, Body = BodyType.Barrigudo, Glasses = true,
            Model = "DonaCleide", Height = 1.60f,
        };

        public static PlayerProfile Marquinho() => new PlayerProfile
        {
            Name = "Marquinho", Gender = Gender.Masculino, SkinTone = 4, Hair = HairStyle.Curto, HairColor = 3,
            ShirtColor = 3, PantsColor = 6, Bottom = Bottoms.Bermuda, Shoes = Shoes.Chinelo, Body = BodyType.Magro, Chain = true,
            Model = "Marquinho", Height = 1.75f,
        };

        public static PlayerProfile Ana() => new PlayerProfile
        {
            Name = "Ana", Gender = Gender.Feminino, SkinTone = 0, Hair = HairStyle.Comprido, HairColor = 7,
            ShirtColor = 10, PantsColor = 3, Bottom = Bottoms.Calca, Shoes = Shoes.Tenis, Body = BodyType.Magro, Glasses = true,
            Model = "Ana", Height = 1.58f,
        };

        public static PlayerProfile Marcela() => new PlayerProfile
        {
            Name = "Marcela", Gender = Gender.Feminino, SkinTone = 4, Hair = HairStyle.BlackPower, HairColor = 0,
            ShirtColor = 3, PantsColor = 3, Bottom = Bottoms.Bermuda, Shoes = Shoes.Tenis, Body = BodyType.Magro,
            Model = "Marcela", Height = 1.65f,
        };

        public static PlayerProfile Ramiro() => new PlayerProfile
        {
            Name = "Ramiro", Gender = Gender.Masculino, SkinTone = 5, Hair = HairStyle.Careca, ShirtColor = 2,
            PantsColor = 9, Bottom = Bottoms.Bermuda, Shoes = Shoes.Chinelo, Body = BodyType.Barrigudo, Mustache = true,
            Model = "Ramiro", Height = 1.82f,
        };

        /// <summary>Chefe final. Até a fase dele aparece de terno preto; na final se revela (modelo LittleTigerRevelado).</summary>
        public static PlayerProfile LittleTiger(bool revelado = false) => new PlayerProfile
        {
            Name = "Little Tiger", Gender = Gender.Masculino, SkinTone = 3, Hair = HairStyle.Curto, HairColor = 0,
            ShirtColor = revelado ? 6 : 5, PantsColor = 5, Bottom = Bottoms.Calca, Shoes = Shoes.Tenis, Body = BodyType.Magro, Chain = true,
            Model = revelado ? "LittleTigerRevelado" : "LittleTiger", Height = 1.85f,
        };

        /// <summary>Todos os chefes, na ordem sugerida do Rei do Carteado (o Little Tiger aparece disfarçado).</summary>
        public static List<PlayerProfile> Chefes() => new List<PlayerProfile>
        {
            TioBeto(), DonaCleide(), Marcela(), Ramiro(), Marquinho(), Ana(), LittleTiger(),
        };

        /// <summary>O chefe que joga em casa naquele cenário (na Sala VIP o Little Tiger já aparece revelado).</summary>
        public static PlayerProfile ChefeDe(EnvironmentId env)
        {
            if (env == EnvironmentId.SalaVip) return LittleTiger(revelado: true);
            foreach (var c in Chefes())
                if (Names.EnvironmentOf(c.Name) == env) return c;
            return TioBeto();
        }
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
        public EnvironmentId Environment = EnvironmentId.BarDoTioBeto;
        public int Bots = 3;
        public BotDifficulty Difficulty = BotDifficulty.Medio;
        public bool Online;          // ainda não implementado: por enquanto toda sala é local contra bots
        public string Code = "";     // código para os amigos entrarem (online)
        /// <summary>Fase do Rei do Carteado que está sendo jogada (-1 = partida normal).</summary>
        public int Fase = -1;

        /// <summary>
        /// Regras de cada modo. O Carteado é a Clássica com power-ups; enquanto os power-ups não entram, as regras são
        /// as mesmas.
        /// </summary>
        public RuleSet BuildRules() => RuleSet.Default();

        public RoomConfig Clone() => (RoomConfig)MemberwiseClone();
    }

    /// <summary>Uma fase do Rei do Carteado: o chefe, a casa dele e o power-up de chefe.</summary>
    public class FaseDoRei
    {
        public PlayerProfile Chefe;
        public EnvironmentId Ambiente;
        public string PowerUp;
        public string Descricao;
    }

    /// <summary>
    /// Rei do Carteado: as 7 fases na ordem sugerida (do power-up mais leve ao mais difícil; o Little Tiger é sempre o
    /// último). O progresso fica salvo no computador.
    /// </summary>
    public static class ReiDoCarteado
    {
        private const string ProgressKey = "cacheta.rei.vencidas";

        public static readonly FaseDoRei[] Fases =
        {
            new FaseDoRei { Chefe = PlayerProfile.TioBeto(), Ambiente = EnvironmentId.BarDoTioBeto, PowerUp = "Saideira",
                            Descricao = "Dono do bar. Quando a mesa está para fechar, ele pede a saideira." },
            new FaseDoRei { Chefe = PlayerProfile.DonaCleide(), Ambiente = EnvironmentId.SalaoDeBingo, PowerUp = "Fofoca",
                            Descricao = "Todo mundo troca 3 cartas aleatórias." },
            new FaseDoRei { Chefe = PlayerProfile.Marcela(), Ambiente = EnvironmentId.GaragemDaMarcela, PowerUp = "Carta Rara",
                            Descricao = "Uma carta colecionável vai para a sua mão e só sai se for para bater." },
            new FaseDoRei { Chefe = PlayerProfile.Ramiro(), Ambiente = EnvironmentId.QuintalDoRamiro, PowerUp = "Bateu Furado",
                            Descricao = "Para ganhar a rodada é preciso bater duas vezes." },
            new FaseDoRei { Chefe = PlayerProfile.Marquinho(), Ambiente = EnvironmentId.QuiosqueDoMarquinho, PowerUp = "Pra gringo é mais caro",
                            Descricao = "Você fica batido uma volta inteira antes de bater, e não pode atravessar." },
            new FaseDoRei { Chefe = PlayerProfile.Ana(), Ambiente = EnvironmentId.PatioDaUniversidade, PowerUp = "Planejada e Organizada",
                            Descricao = "Você não pode reorganizar a mão e o vira fica escondido até decidir." },
            new FaseDoRei { Chefe = PlayerProfile.LittleTiger(revelado: true), Ambiente = EnvironmentId.SalaVip, PowerUp = "Viciado",
                            Descricao = "Ninguém corre. Ele perde 1 mesmo ganhando, e os outros perdem 2." },
        };

        /// <summary>Quantas fases você já venceu (a próxima é a que está liberada).</summary>
        public static int Vencidas
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey, 0), 0, Fases.Length);
            set { PlayerPrefs.SetInt(ProgressKey, Mathf.Clamp(value, 0, Fases.Length)); PlayerPrefs.Save(); }
        }

        public static bool Liberada(int fase) => fase <= Vencidas;

        /// <summary>Marca a fase como vencida (só avança se for a fase liberada agora).</summary>
        public static void Venceu(int fase)
        {
            if (fase >= Vencidas) Vencidas = fase + 1;
        }

        /// <summary>A sala de uma fase: Carteado na casa do chefe, com ele sentado de frente para você.</summary>
        public static RoomConfig Sala(int fase, BotDifficulty dificuldade) => new RoomConfig
        {
            Mode = GameMode.Carteado,
            Environment = Fases[fase].Ambiente,
            Bots = 3,
            Difficulty = dificuldade,
            Fase = fase,
        };
    }

    public static class Names
    {
        public static string Of(GameMode m) => m == GameMode.Carteado ? "Carteado" : "Cacheta Clássica";

        public static string Of(EnvironmentId e)
        {
            switch (e)
            {
                case EnvironmentId.SalaoDeBingo: return "Salão de bingo";
                case EnvironmentId.GaragemDaMarcela: return "Garagem da Marcela";
                case EnvironmentId.QuintalDoRamiro: return "Churrasco do Ramiro";
                case EnvironmentId.QuiosqueDoMarquinho: return "Quiosque do Marquinho";
                case EnvironmentId.PatioDaUniversidade: return "Pátio da faculdade";
                case EnvironmentId.SalaVip: return "Sala VIP";
                default: return "Bar do Tio Beto";
            }
        }

        /// <summary>O cenário de cada chefe (as partidas contra ele no Rei do Carteado acontecem lá).</summary>
        public static EnvironmentId EnvironmentOf(string chefe)
        {
            switch (chefe)
            {
                case "Dona Cleide": return EnvironmentId.SalaoDeBingo;
                case "Marcela": return EnvironmentId.GaragemDaMarcela;
                case "Ramiro": return EnvironmentId.QuintalDoRamiro;
                case "Marquinho": return EnvironmentId.QuiosqueDoMarquinho;
                case "Ana": return EnvironmentId.PatioDaUniversidade;
                case "Little Tiger": return EnvironmentId.SalaVip;
                default: return EnvironmentId.BarDoTioBeto;
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
