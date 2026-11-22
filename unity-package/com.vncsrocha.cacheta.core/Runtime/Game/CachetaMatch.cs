using System;
using System.Collections.Generic;
using System.Linq;

namespace Cacheta.Core
{
    public enum TurnPhase
    {
        /// <summary>O jogador da vez precisa comprar (monte ou descarte).</summary>
        Comprar,
        /// <summary>O jogador da vez está com uma carta a mais e precisa descartar ou bater.</summary>
        Descartar,
        /// <summary>A rodada acabou; chame <see cref="CachetaMatch.StartNextRound"/>.</summary>
        FimDaRodada,
        /// <summary>A partida acabou.</summary>
        FimDaPartida,
    }

    public enum DrawSource
    {
        Monte,
        Descarte,
    }

    /// <summary>
    /// Uma partida completa de Cacheta: várias rodadas até sobrar um jogador com tentos.
    /// É a única fonte de verdade do estado do jogo; não depende de Unity. No online, roda no servidor
    /// e cada cliente recebe só o que pode ver (<see cref="GetView"/> e eventos filtrados).
    /// </summary>
    public sealed class CachetaMatch
    {
        public RuleSet Rules { get; }
        public IReadOnlyList<string> PlayerNames { get; }
        public int PlayerCount => PlayerNames.Count;

        private readonly int[] _tentos;
        private readonly List<Card>[] _hands;
        private readonly List<Card> _stock = new List<Card>();
        private readonly List<Card> _discard = new List<Card>();
        private readonly int[] _turnsThisRound;
        private readonly Rng _rng;
        private readonly List<GameEvent> _log = new List<GameEvent>();

        public int RoundNumber { get; private set; }
        public int DealerSeat { get; private set; }
        public int CurrentSeat { get; private set; } = -1;
        public TurnPhase Phase { get; private set; } = TurnPhase.FimDaRodada;
        public JokerInfo Jokers { get; private set; } = JokerInfo.None;
        public int? WinnerSeat { get; private set; }
        /// <summary>Carta pega do descarte nesta jogada (para a regra de não descartar a mesma).</summary>
        public Card? PickedFromDiscard { get; private set; }

        /// <summary>Disparado a cada evento (público e privado). Filtre com <see cref="GameEvent.IsVisibleTo"/>.</summary>
        public event Action<GameEvent> EventRaised;

        public IReadOnlyList<GameEvent> Log => _log;

        public CachetaMatch(IReadOnlyList<string> playerNames, RuleSet rules = null, ulong seed = 0, int firstDealer = 0)
        {
            Rules = (rules ?? RuleSet.Default()).Clone();
            Rules.Validate();
            if (playerNames == null || playerNames.Count < Rules.MinPlayers || playerNames.Count > Rules.MaxPlayers)
                throw new ArgumentException($"A Cacheta precisa de {Rules.MinPlayers} a {Rules.MaxPlayers} jogadores");

            PlayerNames = playerNames.ToList();
            _tentos = Enumerable.Repeat(Rules.StartingTentos, PlayerCount).ToArray();
            _hands = Enumerable.Range(0, PlayerCount).Select(_ => new List<Card>()).ToArray();
            _turnsThisRound = new int[PlayerCount];
            _rng = new Rng(seed == 0 ? (ulong)DateTime.UtcNow.Ticks : seed);
            // StartNextRound avança o carteador, então começamos um antes.
            DealerSeat = PrevAlive(((firstDealer % PlayerCount) + PlayerCount) % PlayerCount);
        }

        // ---------------------------------------------------------------- consultas

        public int GetTentos(int seat) => _tentos[seat];
        public bool IsEliminated(int seat) => _tentos[seat] <= 0;
        public IReadOnlyList<Card> GetHand(int seat) => _hands[seat];
        public int StockCount => _stock.Count;
        public Card? DiscardTop => _discard.Count > 0 ? _discard[_discard.Count - 1] : (Card?)null;
        public IReadOnlyList<Card> DiscardPile => _discard;
        public bool IsOver => Phase == TurnPhase.FimDaPartida;

        public IEnumerable<int> AliveSeats => Enumerable.Range(0, PlayerCount).Where(s => !IsEliminated(s));

        /// <summary>O que um jogador pode ver da mesa (use para UI, bots e para mandar ao cliente online).</summary>
        public PlayerView GetView(int seat) => new PlayerView(this, seat);

        // ---------------------------------------------------------------- rodada

        public ActionResult StartNextRound()
        {
            if (Phase == TurnPhase.FimDaPartida) return ActionResult.Fail("A partida já acabou");
            if (Phase != TurnPhase.FimDaRodada) return ActionResult.Fail("A rodada atual ainda não acabou");

            RoundNumber++;
            DealerSeat = NextAlive(DealerSeat);
            foreach (var h in _hands) h.Clear();
            _stock.Clear();
            _discard.Clear();
            Array.Clear(_turnsThisRound, 0, _turnsThisRound.Length);
            PickedFromDiscard = null;

            for (int d = 0; d < Rules.DeckCount; d++)
                for (int s = 0; s < 4; s++)
                    for (int r = 1; r <= 13; r++)
                        _stock.Add(new Card(r, (Suit)s, d));
            _rng.Shuffle(_stock);

            // Distribui uma carta por vez, começando à esquerda do carteador.
            int seat = DealerSeat;
            for (int i = 0; i < Rules.CardsPerHand; i++)
            {
                foreach (var _ in AliveSeats)
                {
                    seat = NextAlive(seat);
                    _hands[seat].Add(Pop(_stock));
                }
            }

            var vira = Pop(_stock);
            Jokers = new JokerInfo(vira, Rules.JokerRule);

            Raise(new GameEvent
            {
                Type = GameEventType.RoundStarted, Seat = DealerSeat, Card = vira,
                Message = $"rodada {RoundNumber}, vira {vira}, curinga {RankName(Jokers.JokerRank)}",
            });
            foreach (int s in AliveSeats)
            {
                _hands[s].Sort();
                Raise(new GameEvent { Type = GameEventType.HandDealt, Seat = s, VisibleToSeat = s, Cards = _hands[s].ToList() });
            }

            CurrentSeat = NextAlive(DealerSeat);
            Phase = TurnPhase.Comprar;
            return ActionResult.Success();
        }

        // ---------------------------------------------------------------- ações do jogador

        public ActionResult Draw(int seat, DrawSource source)
            => source == DrawSource.Monte ? DrawFromStock(seat) : DrawFromDiscard(seat);

        public ActionResult DrawFromStock(int seat)
        {
            var check = CheckTurn(seat, TurnPhase.Comprar);
            if (!check.Ok) return check;

            if (_stock.Count == 0)
            {
                if (Rules.EmptyStock == EmptyStockRule.ReembaralharDescarte && _discard.Count > 1)
                {
                    var top = Pop(_discard);
                    _stock.AddRange(_discard);
                    _discard.Clear();
                    _discard.Add(top);
                    _rng.Shuffle(_stock);
                    Raise(new GameEvent { Type = GameEventType.StockReshuffled, Amount = _stock.Count });
                }
                else
                {
                    AnnulRound("o monte acabou");
                    return ActionResult.Success();
                }
            }

            var card = Pop(_stock);
            _hands[seat].Add(card);
            PickedFromDiscard = null;
            Phase = TurnPhase.Descartar;
            Raise(new GameEvent { Type = GameEventType.DrewFromStock, Seat = seat });
            Raise(new GameEvent { Type = GameEventType.CardReceived, Seat = seat, VisibleToSeat = seat, Card = card });
            return ActionResult.Success();
        }

        public ActionResult DrawFromDiscard(int seat)
        {
            var check = CheckTurn(seat, TurnPhase.Comprar);
            if (!check.Ok) return check;
            if (_discard.Count == 0) return ActionResult.Fail("O descarte está vazio");

            var card = Pop(_discard);
            _hands[seat].Add(card);
            PickedFromDiscard = card;
            Phase = TurnPhase.Descartar;
            Raise(new GameEvent { Type = GameEventType.DrewFromDiscard, Seat = seat, Card = card });
            return ActionResult.Success();
        }

        public ActionResult Discard(int seat, Card card)
        {
            var check = CheckTurn(seat, TurnPhase.Descartar);
            if (!check.Ok) return check;
            if (!_hands[seat].Contains(card)) return ActionResult.Fail("Essa carta não está na sua mão");
            if (Rules.CannotDiscardPickedCard && PickedFromDiscard == card)
                return ActionResult.Fail("Não pode descartar a carta que acabou de pegar do descarte");

            _hands[seat].Remove(card);
            _discard.Add(card);
            Raise(new GameEvent { Type = GameEventType.Discarded, Seat = seat, Card = card });
            EndTurn(seat);
            return ActionResult.Success();
        }

        /// <summary>
        /// Bate. Com <paramref name="discard"/> = carta a descartar (modo "descartando nove"); com null, baixa a mão
        /// inteira (modo "baixando dez"). O motor confere se a mão fecha; o cliente não precisa mandar os jogos.
        /// </summary>
        public ActionResult Bater(int seat, Card? discard)
        {
            var check = CheckTurn(seat, TurnPhase.Descartar);
            if (!check.Ok) return check;
            if (PickedFromDiscard.HasValue && !Rules.CanBaterFromDiscard)
                return ActionResult.Fail("Nesta mesa não se bate com carta do descarte");

            var hand = _hands[seat];
            List<Card> toMeld;
            if (discard.HasValue)
            {
                if (Rules.BaterMode == BaterMode.BaixandoDez) return ActionResult.Fail("Nesta mesa bate-se baixando as 10 cartas");
                if (!hand.Contains(discard.Value)) return ActionResult.Fail("Essa carta não está na sua mão");
                if (Rules.CannotDiscardPickedCard && PickedFromDiscard == discard)
                    return ActionResult.Fail("Não pode descartar a carta que acabou de pegar do descarte");
                toMeld = hand.ToList();
                toMeld.Remove(discard.Value);
            }
            else
            {
                if (Rules.BaterMode == BaterMode.DescartandoNove) return ActionResult.Fail("Nesta mesa é preciso descartar uma carta para bater");
                toMeld = hand.ToList();
            }

            var arrangement = HandEvaluator.Arrange(toMeld, Rules, Jokers);
            if (!arrangement.IsComplete) return ActionResult.Fail("A mão ainda não fecha: " + arrangement);

            if (discard.HasValue)
            {
                hand.Remove(discard.Value);
                _discard.Add(discard.Value);
            }

            bool firstTurn = _turnsThisRound[seat] == 0;
            int multiplier = 1;
            if (arrangement.IsClean && Rules.CleanBaterDoubles) multiplier *= 2;
            if (firstTurn && Rules.FirstTurnBaterDoubles) multiplier *= 2;
            int loss = Rules.TentosLostPerRound * multiplier;

            Raise(new GameEvent
            {
                Type = GameEventType.Bateu, Seat = seat, Card = discard, Arrangement = arrangement,
                Clean = arrangement.IsClean, FirstTurn = firstTurn, Amount = loss, Cards = toMeld,
                Message = arrangement.ToString(),
            });

            foreach (int s in AliveSeats.ToList())
            {
                if (s == seat) continue;
                _tentos[s] = Math.Max(0, _tentos[s] - loss);
                Raise(new GameEvent { Type = GameEventType.TentosChanged, Seat = s, Amount = -loss, Message = $"agora tem {_tentos[s]}" });
                if (_tentos[s] == 0) Raise(new GameEvent { Type = GameEventType.PlayerEliminated, Seat = s });
            }

            FinishRound();
            return ActionResult.Success();
        }

        // ---------------------------------------------------------------- internos

        private void EndTurn(int seat)
        {
            _turnsThisRound[seat]++;
            PickedFromDiscard = null;
            CurrentSeat = NextAlive(seat);
            Phase = TurnPhase.Comprar;
        }

        private void AnnulRound(string reason)
        {
            Raise(new GameEvent { Type = GameEventType.RoundAnnulled, Message = reason });
            FinishRound();
        }

        private void FinishRound()
        {
            PickedFromDiscard = null;
            CurrentSeat = -1;
            var alive = AliveSeats.ToList();
            if (alive.Count <= 1)
            {
                WinnerSeat = alive.Count == 1 ? alive[0] : (int?)null;
                Phase = TurnPhase.FimDaPartida;
                Raise(new GameEvent { Type = GameEventType.MatchEnded, Seat = WinnerSeat ?? -1, Message = "levou a mesa" });
            }
            else
            {
                Phase = TurnPhase.FimDaRodada;
            }
        }

        private ActionResult CheckTurn(int seat, TurnPhase expected)
        {
            if (Phase == TurnPhase.FimDaPartida) return ActionResult.Fail("A partida acabou");
            if (Phase == TurnPhase.FimDaRodada) return ActionResult.Fail("A rodada acabou");
            if (seat != CurrentSeat) return ActionResult.Fail("Não é a sua vez");
            if (Phase != expected)
                return ActionResult.Fail(expected == TurnPhase.Comprar ? "Você já comprou" : "Você precisa comprar primeiro");
            return ActionResult.Success();
        }

        private int NextAlive(int seat)
        {
            for (int i = 1; i <= PlayerCount; i++)
            {
                int s = (seat + i) % PlayerCount;
                if (!IsEliminated(s)) return s;
            }
            return seat;
        }

        private int PrevAlive(int seat)
        {
            for (int i = 1; i <= PlayerCount; i++)
            {
                int s = ((seat - i) % PlayerCount + PlayerCount) % PlayerCount;
                if (!IsEliminated(s)) return s;
            }
            return seat;
        }

        private void Raise(GameEvent e)
        {
            e.RoundNumber = RoundNumber;
            _log.Add(e);
            EventRaised?.Invoke(e);
        }

        private static Card Pop(List<Card> list)
        {
            var c = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return c;
        }

        internal static string RankName(int rank) => new Card(rank, Suit.Paus).RankLabel;

        // ---------------------------------------------------------------- testes

        /// <summary>Só para testes/tutoriais: força mãos, vira e monte específicos depois de StartNextRound.</summary>
        public void DebugSetup(IList<IList<Card>> hands, Card vira, IList<Card> stock, IList<Card> discard = null, int? currentSeat = null)
        {
            for (int s = 0; s < PlayerCount; s++)
            {
                _hands[s].Clear();
                if (s < hands.Count && hands[s] != null) _hands[s].AddRange(hands[s]);
            }
            Jokers = new JokerInfo(vira, Rules.JokerRule);
            _stock.Clear();
            _stock.AddRange(stock);
            _discard.Clear();
            if (discard != null) _discard.AddRange(discard);
            if (currentSeat.HasValue) CurrentSeat = currentSeat.Value;
            Phase = TurnPhase.Comprar;
            PickedFromDiscard = null;
        }
    }
}
