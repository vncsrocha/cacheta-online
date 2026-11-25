using System;
using System.Collections.Generic;
using System.Linq;

namespace Cacheta.Core
{
    public enum TurnPhase
    {
        /// <summary>Antes de jogar: o jogador da vez olha a mão e a vira e diz se vai ou corre.</summary>
        Decidir,
        /// <summary>O jogador da vez precisa comprar (monte ou descarte).</summary>
        Comprar,
        /// <summary>O jogador da vez está com uma carta a mais e precisa descartar ou bater.</summary>
        Descartar,
        /// <summary>A rodada acabou; chame <see cref="CachetaMatch.StartNextRound"/>.</summary>
        FimDaRodada,
        /// <summary>A partida acabou.</summary>
        FimDaPartida,
    }

    /// <summary>Situação de cada jogador na rodada atual.</summary>
    public enum SeatStatus
    {
        /// <summary>Ainda não disse se vai ou corre.</summary>
        Decidindo,
        /// <summary>Foi: está jogando a rodada.</summary>
        Vai,
        /// <summary>Correu: não joga a rodada e perde menos se alguém bater.</summary>
        Correu,
        /// <summary>Cumprindo a rodada de fora por ter batido furado.</summary>
        Sentado,
        /// <summary>Sem tentos, fora da partida.</summary>
        Eliminado,
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
        private readonly SeatStatus[] _status;
        private readonly bool[] _sitOutNext;
        /// <summary>Quem fez o último descarte, enquanto ele ainda pode ser pego para bater atravessando.</summary>
        private int _atravessarFrom = -1;
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
            _status = new SeatStatus[PlayerCount];
            _sitOutNext = new bool[PlayerCount];
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

        /// <summary>Situação do jogador na rodada (decidindo, vai, correu, sentado, eliminado).</summary>
        public SeatStatus GetStatus(int seat) => IsEliminated(seat) ? SeatStatus.Eliminado : _status[seat];

        /// <summary>Se o jogador está jogando a rodada (não correu, não está sentado, não foi eliminado).</summary>
        public bool IsPlaying(int seat) => !IsEliminated(seat) && (_status[seat] == SeatStatus.Vai || _status[seat] == SeatStatus.Decidindo);

        /// <summary>Se o jogador vai ficar a próxima rodada de fora (bateu furado).</summary>
        public bool WillSitOutNext(int seat) => _sitOutNext[seat];

        /// <summary>
        /// Se agora dá para pedir para bater atravessando com o descarte de cima: alguém acabou de descartar e
        /// o próximo ainda não comprou. Quem descartou não pode.
        /// </summary>
        public bool CanAtravessar(int seat)
            => Rules.BaterAtravessando && Phase == TurnPhase.Comprar && _atravessarFrom >= 0 && seat != _atravessarFrom
               && _discard.Count > 0 && Rules.CanBaterFromDiscard && IsPlaying(seat) && _status[seat] == SeatStatus.Vai;

        /// <summary>Quem fez o descarte que ainda pode ser pego para bater atravessando (-1 se nenhum).</summary>
        public int AtravessarFrom => _atravessarFrom;

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
            _atravessarFrom = -1;
            for (int s = 0; s < PlayerCount; s++)
            {
                _status[s] = IsEliminated(s) ? SeatStatus.Eliminado
                    : _sitOutNext[s] ? SeatStatus.Sentado
                    : Rules.VaiOuCorre ? SeatStatus.Decidindo : SeatStatus.Vai;
                _sitOutNext[s] = false;
            }
            // Se sobrar menos de 2 jogando, ninguém fica sentado nesta rodada.
            if (Enumerable.Range(0, PlayerCount).Count(s => _status[s] == SeatStatus.Decidindo || _status[s] == SeatStatus.Vai) < 2)
                for (int s = 0; s < PlayerCount; s++)
                    if (_status[s] == SeatStatus.Sentado) _status[s] = Rules.VaiOuCorre ? SeatStatus.Decidindo : SeatStatus.Vai;

            for (int d = 0; d < Rules.DeckCount; d++)
                for (int s = 0; s < 4; s++)
                    for (int r = 1; r <= 13; r++)
                        _stock.Add(new Card(r, (Suit)s, d));
            _rng.Shuffle(_stock);

            // Distribui uma carta por vez, começando à esquerda do carteador.
            int seat = DealerSeat;
            var dealt = Enumerable.Range(0, PlayerCount).Where(IsPlaying).ToList();
            for (int i = 0; i < Rules.CardsPerHand; i++)
            {
                foreach (var _ in dealt)
                {
                    seat = NextPlaying(seat);
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
                if (_status[s] == SeatStatus.Sentado)
                {
                    Raise(new GameEvent { Type = GameEventType.SentadoDeFora, Seat = s, Message = "cumprindo a rodada de fora" });
                    continue;
                }
                _hands[s].Sort();
                Raise(new GameEvent { Type = GameEventType.HandDealt, Seat = s, VisibleToSeat = s, Cards = _hands[s].ToList() });
            }

            // Quem fala primeiro gira a cada rodada junto com o carteador (como o botão do pôquer).
            CurrentSeat = NextPlaying(DealerSeat);
            Phase = Rules.VaiOuCorre ? TurnPhase.Decidir : TurnPhase.Comprar;
            return ActionResult.Success();
        }

        /// <summary>
        /// Vai ou corre. Na ordem da mesa, cada um decide olhando a mão e a vira. Se todos os outros correrem,
        /// quem sobrou ganha a rodada sem jogar.
        /// </summary>
        public ActionResult Decide(int seat, bool vai)
        {
            var check = CheckTurn(seat, TurnPhase.Decidir);
            if (!check.Ok) return check;

            _status[seat] = vai ? SeatStatus.Vai : SeatStatus.Correu;
            Raise(new GameEvent { Type = GameEventType.Decidiu, Seat = seat, Amount = vai ? 1 : 0, Message = vai ? "vai" : "correu" });

            var undecided = Enumerable.Range(0, PlayerCount).Where(s => !IsEliminated(s) && _status[s] == SeatStatus.Decidindo).ToList();
            int going = Enumerable.Range(0, PlayerCount).Count(s => !IsEliminated(s) && _status[s] == SeatStatus.Vai);

            if (going == 0 && undecided.Count == 1)
            {
                WinUncontested(undecided[0]);
                return ActionResult.Success();
            }
            if (undecided.Count == 0)
            {
                if (going == 1)
                {
                    WinUncontested(Enumerable.Range(0, PlayerCount).First(s => !IsEliminated(s) && _status[s] == SeatStatus.Vai));
                    return ActionResult.Success();
                }
                // Todos decidiram: começa o jogo com quem vai, a partir da esquerda do carteador.
                CurrentSeat = NextPlaying(DealerSeat);
                Phase = TurnPhase.Comprar;
                return ActionResult.Success();
            }
            for (int i = 1; i <= PlayerCount; i++)
            {
                int s = (seat + i) % PlayerCount;
                if (!IsEliminated(s) && _status[s] == SeatStatus.Decidindo) { CurrentSeat = s; break; }
            }
            return ActionResult.Success();
        }

        private void WinUncontested(int winner)
        {
            _status[winner] = SeatStatus.Vai;
            Raise(new GameEvent { Type = GameEventType.RodadaSemDisputa, Seat = winner, Message = "todos correram" });
            foreach (int s in AliveSeats.ToList())
            {
                if (s == winner) continue;
                int loss = _status[s] == SeatStatus.Sentado ? Rules.TentosLostSentado : Rules.TentosLostCorreu;
                LoseTentos(s, loss);
            }
            FinishRound();
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
            _atravessarFrom = -1;
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
            _atravessarFrom = -1;
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
            _atravessarFrom = seat;
            return ActionResult.Success();
        }

        /// <summary>
        /// Bate na sua vez. Com <paramref name="discard"/> = carta a descartar (3 jogos de 3 e descarta 1); com null,
        /// baixa as 10 cartas (batida de 10: 2 jogos de 3 e 1 sequência de 4). O motor confere se a mão fecha.
        /// </summary>
        public ActionResult Bater(int seat, Card? discard)
        {
            var check = CheckTurn(seat, TurnPhase.Descartar);
            if (!check.Ok) return check;
            if (PickedFromDiscard.HasValue && !Rules.CanBaterFromDiscard)
                return ActionResult.Fail("Nesta mesa não se bate com carta do descarte");

            var eval = EvaluateBater(_hands[seat], discard, PickedFromDiscard);
            if (eval.Error != null) return ActionResult.Fail(eval.Error);
            ApplyBater(seat, discard, eval, atravessando: false);
            return ActionResult.Success();
        }

        /// <summary>
        /// Bater atravessando: pega o descarte de outro jogador fora da vez para bater na hora. Se a mão não fechar,
        /// é "bater furado": a carta volta para o descarte e o jogador fica a próxima rodada de fora.
        /// Devolve Ok nos dois casos (bateu ou furou); veja os eventos para saber qual.
        /// </summary>
        public ActionResult BaterAtravessando(int seat, Card? discard)
        {
            if (Phase == TurnPhase.FimDaPartida) return ActionResult.Fail("A partida acabou");
            if (!CanAtravessar(seat)) return ActionResult.Fail("Agora não dá para bater atravessando");

            var top = _discard[_discard.Count - 1];
            var hand = _hands[seat].ToList();
            hand.Add(top);
            var eval = EvaluateBater(hand, discard, top);
            if (eval.Error != null)
            {
                if (Rules.BaterFuradoSitsOut) _sitOutNext[seat] = true;
                Raise(new GameEvent { Type = GameEventType.BaterFurado, Seat = seat, Card = top,
                                      Message = Rules.BaterFuradoSitsOut ? "bateu furado, fica a próxima rodada de fora" : "bateu furado" });
                return ActionResult.Success();
            }

            Pop(_discard);
            _hands[seat].Add(top);
            Raise(new GameEvent { Type = GameEventType.DrewFromDiscard, Seat = seat, Card = top, Message = "atravessando" });
            CurrentSeat = seat;
            ApplyBater(seat, discard, eval, atravessando: true);
            return ActionResult.Success();
        }

        /// <summary>Confere se uma mão de 10 cartas bate (descartando <paramref name="discard"/> ou baixando as 10).</summary>
        private (HandArrangement Arrangement, bool BatidaDeDez, List<Card> Melded, string Error) EvaluateBater(
            IReadOnlyList<Card> hand, Card? discard, Card? picked)
        {
            if (discard.HasValue)
            {
                if (Rules.BaterMode == BaterMode.BaixandoDez) return (null, false, null, "Nesta mesa bate-se baixando as 10 cartas");
                if (!hand.Contains(discard.Value)) return (null, false, null, "Essa carta não está na sua mão");
                if (Rules.CannotDiscardPickedCard && picked == discard)
                    return (null, false, null, "Não pode descartar a carta que acabou de pegar do descarte");
                var toMeld = hand.ToList();
                toMeld.Remove(discard.Value);
                var arr = HandEvaluator.Arrange(toMeld, Rules, Jokers);
                return arr.IsComplete ? (arr, false, toMeld, null) : (null, false, null, "A mão ainda não fecha: " + arr);
            }

            var all = hand.ToList();
            if (Rules.BatidaDeDez)
            {
                var dez = HandEvaluator.BatidaDeDez(all, Rules, Jokers);
                if (dez != null) return (dez, true, all, null);
            }
            if (Rules.BaterMode != BaterMode.DescartandoNove)
            {
                var arr = HandEvaluator.Arrange(all, Rules, Jokers);
                if (arr.IsComplete) return (arr, false, all, null);
            }
            return (null, false, null, Rules.BatidaDeDez
                ? "Para bater com as 10 cartas são 2 jogos de 3 e 1 sequência de 4"
                : "Nesta mesa é preciso descartar uma carta para bater");
        }

        private void ApplyBater(int seat, Card? discard,
                                (HandArrangement Arrangement, bool BatidaDeDez, List<Card> Melded, string Error) eval, bool atravessando)
        {
            var hand = _hands[seat];
            if (discard.HasValue)
            {
                hand.Remove(discard.Value);
                _discard.Add(discard.Value);
            }
            var arrangement = eval.Arrangement;
            bool firstTurn = _turnsThisRound[seat] == 0;

            int lossFoi, lossCorreu;
            if (Rules.VaiOuCorre)
            {
                lossFoi = eval.BatidaDeDez ? Rules.TentosLostFoiBatidaDeDez : Rules.TentosLostFoi;
                lossCorreu = eval.BatidaDeDez ? Rules.TentosLostCorreuBatidaDeDez : Rules.TentosLostCorreu;
            }
            else
            {
                int multiplier = 1;
                if (arrangement.IsClean && Rules.CleanBaterDoubles) multiplier *= 2;
                if (firstTurn && Rules.FirstTurnBaterDoubles) multiplier *= 2;
                if (eval.BatidaDeDez) multiplier *= 2;
                lossFoi = lossCorreu = Rules.TentosLostPerRound * multiplier;
            }
            if (Rules.VaiOuCorre && arrangement.IsClean && Rules.CleanBaterDoubles) { lossFoi *= 2; lossCorreu *= 2; }

            Raise(new GameEvent
            {
                Type = GameEventType.Bateu, Seat = seat, Card = discard, Arrangement = arrangement,
                Clean = arrangement.IsClean, FirstTurn = firstTurn, Amount = lossFoi, Cards = eval.Melded,
                BatidaDeDez = eval.BatidaDeDez, Atravessando = atravessando,
                Message = arrangement.ToString(),
            });

            foreach (int s in AliveSeats.ToList())
            {
                if (s == seat) continue;
                int loss = _status[s] == SeatStatus.Sentado ? Rules.TentosLostSentado
                    : _status[s] == SeatStatus.Correu ? lossCorreu : lossFoi;
                LoseTentos(s, loss);
            }

            FinishRound();
        }

        private void LoseTentos(int seat, int loss)
        {
            if (loss <= 0) return;
            _tentos[seat] = Math.Max(0, _tentos[seat] - loss);
            Raise(new GameEvent { Type = GameEventType.TentosChanged, Seat = seat, Amount = -loss, Message = $"agora tem {_tentos[seat]}" });
            if (_tentos[seat] == 0) Raise(new GameEvent { Type = GameEventType.PlayerEliminated, Seat = seat });
        }

        // ---------------------------------------------------------------- internos

        private void EndTurn(int seat)
        {
            _turnsThisRound[seat]++;
            PickedFromDiscard = null;
            CurrentSeat = NextPlaying(seat);
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
            _atravessarFrom = -1;
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
            {
                if (Phase == TurnPhase.Decidir) return ActionResult.Fail("Primeiro diga se vai ou corre");
                if (expected == TurnPhase.Decidir) return ActionResult.Fail("A rodada já começou");
                return ActionResult.Fail(expected == TurnPhase.Comprar ? "Você já comprou" : "Você precisa comprar primeiro");
            }
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

        /// <summary>Próximo jogador que está na rodada (não correu, não está sentado, não foi eliminado).</summary>
        private int NextPlaying(int seat)
        {
            for (int i = 1; i <= PlayerCount; i++)
            {
                int s = (seat + i) % PlayerCount;
                if (IsPlaying(s)) return s;
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
        public void DebugSetup(IList<IList<Card>> hands, Card vira, IList<Card> stock, IList<Card> discard = null, int? currentSeat = null,
                               IList<int> correram = null)
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
            for (int s = 0; s < PlayerCount; s++)
                _status[s] = IsEliminated(s) ? SeatStatus.Eliminado : correram != null && correram.Contains(s) ? SeatStatus.Correu : SeatStatus.Vai;
            Phase = TurnPhase.Comprar;
            PickedFromDiscard = null;
            _atravessarFrom = -1;
        }
    }
}
