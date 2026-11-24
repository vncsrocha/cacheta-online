namespace Cacheta.Core
{
    /// <summary>Define quais cartas são curinga numa rodada, a partir da vira.</summary>
    public sealed class JokerInfo
    {
        public Card Vira { get; }
        public JokerSuitRule Rule { get; }
        /// <summary>Valor do curinga: o seguinte ao da vira (depois do Rei vem o Ás).</summary>
        public int JokerRank { get; }

        public JokerInfo(Card vira, JokerSuitRule rule)
        {
            Vira = vira;
            Rule = rule;
            JokerRank = vira.Rank == 13 ? 1 : vira.Rank + 1;
        }

        public static JokerInfo None { get; } = new JokerInfo(new Card(1, Suit.Paus), JokerSuitRule.Nenhum);

        public bool IsJoker(Card c)
        {
            if (Rule == JokerSuitRule.Nenhum || c.Rank != JokerRank) return false;
            switch (Rule)
            {
                case JokerSuitRule.MesmoNaipe: return c.Suit == Vira.Suit;
                case JokerSuitRule.MesmaCor: return c.Suit.IsRed() == Vira.Suit.IsRed();
                default: return true;
            }
        }
    }
}
