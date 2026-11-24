using System.Collections.Generic;
using System.Linq;

namespace Cacheta.Core
{
    /// <summary>
    /// Fotografia da mesa do ponto de vista de um jogador: a própria mão, quantas cartas os outros têm,
    /// o descarte, a vira etc. Nunca contém as cartas dos adversários nem a ordem do monte.
    /// É o que os bots usam para decidir (então eles não "roubam") e o que o servidor vai mandar ao cliente.
    /// </summary>
    public sealed class PlayerView
    {
        public int Seat { get; }
        public RuleSet Rules { get; }
        public JokerInfo Jokers { get; }
        public IReadOnlyList<Card> Hand { get; }
        public IReadOnlyList<int> HandCounts { get; }
        public IReadOnlyList<int> Tentos { get; }
        public IReadOnlyList<string> PlayerNames { get; }
        public IReadOnlyList<Card> DiscardPile { get; }
        public Card? DiscardTop { get; }
        public int StockCount { get; }
        public int CurrentSeat { get; }
        public int DealerSeat { get; }
        public int RoundNumber { get; }
        public TurnPhase Phase { get; }
        public Card? PickedFromDiscard { get; }
        /// <summary>Situação de cada jogador na rodada (vai, correu, sentado...).</summary>
        public IReadOnlyList<SeatStatus> Status { get; }
        /// <summary>Se este jogador pode pedir para bater atravessando com o descarte de cima agora.</summary>
        public bool CanAtravessar { get; }

        public bool IsMyTurn => CurrentSeat == Seat;

        internal PlayerView(CachetaMatch m, int seat)
        {
            Seat = seat;
            Rules = m.Rules;
            Jokers = m.Jokers;
            Hand = m.GetHand(seat).ToList();
            HandCounts = Enumerable.Range(0, m.PlayerCount).Select(s => m.GetHand(s).Count).ToList();
            Tentos = Enumerable.Range(0, m.PlayerCount).Select(m.GetTentos).ToList();
            PlayerNames = m.PlayerNames;
            DiscardPile = m.DiscardPile.ToList();
            DiscardTop = m.DiscardTop;
            StockCount = m.StockCount;
            CurrentSeat = m.CurrentSeat;
            DealerSeat = m.DealerSeat;
            RoundNumber = m.RoundNumber;
            Phase = m.Phase;
            PickedFromDiscard = m.PickedFromDiscard;
            Status = Enumerable.Range(0, m.PlayerCount).Select(m.GetStatus).ToList();
            CanAtravessar = m.CanAtravessar(seat);
        }
    }
}
