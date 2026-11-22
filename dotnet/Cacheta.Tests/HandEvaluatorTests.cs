using System.Collections.Generic;
using System.Linq;
using Cacheta.Core;
using Xunit;

namespace Cacheta.Tests
{
    public class HandEvaluatorTests
    {
        // Atalho: "3c 4c 5c" -> cartas. Use "#" no fim para a cópia do segundo baralho (ex.: "7c#").
        internal static List<Card> H(string s) => s.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.EndsWith("#") ? Card.Parse(t.TrimEnd('#'), 1) : Card.Parse(t)).ToList();

        private static readonly RuleSet R = RuleSet.Default();
        // Vira 2♣ => curinga é o 3♣ (mesmo naipe, padrão).
        private static readonly JokerInfo Vira2p = new JokerInfo(Card.Parse("2p"), JokerSuitRule.MesmoNaipe);

        [Fact]
        public void ParseAndIds()
        {
            var c = Card.Parse("10e", 1);
            Assert.Equal(10, c.Rank);
            Assert.Equal(Suit.Espadas, c.Suit);
            Assert.Equal(c, Card.FromId(c.Id));
            Assert.Equal("10♠", c.ToString());
        }

        [Fact]
        public void SequenciaETrincaFecham()
        {
            var arr = HandEvaluator.Arrange(H("4c 5c 6c 9o 9e 9p Jc Qc Kc"), R, JokerInfo.None);
            Assert.True(arr.IsComplete, arr.ToString());
            Assert.True(arr.IsClean);
            Assert.Equal(3, arr.Melds.Count);
        }

        [Fact]
        public void TrincaComNaipeRepetidoNaoVale()
        {
            var arr = HandEvaluator.Arrange(H("9o 9o# 9e"), R, JokerInfo.None);
            Assert.False(arr.IsComplete);

            var loose = R.Clone();
            loose.SetsRequireDistinctSuits = false;
            Assert.True(HandEvaluator.IsComplete(H("9o 9o# 9e"), loose, JokerInfo.None));
        }

        [Fact]
        public void SequenciaDeNaipesMisturadosNaoVale()
        {
            Assert.False(HandEvaluator.IsComplete(H("4c 5o 6c"), R, JokerInfo.None));
        }

        [Fact]
        public void AsAltoEBaixo()
        {
            Assert.True(HandEvaluator.IsComplete(H("Ac 2c 3c"), R, JokerInfo.None));
            Assert.True(HandEvaluator.IsComplete(H("Qc Kc Ac"), R, JokerInfo.None));
            Assert.False(HandEvaluator.IsComplete(H("Kc Ac 2c"), R, JokerInfo.None)); // sem dar a volta

            var wrap = R.Clone();
            wrap.AllowWrapAround = true;
            Assert.True(HandEvaluator.IsComplete(H("Kc Ac 2c"), wrap, JokerInfo.None));

            var low = R.Clone();
            low.AceHigh = false;
            Assert.False(HandEvaluator.IsComplete(H("Qc Kc Ac"), low, JokerInfo.None));
        }

        [Fact]
        public void CuringaCompletaSequencia()
        {
            // 3♣ é curinga: completa 7♥ _ 9♥
            var arr = HandEvaluator.Arrange(H("7c 9c 3p"), R, Vira2p);
            Assert.True(arr.IsComplete, arr.ToString());
            Assert.False(arr.IsClean);
            Assert.Equal(1, arr.WildsUsed);
        }

        [Fact]
        public void CuringaUsadoComoElePropioContaComoLimpo()
        {
            // 2♣ 3♣ 4♣: o 3♣ é curinga, mas está no próprio lugar => batida limpa.
            var arr = HandEvaluator.Arrange(H("2p 3p 4p"), R, Vira2p);
            Assert.True(arr.IsClean, arr.ToString());
        }

        [Fact]
        public void LimiteDeUmCuringaPorJogo()
        {
            // 7♥ + dois curingas: com limite 1 não fecha; sem limite fecha.
            var hand = H("7c 3p 3p#");
            Assert.False(HandEvaluator.IsComplete(hand, R, Vira2p));
            var many = R.Clone();
            many.MaxJokersPerMeld = 0;
            Assert.True(HandEvaluator.IsComplete(hand, many, Vira2p));
        }

        [Fact]
        public void CuringaEmTrincaConfiguravel()
        {
            var hand = H("8o 8e 3p");
            Assert.True(HandEvaluator.IsComplete(hand, R, Vira2p));
            var noSets = R.Clone();
            noSets.JokerAllowedInSets = false;
            Assert.False(HandEvaluator.IsComplete(hand, noSets, Vira2p));
        }

        [Fact]
        public void ViraReiFazAsSerCuringa()
        {
            var j = new JokerInfo(Card.Parse("Ke"), JokerSuitRule.MesmoNaipe);
            Assert.True(j.IsJoker(Card.Parse("Ae")));
            Assert.False(j.IsJoker(Card.Parse("Ac")));
            var cor = new JokerInfo(Card.Parse("Ke"), JokerSuitRule.MesmaCor);
            Assert.True(cor.IsJoker(Card.Parse("Ap")));
            Assert.False(cor.IsJoker(Card.Parse("Ac")));
        }

        [Fact]
        public void EscolheMelhorDivisaoQuandoCartaServeEmDoisJogos()
        {
            // 7♥ pode ir na trinca de 7 ou na sequência 5-6-7♥; só uma divisão fecha tudo.
            var hand = H("5c 6c 7c 7o 7e 7p 8o 9o 10o");
            var arr = HandEvaluator.Arrange(hand, R, JokerInfo.None);
            Assert.True(arr.IsComplete, arr.ToString());
        }

        [Fact]
        public void QuadraESequenciaLonga()
        {
            Assert.True(HandEvaluator.IsComplete(H("Ko Ke Kp Kc 2e 3e 4e 5e 6e"), R, JokerInfo.None));
        }

        [Fact]
        public void MaoQueNaoFechaMostraSoltas()
        {
            var arr = HandEvaluator.Arrange(H("4c 5c 6c 9o 9e Jc Qp 2o 7e"), R, JokerInfo.None);
            Assert.False(arr.IsComplete);
            Assert.Equal(6, arr.Unmatched.Count);
        }
    }
}
