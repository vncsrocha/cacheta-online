using System;

namespace Cacheta.Core
{
    /// <summary>Quais cartas viram curinga a partir da vira.</summary>
    public enum JokerSuitRule
    {
        /// <summary>Só a carta seguinte à vira do MESMO naipe (2 curingas com 2 baralhos).</summary>
        MesmoNaipe,
        /// <summary>A carta seguinte à vira da mesma COR (4 curingas).</summary>
        MesmaCor,
        /// <summary>A carta seguinte à vira de QUALQUER naipe (8 curingas).</summary>
        QualquerNaipe,
        /// <summary>Sem curinga.</summary>
        Nenhum,
    }

    /// <summary>Como se bate.</summary>
    public enum BaterMode
    {
        /// <summary>Compra (fica com 10), descarta 1 e as 9 restantes têm que estar todas em jogos.</summary>
        DescartandoNove,
        /// <summary>Compra e baixa as 10 cartas todas em jogos, sem descartar.</summary>
        BaixandoDez,
        /// <summary>Vale qualquer uma das duas.</summary>
        Ambos,
    }

    /// <summary>O que acontece quando o monte de compra acaba.</summary>
    public enum EmptyStockRule
    {
        /// <summary>Embaralha o descarte (menos a carta de cima) e ele vira o novo monte.</summary>
        ReembaralharDescarte,
        /// <summary>A rodada acaba sem vencedor e ninguém perde tentos.</summary>
        RodadaAnulada,
    }

    /// <summary>
    /// Todas as regras configuráveis da Cacheta. Os valores padrão são a nossa "regra da casa"; cada sala pode
    /// mudar o que quiser. Mantido como classe simples e serializável (Unity JsonUtility / qualquer JSON).
    /// </summary>
    [Serializable]
    public sealed class RuleSet
    {
        // --- Mesa ---
        public int MinPlayers = 2;
        public int MaxPlayers = 8;
        public int DeckCount = 2;
        public int CardsPerHand = 9;

        // --- Curinga ---
        public JokerSuitRule JokerRule = JokerSuitRule.MesmoNaipe;
        /// <summary>Máximo de curingas usados como coringa dentro de um mesmo jogo (0 = sem limite).</summary>
        public int MaxJokersPerMeld = 1;
        /// <summary>Se curinga pode completar trinca (algumas mesas só aceitam em sequência).</summary>
        public bool JokerAllowedInSets = true;

        // --- Jogos ---
        public int MinMeldSize = 3;
        /// <summary>Ás pode ser alto (Q-K-A) além de baixo (A-2-3).</summary>
        public bool AceHigh = true;
        /// <summary>Permite dar a volta (K-A-2). Quase nenhuma mesa permite.</summary>
        public bool AllowWrapAround = false;
        /// <summary>Trinca/quadra só com naipes diferentes. Se falso, aceita naipe repetido (dois 7♥ por exemplo).</summary>
        public bool SetsRequireDistinctSuits = true;
        /// <summary>Tamanho máximo de uma trinca/quadra (4 com naipes diferentes).</summary>
        public int MaxSetSize = 4;

        // --- Batida ---
        public BaterMode BaterMode = BaterMode.DescartandoNove;
        /// <summary>Pode bater pegando a carta do descarte.</summary>
        public bool CanBaterFromDiscard = true;
        /// <summary>Quem pega a carta do descarte não pode descartar essa mesma carta na mesma jogada.</summary>
        public bool CannotDiscardPickedCard = true;

        // --- Monte ---
        public EmptyStockRule EmptyStock = EmptyStockRule.ReembaralharDescarte;

        // --- Pontuação (tentos) ---
        public int StartingTentos = 10;
        /// <summary>Quantos tentos cada perdedor perde numa batida normal.</summary>
        public int TentosLostPerRound = 1;
        /// <summary>Batida limpa (sem usar curinga como coringa) faz os outros perderem em dobro.</summary>
        public bool CleanBaterDoubles = true;
        /// <summary>Batida de mão (na primeira vez que o jogador joga na rodada) faz os outros perderem em dobro.</summary>
        public bool FirstTurnBaterDoubles = false;

        public static RuleSet Default() => new RuleSet();

        public RuleSet Clone() => (RuleSet)MemberwiseClone();

        public void Validate()
        {
            if (DeckCount < 1) throw new ArgumentException("DeckCount precisa ser >= 1");
            if (MinPlayers < 2 || MaxPlayers < MinPlayers) throw new ArgumentException("Faixa de jogadores inválida");
            if (CardsPerHand < MinMeldSize) throw new ArgumentException("Mão menor que um jogo");
            if (MaxPlayers * (CardsPerHand + 1) + 1 > DeckCount * 52)
                throw new ArgumentException("Cartas insuficientes para o número máximo de jogadores");
            if (MinMeldSize < 3) throw new ArgumentException("MinMeldSize precisa ser >= 3");
            if (MaxSetSize < MinMeldSize) throw new ArgumentException("MaxSetSize menor que MinMeldSize");
        }
    }
}
