using System;
using System.Collections.Generic;
using System.Linq;

namespace Cacheta.Core
{
    /// <summary>
    /// Encontra a melhor forma de organizar uma mão em jogos (sequências e trincas), respeitando o <see cref="RuleSet"/>
    /// e os curingas da rodada. "Melhor" = menos cartas soltas; empate = menos curingas usados como coringa.
    /// Busca exaustiva com memoização: as mãos têm no máximo ~11 cartas, então é rápido.
    /// </summary>
    public static class HandEvaluator
    {
        public static HandArrangement Arrange(IReadOnlyList<Card> hand, RuleSet rules, JokerInfo jokers)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            jokers = jokers ?? JokerInfo.None;

            var jokerCards = hand.Where(jokers.IsJoker).ToList();
            var plain = hand.Where(c => !jokers.IsJoker(c)).ToList();

            // Cada curinga pode ser usado como coringa OU como a própria carta. Testamos as combinações
            // (normalmente há 0 a 2 curingas na mão, então são poucas).
            int subsetCount = jokerCards.Count <= 8 ? 1 << jokerCards.Count : 1;
            HandArrangement best = null;
            for (int mask = 0; mask < subsetCount; mask++)
            {
                var naturals = new List<Card>(plain);
                var wilds = new List<Card>();
                for (int j = 0; j < jokerCards.Count; j++)
                {
                    if ((mask & (1 << j)) != 0) naturals.Add(jokerCards[j]);
                    else wilds.Add(jokerCards[j]);
                }
                var arrangement = new Solver(naturals, wilds, rules).Run();
                if (best == null || IsBetter(arrangement, best)) best = arrangement;
                if (best.IsClean) break;
            }
            return best;
        }

        /// <summary>Verdadeiro se todas as cartas podem ser baixadas em jogos.</summary>
        public static bool IsComplete(IReadOnlyList<Card> hand, RuleSet rules, JokerInfo jokers)
            => Arrange(hand, rules, jokers).IsComplete;

        private static bool IsBetter(HandArrangement a, HandArrangement b)
        {
            if (a.Unmatched.Count != b.Unmatched.Count) return a.Unmatched.Count < b.Unmatched.Count;
            return a.WildsUsed < b.WildsUsed;
        }

        // ------------------------------------------------------------------------------------------

        private sealed class Plan
        {
            public MeldType Type;
            public int[] Slots;      // índice na lista de naturais, ou -1 para coringa
            public int WildCount;
        }

        private sealed class Result
        {
            public int Unmatched;
            public int Wilds;
            public Plan Plan;        // jogo escolhido neste passo (ou null)
            public int Loose = -1;   // carta deixada solta neste passo (ou -1)
            public Result Next;

            public bool BetterThan(Result o)
                => o == null || Unmatched < o.Unmatched || (Unmatched == o.Unmatched && Wilds < o.Wilds);
        }

        private sealed class Solver
        {
            private readonly List<Card> _nat;
            private readonly List<Card> _wildCards;
            private readonly RuleSet _r;
            private readonly int _maxWildPerMeld;
            private readonly Dictionary<long, Result> _memo = new Dictionary<long, Result>();

            public Solver(List<Card> naturals, List<Card> wilds, RuleSet rules)
            {
                if (naturals.Count > 40) throw new ArgumentException("Mão grande demais");
                _nat = naturals.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ThenBy(c => c.DeckIndex).ToList();
                _wildCards = wilds;
                _r = rules;
                _maxWildPerMeld = rules.MaxJokersPerMeld <= 0 ? int.MaxValue : rules.MaxJokersPerMeld;
            }

            public HandArrangement Run()
            {
                ulong all = _nat.Count == 64 ? ulong.MaxValue : (1UL << _nat.Count) - 1;
                var res = Solve(all, _wildCards.Count);
                return Build(res);
            }

            private Result Solve(ulong remaining, int wilds)
            {
                if (remaining == 0)
                    return new Result { Unmatched = wilds, Wilds = 0 }; // curingas que sobraram ficam soltos

                long key = (long)(remaining << 6) ^ wilds; // remaining < 2^40 e wilds < 64
                if (_memo.TryGetValue(key, out var cached)) return cached;

                int first = LowestBit(remaining);
                ulong rest = remaining & ~(1UL << first);

                // Opção 1: deixa a carta solta.
                var sub = Solve(rest, wilds);
                Result best = new Result { Unmatched = sub.Unmatched + 1, Wilds = sub.Wilds, Loose = first, Next = sub };

                // Opção 2: forma um jogo que contém a carta.
                foreach (var plan in SetsWith(first, rest, wilds).Concat(RunsWith(first, rest, wilds)))
                {
                    ulong used = 0;
                    foreach (int s in plan.Slots) if (s >= 0) used |= 1UL << s;
                    var r = Solve(remaining & ~used, wilds - plan.WildCount);
                    var cand = new Result { Unmatched = r.Unmatched, Wilds = r.Wilds + plan.WildCount, Plan = plan, Next = r };
                    if (cand.BetterThan(best)) best = cand;
                }

                _memo[key] = best;
                return best;
            }

            private IEnumerable<Plan> SetsWith(int first, ulong rest, int wilds)
            {
                Card c = _nat[first];
                int maxW = _r.JokerAllowedInSets ? Math.Min(wilds, _maxWildPerMeld) : 0;

                // Candidatas: mesmo valor. Com naipes distintos, guardamos uma por naipe (diferente do da carta).
                var others = new List<int>();
                var seenSuits = new HashSet<Suit> { c.Suit };
                for (int i = 0; i < _nat.Count; i++)
                {
                    if ((rest & (1UL << i)) == 0 || _nat[i].Rank != c.Rank) continue;
                    if (_r.SetsRequireDistinctSuits)
                    {
                        if (seenSuits.Contains(_nat[i].Suit)) continue;
                        seenSuits.Add(_nat[i].Suit);
                    }
                    others.Add(i);
                }

                int maxSize = _r.SetsRequireDistinctSuits ? Math.Min(4, _r.MaxSetSize) : _r.MaxSetSize;
                int combos = 1 << others.Count;
                for (int m = 0; m < combos; m++)
                {
                    var picked = new List<int> { first };
                    for (int b = 0; b < others.Count; b++) if ((m & (1 << b)) != 0) picked.Add(others[b]);
                    for (int w = 0; w <= maxW; w++)
                    {
                        int size = picked.Count + w;
                        if (size < _r.MinMeldSize || size > maxSize) continue;
                        var slots = new List<int>(picked);
                        for (int k = 0; k < w; k++) slots.Add(-1);
                        yield return new Plan { Type = MeldType.Trinca, Slots = slots.ToArray(), WildCount = w };
                    }
                }
            }

            private IEnumerable<Plan> RunsWith(int first, ulong rest, int wilds)
            {
                Card c = _nat[first];
                int maxW = Math.Min(wilds, _maxWildPerMeld);
                int maxK = _r.AceHigh ? 14 : 13;

                var positions = new List<int> { c.Rank };
                if (c.Rank == 1 && _r.AceHigh && !_r.AllowWrapAround) positions.Add(14);

                var results = new List<Plan>();
                foreach (int v in positions)
                {
                    for (int lo = v - 12; lo <= v; lo++)
                    {
                        if (!_r.AllowWrapAround && lo < 1) continue;
                        Extend(c.Suit, v, first, lo, lo, rest, new List<int>(), 0, maxW, maxK, results);
                    }
                }
                return results;
            }

            private void Extend(Suit suit, int v, int first, int lo, int k, ulong avail, List<int> slots,
                                int wildsUsed, int maxW, int maxK, List<Plan> output)
            {
                int length = k - lo; // cartas já colocadas
                if (length >= 13) return;
                if (!_r.AllowWrapAround && k > maxK) return;

                int rank = ((k - 1) % 13 + 13) % 13 + 1;
                if (k == v)
                {
                    slots.Add(first);
                    Emit(lo, k, v, slots, wildsUsed, output);
                    Extend(suit, v, first, lo, k + 1, avail, slots, wildsUsed, maxW, maxK, output);
                    slots.RemoveAt(slots.Count - 1);
                    return;
                }

                // Uma carta natural desse valor/naipe (cópias são equivalentes, basta a primeira livre).
                int nat = -1;
                for (int i = 0; i < _nat.Count; i++)
                {
                    if ((avail & (1UL << i)) != 0 && _nat[i].Suit == suit && _nat[i].Rank == rank) { nat = i; break; }
                }
                if (nat >= 0)
                {
                    slots.Add(nat);
                    Emit(lo, k, v, slots, wildsUsed, output);
                    Extend(suit, v, first, lo, k + 1, avail & ~(1UL << nat), slots, wildsUsed, maxW, maxK, output);
                    slots.RemoveAt(slots.Count - 1);
                }
                if (wildsUsed < maxW)
                {
                    slots.Add(-1);
                    Emit(lo, k, v, slots, wildsUsed + 1, output);
                    Extend(suit, v, first, lo, k + 1, avail, slots, wildsUsed + 1, maxW, maxK, output);
                    slots.RemoveAt(slots.Count - 1);
                }
            }

            private void Emit(int lo, int k, int v, List<int> slots, int wildsUsed, List<Plan> output)
            {
                if (k < v) return; // a sequência ainda não chegou na carta obrigatória
                if (slots.Count < _r.MinMeldSize) return;
                output.Add(new Plan { Type = MeldType.Sequencia, Slots = slots.ToArray(), WildCount = wildsUsed });
            }

            private HandArrangement Build(Result res)
            {
                var melds = new List<Meld>();
                var loose = new List<Card>();
                int wildIdx = 0;
                int wildsUsed = 0;
                for (var r = res; r != null; r = r.Next)
                {
                    if (r.Loose >= 0) loose.Add(_nat[r.Loose]);
                    if (r.Plan != null)
                    {
                        var cards = new List<Card>();
                        var wild = new List<Card>();
                        foreach (int s in r.Plan.Slots)
                        {
                            if (s >= 0) cards.Add(_nat[s]);
                            else
                            {
                                var w = _wildCards[wildIdx++];
                                cards.Add(w);
                                wild.Add(w);
                            }
                        }
                        wildsUsed += wild.Count;
                        melds.Add(new Meld(r.Plan.Type, cards, wild));
                    }
                }
                while (wildIdx < _wildCards.Count) loose.Add(_wildCards[wildIdx++]);
                return new HandArrangement(melds, loose, wildsUsed);
            }

            private static int LowestBit(ulong x)
            {
                int i = 0;
                while ((x & 1UL) == 0) { x >>= 1; i++; }
                return i;
            }
        }
    }
}
