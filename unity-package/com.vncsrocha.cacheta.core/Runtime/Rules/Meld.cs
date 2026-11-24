using System.Collections.Generic;
using System.Linq;

namespace Cacheta.Core
{
    public enum MeldType
    {
        /// <summary>Sequência: 3+ cartas seguidas do mesmo naipe.</summary>
        Sequencia,
        /// <summary>Trinca/quadra: 3+ cartas do mesmo valor.</summary>
        Trinca,
    }

    /// <summary>Um jogo formado (sequência ou trinca).</summary>
    public sealed class Meld
    {
        public MeldType Type { get; }
        /// <summary>Cartas na ordem do jogo (sequência em ordem crescente).</summary>
        public IReadOnlyList<Card> Cards { get; }
        /// <summary>Cartas que estão sendo usadas como coringa (substituindo outra carta).</summary>
        public IReadOnlyList<Card> WildCards { get; }

        public Meld(MeldType type, IReadOnlyList<Card> cards, IReadOnlyList<Card> wildCards)
        {
            Type = type;
            Cards = cards;
            WildCards = wildCards;
        }

        public override string ToString()
        {
            var wild = new HashSet<Card>(WildCards);
            return "[" + string.Join(" ", Cards.Select(c => wild.Contains(c) ? c + "*" : c.ToString())) + "]";
        }
    }

    /// <summary>Uma forma de organizar a mão em jogos + cartas soltas.</summary>
    public sealed class HandArrangement
    {
        public IReadOnlyList<Meld> Melds { get; }
        public IReadOnlyList<Card> Unmatched { get; }
        public int WildsUsed { get; }

        public HandArrangement(IReadOnlyList<Meld> melds, IReadOnlyList<Card> unmatched, int wildsUsed)
        {
            Melds = melds;
            Unmatched = unmatched;
            WildsUsed = wildsUsed;
        }

        /// <summary>Todas as cartas estão em jogos.</summary>
        public bool IsComplete => Unmatched.Count == 0;

        /// <summary>Completa e sem usar curinga como coringa.</summary>
        public bool IsClean => IsComplete && WildsUsed == 0;

        public override string ToString()
        {
            string s = string.Join(" ", Melds.Select(m => m.ToString()));
            if (Unmatched.Count > 0) s += (s.Length > 0 ? " | soltas: " : "soltas: ") + string.Join(" ", Unmatched);
            return s;
        }
    }
}
