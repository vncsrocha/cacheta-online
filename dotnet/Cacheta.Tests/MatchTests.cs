using System.Collections.Generic;
using System.Linq;
using Cacheta.Core;
using Cacheta.Core.Bots;
using Xunit;
using static Cacheta.Tests.HandEvaluatorTests;

namespace Cacheta.Tests
{
    public class MatchTests
    {
        private static CachetaMatch NewMatch(int players = 3, RuleSet rules = null, ulong seed = 42)
            => new CachetaMatch(Enumerable.Range(0, players).Select(i => "P" + i).ToList(), rules, seed);

        [Fact]
        public void DistribuiNoveCartasEViraCuringa()
        {
            var m = NewMatch(4);
            Assert.True(m.StartNextRound().Ok);
            for (int s = 0; s < 4; s++) Assert.Equal(9, m.GetHand(s).Count);
            Assert.Equal(104 - 36 - 1, m.StockCount);
            Assert.Equal(TurnPhase.Comprar, m.Phase);
            Assert.Equal((m.DealerSeat + 1) % 4, m.CurrentSeat);
            // Todas as cartas são únicas.
            var all = Enumerable.Range(0, 4).SelectMany(m.GetHand).Select(c => c.Id).ToList();
            Assert.Equal(all.Count, all.Distinct().Count());
        }

        [Fact]
        public void MesmaSementeMesmaPartida()
        {
            var a = NewMatch(3, seed: 7);
            var b = NewMatch(3, seed: 7);
            a.StartNextRound();
            b.StartNextRound();
            Assert.Equal(a.GetHand(0), b.GetHand(0));
            Assert.Equal(a.Jokers.Vira, b.Jokers.Vira);
        }

        [Fact]
        public void ForaDaVezOuSemComprarERecusado()
        {
            var m = NewMatch(3);
            m.StartNextRound();
            int cur = m.CurrentSeat;
            int other = (cur + 1) % 3;
            Assert.False(m.DrawFromStock(other).Ok);
            Assert.False(m.Discard(cur, m.GetHand(cur)[0]).Ok); // precisa comprar antes
            Assert.False(m.DrawFromDiscard(cur).Ok);             // descarte vazio
            Assert.True(m.DrawFromStock(cur).Ok);
            Assert.False(m.DrawFromStock(cur).Ok);               // já comprou
            Assert.Equal(10, m.GetHand(cur).Count);
            Assert.True(m.Discard(cur, m.GetHand(cur)[0]).Ok);
            Assert.Equal(other, m.CurrentSeat);
        }

        [Fact]
        public void NaoPodeDescartarCartaPegaDoDescarte()
        {
            var m = NewMatch(2);
            m.StartNextRound();
            int a = m.CurrentSeat, b = 1 - a;
            m.DrawFromStock(a);
            var thrown = m.GetHand(a)[0];
            m.Discard(a, thrown);
            Assert.True(m.DrawFromDiscard(b).Ok);
            Assert.False(m.Discard(b, thrown).Ok);
        }

        [Fact]
        public void BaterDescartandoTiraTentosDosOutros()
        {
            var m = NewMatch(3);
            m.StartNextRound();
            // P0 tem 3 jogos menos uma carta; compra o 6♥ e bate descartando o 2♦.
            m.DebugSetup(new List<IList<Card>>
            {
                H("4c 5c 9o 9e 9p Jc Qc Kc 2o"),
                H("Ao 2e 3o 4o 5e 6e 7o 8o 9c"),
                H("Ap 2p 3e 4e 5p 6p 7p 8p 9c#"),
            }, vira: Card.Parse("10e"), stock: H("Ko 6c"), currentSeat: 0);

            Assert.True(m.DrawFromStock(0).Ok);
            Assert.False(m.Bater(0, Card.Parse("4c")).Ok); // descartando a carta errada não fecha
            var r = m.Bater(0, Card.Parse("2o"));
            Assert.True(r.Ok, r.Error);
            Assert.Equal(TurnPhase.FimDaRodada, m.Phase);
            // Batida limpa dobra: os outros perdem 2.
            Assert.Equal(10, m.GetTentos(0));
            Assert.Equal(8, m.GetTentos(1));
            Assert.Equal(8, m.GetTentos(2));
            var bateu = m.Log.Last(e => e.Type == GameEventType.Bateu);
            Assert.True(bateu.Clean);
        }

        [Fact]
        public void BatidaComCuringaPerdeUmTento()
        {
            var m = NewMatch(2);
            m.StartNextRound();
            // Vira 2♣ => 3♣ é curinga e preenche o 5♥ que falta.
            m.DebugSetup(new List<IList<Card>>
            {
                H("4c 6c 9o 9e 9p Jc Qc Kc 2o"),
                H("Ao 2e 3o 4o 5e 6e 7o 8o 9c"),
            }, vira: Card.Parse("2p"), stock: H("Ko 3p"), currentSeat: 0);
            m.DrawFromStock(0);
            Assert.True(m.Bater(0, Card.Parse("2o")).Ok);
            Assert.Equal(9, m.GetTentos(1));
        }

        [Fact]
        public void ModoBaixandoDez()
        {
            var rules = RuleSet.Default();
            rules.BaterMode = BaterMode.BaixandoDez;
            var m = NewMatch(2, rules);
            m.StartNextRound();
            m.DebugSetup(new List<IList<Card>>
            {
                H("4c 5c 9o 9e 9p Jc Qc Kc Ac"),
                H("Ao 2e 3o 4o 5e 6e 7o 8o 9c"),
            }, vira: Card.Parse("10e"), stock: H("Ko 6c"), currentSeat: 0);
            m.DrawFromStock(0);
            Assert.False(m.Bater(0, Card.Parse("Ac")).Ok); // nesta mesa não se descarta para bater
            Assert.True(m.Bater(0, null).Ok);               // Q-K-A + J... : 4-5-6, 9s, J-Q-K-A
        }

        [Fact]
        public void MonteAcabaReembaralhaDescarte()
        {
            var m = NewMatch(2);
            m.StartNextRound();
            m.DebugSetup(new List<IList<Card>>
            {
                H("Ao 3e 5o 7e 9o Jp Kc 2c 4p"),
                H("Ae 3o 5e 7o 9e Jc Kp 2p 4c"),
            }, vira: Card.Parse("10e"), stock: H("6o"), discard: H("8c 8o 8e"), currentSeat: 0);
            m.DrawFromStock(0);
            m.Discard(0, Card.Parse("Ao"));
            Assert.Equal(0, m.StockCount);
            Assert.True(m.DrawFromStock(1).Ok);
            Assert.Contains(m.Log, e => e.Type == GameEventType.StockReshuffled);
            Assert.Equal(Card.Parse("Ao"), m.DiscardTop);
        }

        [Fact]
        public void MonteAcabaRodadaAnulada()
        {
            var rules = RuleSet.Default();
            rules.EmptyStock = EmptyStockRule.RodadaAnulada;
            var m = NewMatch(2, rules);
            m.StartNextRound();
            m.DebugSetup(new List<IList<Card>> { H("Ao 3e 5o"), H("Ae 3o 5e") },
                vira: Card.Parse("10e"), stock: new List<Card>(), discard: H("8c 8o"), currentSeat: 0);
            Assert.True(m.DrawFromStock(0).Ok);
            Assert.Equal(TurnPhase.FimDaRodada, m.Phase);
            Assert.Equal(10, m.GetTentos(0));
            Assert.Equal(10, m.GetTentos(1));
        }

        [Fact]
        public void ViewNaoMostraCartasDosOutros()
        {
            var m = NewMatch(3);
            m.StartNextRound();
            var v = m.GetView(1);
            Assert.Equal(m.GetHand(1), v.Hand);
            Assert.Equal(new[] { 9, 9, 9 }, v.HandCounts);
            var privateEvents = m.Log.Where(e => e.VisibleToSeat.HasValue);
            Assert.All(privateEvents.Where(e => e.VisibleToSeat != 1), e => Assert.False(e.IsVisibleTo(1)));
        }

        [Theory]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(6)]
        [InlineData(8)]
        public void PartidaInteiraSoComBotsTerminaComUmVencedor(int players)
        {
            var rules = RuleSet.Default();
            rules.StartingTentos = 3; // partida curta para o teste
            var m = NewMatch(players, rules, seed: (ulong)(100 + players));
            var bots = Enumerable.Range(0, players)
                .Select(i => (IBot)new SimpleBot((BotDifficulty)(i % 3), (ulong)(i + 1))).ToList();
            var winner = BotDriver.PlayWholeMatch(m, bots);
            Assert.True(m.IsOver);
            Assert.NotNull(winner);
            Assert.Single(m.AliveSeats);
            Assert.Equal(winner, m.AliveSeats.Single());
        }

        [Fact]
        public void TentosAcabamEJogadorEliminadoSaiDaRoda()
        {
            var rules = RuleSet.Default();
            rules.StartingTentos = 1;
            rules.CleanBaterDoubles = false;
            var m = NewMatch(3, rules);
            m.StartNextRound();
            m.DebugSetup(new List<IList<Card>>
            {
                H("4c 5c 9o 9e 9p Jc Qc Kc 2o"),
                H("Ao 2e 3o 4o 5e 6e 7o 8o 9c"),
                H("Ap 2p 3e 4e 5p 6p 7p 8p 9c#"),
            }, vira: Card.Parse("10e"), stock: H("Ko 6c"), currentSeat: 0);
            m.DrawFromStock(0);
            Assert.True(m.Bater(0, Card.Parse("2o")).Ok);
            Assert.True(m.IsOver);
            Assert.Equal(0, m.WinnerSeat);
        }
    }
}
