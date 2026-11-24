using System;

namespace Cacheta.Core
{
    /// <summary>Naipes. A ordem não importa para as regras, só para exibição.</summary>
    public enum Suit : byte
    {
        Paus = 0,    // ♣
        Ouros = 1,   // ♦
        Copas = 2,   // ♥
        Espadas = 3, // ♠
    }

    public static class SuitExtensions
    {
        public static bool IsRed(this Suit s) => s == Suit.Ouros || s == Suit.Copas;

        public static string Symbol(this Suit s)
        {
            switch (s)
            {
                case Suit.Paus: return "♣";
                case Suit.Ouros: return "♦";
                case Suit.Copas: return "♥";
                default: return "♠";
            }
        }
    }

    /// <summary>
    /// Uma carta física. Como a Cacheta usa dois baralhos, existem duas cópias de cada carta;
    /// <see cref="DeckIndex"/> diferencia as duas para que cada carta tenha um <see cref="Id"/> único (0..103),
    /// o que facilita serialização e rede.
    /// </summary>
    public readonly struct Card : IEquatable<Card>, IComparable<Card>
    {
        /// <summary>1 = Ás, 11 = Valete, 12 = Dama, 13 = Rei.</summary>
        public readonly byte Rank;
        public readonly Suit Suit;
        public readonly byte DeckIndex;

        public Card(int rank, Suit suit, int deckIndex = 0)
        {
            if (rank < 1 || rank > 13) throw new ArgumentOutOfRangeException(nameof(rank));
            if (deckIndex < 0 || deckIndex > 255) throw new ArgumentOutOfRangeException(nameof(deckIndex));
            Rank = (byte)rank;
            Suit = suit;
            DeckIndex = (byte)deckIndex;
        }

        /// <summary>Identificador único e estável: deck*52 + naipe*13 + (valor-1).</summary>
        public int Id => DeckIndex * 52 + (int)Suit * 13 + (Rank - 1);

        public static Card FromId(int id) => new Card(id % 13 + 1, (Suit)(id / 13 % 4), id / 52);

        /// <summary>Mesmo valor e naipe, ignorando de qual baralho veio.</summary>
        public bool SameFace(Card other) => Rank == other.Rank && Suit == other.Suit;

        public string RankLabel
        {
            get
            {
                switch (Rank)
                {
                    case 1: return "A";
                    case 11: return "J";
                    case 12: return "Q";
                    case 13: return "K";
                    default: return Rank.ToString();
                }
            }
        }

        public override string ToString() => RankLabel + Suit.Symbol();

        /// <summary>Lê textos como "7♥", "7c", "10e", "Ao", "Kp" (c=copas, o=ouros, e=espadas, p=paus).</summary>
        public static Card Parse(string text, int deckIndex = 0)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length < 2) throw new FormatException(text);
            text = text.Trim();
            string r = text.Substring(0, text.Length - 1).ToUpperInvariant();
            char s = char.ToLowerInvariant(text[text.Length - 1]);
            int rank;
            switch (r)
            {
                case "A": rank = 1; break;
                case "J": rank = 11; break;
                case "Q": rank = 12; break;
                case "K": rank = 13; break;
                default:
                    if (!int.TryParse(r, out rank)) throw new FormatException(text);
                    break;
            }
            Suit suit;
            switch (s)
            {
                case 'p': case '♣': suit = Suit.Paus; break;
                case 'o': case '♦': suit = Suit.Ouros; break;
                case 'c': case '♥': suit = Suit.Copas; break;
                case 'e': case '♠': suit = Suit.Espadas; break;
                default: throw new FormatException(text);
            }
            return new Card(rank, suit, deckIndex);
        }

        public bool Equals(Card other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Card c && Equals(c);
        public override int GetHashCode() => Id;
        public static bool operator ==(Card a, Card b) => a.Equals(b);
        public static bool operator !=(Card a, Card b) => !a.Equals(b);

        /// <summary>Ordena por naipe e depois valor (bom para mostrar a mão).</summary>
        public int CompareTo(Card other)
        {
            int c = Suit.CompareTo(other.Suit);
            if (c != 0) return c;
            c = Rank.CompareTo(other.Rank);
            return c != 0 ? c : DeckIndex.CompareTo(other.DeckIndex);
        }
    }
}
