using System;
using System.Collections.Generic;
using System.Linq;

namespace Cacheta.Core.Bots
{
    /// <summary>Decisão da segunda metade da jogada: descartar ou bater.</summary>
    public readonly struct TurnDecision
    {
        public readonly bool Bater;
        /// <summary>Carta a descartar. Nulo só quando bate baixando as 10.</summary>
        public readonly Card? Discard;

        public TurnDecision(bool bater, Card? discard)
        {
            Bater = bater;
            Discard = discard;
        }
    }

    /// <summary>Qualquer jogador automático. Recebe só a <see cref="PlayerView"/>, nunca o estado completo.</summary>
    public interface IBot
    {
        /// <summary>Vai (true) ou corre (false), olhando a mão e a vira.</summary>
        bool ChooseVai(PlayerView view);
        DrawSource ChooseDraw(PlayerView view);
        TurnDecision ChooseAction(PlayerView view);
        /// <summary>Se quer bater atravessando com o descarte de cima (e como). Nulo = não.</summary>
        TurnDecision? ChooseAtravessar(PlayerView view);
    }

    public enum BotDifficulty
    {
        Facil,
        Medio,
        Dificil,
    }

    /// <summary>
    /// Bot heurístico: mede a mão por "cartas soltas" e "quase-jogos" (pares e cartas vizinhas),
    /// pega o descarte quando ele melhora a mão e descarta a carta que menos faz falta.
    /// A dificuldade controla a chance de errar e o quanto ele aproveita o descarte.
    /// </summary>
    public sealed class SimpleBot : IBot
    {
        private readonly BotDifficulty _difficulty;
        private readonly Rng _rng;

        public SimpleBot(BotDifficulty difficulty = BotDifficulty.Medio, ulong seed = 1)
        {
            _difficulty = difficulty;
            _rng = new Rng(seed);
        }

        private double MistakeChance
        {
            get
            {
                switch (_difficulty)
                {
                    case BotDifficulty.Facil: return 0.30;
                    case BotDifficulty.Medio: return 0.08;
                    default: return 0.0;
                }
            }
        }

        public bool ChooseVai(PlayerView v)
        {
            // Mão de 9 cartas: quantas ficam soltas e quantas estão quase formando jogo.
            double score = Score(v.Hand, v);
            int myTentos = v.Tentos[v.Seat];
            // Limite de risco: quanto menor a pontuação, melhor a mão. Com poucos tentos, joga mais seguro.
            // (Mão inicial típica: 45 a 75. Abaixo de 50 já tem jogo formado ou quase.)
            double limit = _difficulty == BotDifficulty.Facil ? 66 : _difficulty == BotDifficulty.Medio ? 61 : 57;
            if (myTentos <= 2) limit -= 8;
            if (_difficulty == BotDifficulty.Facil && _rng.Next(100) < 25) return _rng.Next(2) == 0;
            // Se todos antes correram e sobra pouca gente decidindo, vale arriscar mais.
            int going = 0, deciding = 0;
            for (int s = 0; s < v.Status.Count; s++)
            {
                if (s == v.Seat) continue;
                if (v.Status[s] == SeatStatus.Vai) going++;
                if (v.Status[s] == SeatStatus.Decidindo) deciding++;
            }
            if (going == 0 && deciding <= 1) limit += 10;
            limit -= 2 * going; // muita gente indo = mais concorrência
            // Correr sai mais barato do que ir e perder; o difícil considera isso.
            return score <= limit;
        }

        public TurnDecision? ChooseAtravessar(PlayerView v)
        {
            if (!v.CanAtravessar || !v.DiscardTop.HasValue) return null;
            var top = v.DiscardTop.Value;
            var withTop = new List<Card>(v.Hand) { top };
            var found = FindBater(withTop, v, top);
            if (!found.HasValue) return null;
            // O fácil às vezes nem percebe.
            if (_difficulty == BotDifficulty.Facil && _rng.Next(100) < 50) return null;
            return found;
        }

        public DrawSource ChooseDraw(PlayerView v)
        {
            if (!v.DiscardTop.HasValue) return DrawSource.Monte;
            var top = v.DiscardTop.Value;
            var hand = v.Hand.ToList();

            // Se o descarte faz bater, pega sempre.
            var withTop = new List<Card>(hand) { top };
            if (FindBater(withTop, v, top).HasValue) return DrawSource.Descarte;
            if (_difficulty == BotDifficulty.Facil && _rng.Next(100) < 70) return DrawSource.Monte;

            double current = Score(hand, v);
            double best = double.MaxValue;
            foreach (var d in withTop)
            {
                if (d == top && v.Rules.CannotDiscardPickedCard) continue;
                var rest = new List<Card>(withTop);
                rest.Remove(d);
                best = Math.Min(best, Score(rest, v));
            }
            // Pegar do descarte mostra a carta para a mesa; só vale se melhora de verdade.
            double threshold = _difficulty == BotDifficulty.Dificil ? 3 : 6;
            return best <= current - threshold ? DrawSource.Descarte : DrawSource.Monte;
        }

        public TurnDecision ChooseAction(PlayerView v)
        {
            var hand = v.Hand.ToList();
            var bater = FindBater(hand, v, v.PickedFromDiscard);
            if (bater.HasValue) return bater.Value;

            var candidates = hand.Where(c => !(v.Rules.CannotDiscardPickedCard && v.PickedFromDiscard == c)).ToList();

            if (_rng.Next(1000) < MistakeChance * 1000)
            {
                var nonJokers = candidates.Where(c => !v.Jokers.IsJoker(c)).ToList();
                var pool = nonJokers.Count > 0 ? nonJokers : candidates;
                return new TurnDecision(false, pool[_rng.Next(pool.Count)]);
            }

            Card bestCard = candidates[0];
            double bestScore = double.MaxValue;
            foreach (var d in candidates)
            {
                var rest = new List<Card>(hand);
                rest.Remove(d);
                double s = Score(rest, v);
                if (v.Jokers.IsJoker(d)) s += 50; // curinga quase nunca se joga fora
                s += DangerOfDiscard(d, v);
                if (s < bestScore || (s == bestScore && d.Rank > bestCard.Rank))
                {
                    bestScore = s;
                    bestCard = d;
                }
            }
            return new TurnDecision(false, bestCard);
        }

        /// <summary>Procura uma batida válida com essa mão de 10 cartas (preferindo batida limpa).</summary>
        public static TurnDecision? FindBater(IReadOnlyList<Card> hand, PlayerView v, Card? pickedFromDiscard)
        {
            if (pickedFromDiscard.HasValue && !v.Rules.CanBaterFromDiscard) return null;
            TurnDecision? found = null;
            bool foundClean = false;

            if (v.Rules.BatidaDeDez && HandEvaluator.BatidaDeDez(hand, v.Rules, v.Jokers) != null)
                return new TurnDecision(true, null); // batida de 10 sempre vale mais
            if (v.Rules.BaterMode != BaterMode.DescartandoNove)
            {
                var arr = HandEvaluator.Arrange(hand, v.Rules, v.Jokers);
                if (arr.IsComplete)
                {
                    found = new TurnDecision(true, null);
                    foundClean = arr.IsClean;
                    if (foundClean) return found;
                }
            }
            if (v.Rules.BaterMode != BaterMode.BaixandoDez)
            {
                foreach (var d in hand.Distinct())
                {
                    if (v.Rules.CannotDiscardPickedCard && pickedFromDiscard == d) continue;
                    var rest = hand.ToList();
                    rest.Remove(d);
                    var arr = HandEvaluator.Arrange(rest, v.Rules, v.Jokers);
                    if (!arr.IsComplete) continue;
                    if (arr.IsClean && v.Rules.CleanBaterDoubles) return new TurnDecision(true, d);
                    if (!found.HasValue) found = new TurnDecision(true, d);
                }
            }
            return found;
        }

        /// <summary>Pontuação da mão (menor = melhor).</summary>
        public static double Score(IReadOnlyList<Card> hand, PlayerView v)
        {
            var arr = HandEvaluator.Arrange(hand, v.Rules, v.Jokers);
            var loose = arr.Unmatched.Where(c => !v.Jokers.IsJoker(c)).ToList();
            int pairs = 0;
            for (int i = 0; i < loose.Count; i++)
                for (int j = i + 1; j < loose.Count; j++)
                    if (CouldCombine(loose[i], loose[j], v.Rules)) pairs++;
            int looseJokers = arr.Unmatched.Count - loose.Count;
            return arr.Unmatched.Count * 10 - pairs * 4 - looseJokers * 8;
        }

        private static bool CouldCombine(Card a, Card b, RuleSet r)
        {
            if (a.Rank == b.Rank) return !r.SetsRequireDistinctSuits || a.Suit != b.Suit;
            if (a.Suit != b.Suit) return false;
            int diff = Math.Abs(a.Rank - b.Rank);
            if (r.AceHigh && (a.Rank == 1 || b.Rank == 1)) diff = Math.Min(diff, 14 - Math.Max(a.Rank, b.Rank));
            return diff <= 2;
        }

        /// <summary>Evita entregar ao próximo uma carta que acabou de ser pega ou que combina com o descarte recente.</summary>
        private double DangerOfDiscard(Card d, PlayerView v)
        {
            if (_difficulty != BotDifficulty.Dificil) return 0;
            // Cartas do mesmo valor já descartadas são mais seguras (ninguém está juntando).
            int sameRankDiscarded = v.DiscardPile.Count(c => c.Rank == d.Rank);
            return sameRankDiscarded > 0 ? -0.5 : 0;
        }
    }

    /// <summary>Ajuda a rodar bots contra a partida, uma ação por vez (útil para animar no Unity com pausas).</summary>
    public static class BotDriver
    {
        /// <summary>Executa UMA ação (comprar, ou descartar/bater) para o jogador da vez.</summary>
        public static ActionResult Step(CachetaMatch match, IBot bot)
        {
            int seat = match.CurrentSeat;
            if (seat < 0) return ActionResult.Fail("Ninguém está na vez");
            var view = match.GetView(seat);
            if (match.Phase == TurnPhase.Decidir)
                return match.Decide(seat, bot.ChooseVai(view));
            if (match.Phase == TurnPhase.Comprar)
            {
                var source = bot.ChooseDraw(view);
                var r = match.Draw(seat, source);
                return r.Ok || source == DrawSource.Monte ? r : match.Draw(seat, DrawSource.Monte);
            }
            if (match.Phase == TurnPhase.Descartar)
            {
                var d = bot.ChooseAction(view);
                if (d.Bater)
                {
                    var r = match.Bater(seat, d.Discard);
                    if (r.Ok) return r;
                }
                var discard = d.Discard ?? view.Hand.First(c => view.PickedFromDiscard != c);
                return match.Discard(seat, discard);
            }
            return ActionResult.Fail("Fase sem ação de jogador");
        }

        /// <summary>
        /// Depois de um descarte, dá a chance de cada bot (na ordem da mesa a partir de quem descartou) bater
        /// atravessando. <paramref name="isBot"/> diz quais assentos são bots. Devolve true se alguém tentou.
        /// </summary>
        public static bool TryAtravessar(CachetaMatch match, IReadOnlyList<IBot> bots, Func<int, bool> isBot = null)
        {
            int from = match.AtravessarFrom;
            if (from < 0) return false;
            for (int i = 1; i < match.PlayerCount; i++)
            {
                int seat = (from + i) % match.PlayerCount;
                if (isBot != null && !isBot(seat)) continue;
                if (bots[seat] == null || !match.CanAtravessar(seat)) continue;
                var d = bots[seat].ChooseAtravessar(match.GetView(seat));
                if (!d.HasValue) continue;
                match.BaterAtravessando(seat, d.Value.Discard);
                return true;
            }
            return false;
        }

        /// <summary>Joga uma partida inteira só com bots. Devolve o assento vencedor.</summary>
        public static int? PlayWholeMatch(CachetaMatch match, IReadOnlyList<IBot> bots, int maxActions = 200000)
        {
            int actions = 0;
            while (!match.IsOver && actions++ < maxActions)
            {
                if (match.Phase == TurnPhase.FimDaRodada)
                {
                    match.StartNextRound();
                    continue;
                }
                if (match.Phase == TurnPhase.Comprar && TryAtravessar(match, bots) && match.Phase != TurnPhase.Comprar) continue;
                var r = Step(match, bots[match.CurrentSeat]);
                if (!r.Ok) throw new InvalidOperationException("Bot fez jogada inválida: " + r.Error);
            }
            return match.WinnerSeat;
        }
    }
}
