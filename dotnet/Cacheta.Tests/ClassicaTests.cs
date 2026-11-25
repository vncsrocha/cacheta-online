using System.Collections.Generic;
using System.Linq;
using Cacheta.Core;
using Cacheta.Core.Bots;
using Xunit;
using static Cacheta.Tests.HandEvaluatorTests;

namespace Cacheta.Tests
{
    /// <summary>Regras da Cacheta Clássica: vai ou corre, batida de 10, bater atravessando e bater furado.</summary>
    public class ClassicaTests
    {
        private static CachetaMatch NewMatch(int players = 4, ulong seed = 42)
            => new CachetaMatch(Enumerable.Range(0, players).Select(i => "P" + i).ToList(), RuleSet.Default(), seed);

        // P0 compra o 6♥ e bate descartando o 2♦ (3 jogos de 3).
        private static List<IList<Card>> MaoQuaseBatendo() => new List<IList<Card>>
        {
            H("4c 5c 9o 9e 9p Jc Qc Kc 2o"),
            H("Ao 2e 3o 4o 5e 6e 7o 8o 9c"),
            H("Ap 2p 3e 4e 5p 6p 7p 8p 9c#"),
            H("Ae 2c 3c 7c 8c 10o Jo Qo Ko"),
        };

        [Fact]
        public void RodadaComecaDecidindoNaOrdemDaMesa()
        {
            var m = NewMatch();
            m.StartNextRound();
            Assert.Equal(TurnPhase.Decidir, m.Phase);
            int first = (m.DealerSeat + 1) % 4;
            Assert.Equal(first, m.CurrentSeat);
            Assert.False(m.DrawFromStock(first).Ok);                  // primeiro decide
            Assert.False(m.Decide((first + 1) % 4, true).Ok);        // fora da vez
            Assert.True(m.Decide(first, true).Ok);
            Assert.Equal((first + 1) % 4, m.CurrentSeat);
        }

        [Fact]
        public void QuemFalaPrimeiroGiraACadaRodada()
        {
            var m = NewMatch();
            m.StartNextRound();
            int first = m.CurrentSeat;
            for (int i = 0; i < 3; i++) m.Decide(m.CurrentSeat, false); // todos correm, o último ganha
            Assert.Equal(TurnPhase.FimDaRodada, m.Phase);
            m.StartNextRound();
            Assert.Equal((first + 1) % 4, m.CurrentSeat);
        }

        [Fact]
        public void TodosCorremMenosOUltimoQueGanhaSemJogar()
        {
            var m = NewMatch();
            m.StartNextRound();
            var order = Enumerable.Range(1, 4).Select(i => (m.DealerSeat + i) % 4).ToList();
            m.Decide(order[0], false);
            m.Decide(order[1], false);
            m.Decide(order[2], false);
            Assert.Equal(TurnPhase.FimDaRodada, m.Phase);
            Assert.Contains(m.Log, e => e.Type == GameEventType.RodadaSemDisputa && e.Seat == order[3]);
            Assert.Equal(10, m.GetTentos(order[3]));
            Assert.Equal(9, m.GetTentos(order[0]));
            Assert.Equal(9, m.GetTentos(order[1]));
            Assert.Equal(9, m.GetTentos(order[2]));
        }

        [Fact]
        public void SoUmFoiEleGanhaSemJogar()
        {
            var m = NewMatch();
            m.StartNextRound();
            var order = Enumerable.Range(1, 4).Select(i => (m.DealerSeat + i) % 4).ToList();
            m.Decide(order[0], true);
            m.Decide(order[1], false);
            m.Decide(order[2], false);
            m.Decide(order[3], false);
            Assert.Equal(TurnPhase.FimDaRodada, m.Phase);
            Assert.Contains(m.Log, e => e.Type == GameEventType.RodadaSemDisputa && e.Seat == order[0]);
        }

        [Fact]
        public void QuemCorreuNaoJoga()
        {
            var m = NewMatch();
            m.StartNextRound();
            var order = Enumerable.Range(1, 4).Select(i => (m.DealerSeat + i) % 4).ToList();
            m.Decide(order[0], true);
            m.Decide(order[1], false);
            m.Decide(order[2], true);
            m.Decide(order[3], false);
            Assert.Equal(TurnPhase.Comprar, m.Phase);
            Assert.Equal(order[0], m.CurrentSeat);
            m.DrawFromStock(order[0]);
            m.Discard(order[0], m.GetHand(order[0])[0]);
            Assert.Equal(order[2], m.CurrentSeat); // pulou quem correu
        }

        [Fact]
        public void BatidaNormalCorreuPerdeUmFoiPerdeDois()
        {
            var m = NewMatch();
            m.StartNextRound();
            m.DebugSetup(MaoQuaseBatendo(), vira: Card.Parse("10e"), stock: H("Ko 6c"), currentSeat: 0, correram: new[] { 3 });
            m.DrawFromStock(0);
            Assert.True(m.Bater(0, Card.Parse("2o")).Ok);
            Assert.Equal(10, m.GetTentos(0));
            Assert.Equal(8, m.GetTentos(1));
            Assert.Equal(8, m.GetTentos(2));
            Assert.Equal(9, m.GetTentos(3)); // correu
        }

        [Fact]
        public void BatidaDeDezCorreuPerdeDoisFoiPerdeTres()
        {
            var m = NewMatch();
            m.StartNextRound();
            // 4♥ 5♥ 6♥ | 9♦ 9♠ 9♣ | J♥ Q♥ K♥ A♥ (sequência de 4) = 10 cartas
            m.DebugSetup(new List<IList<Card>>
            {
                H("4c 5c 9o 9e 9p Jc Qc Kc Ac"),
                H("Ao 2e 3o 4o 5e 6e 7o 8o 9c"),
                H("Ap 2p 3e 4e 5p 6p 7p 8p 9c#"),
                H("Ae 2c 3c 7c 8c 10o Jo Qo Ko"),
            }, vira: Card.Parse("10e"), stock: H("Ko 6c"), currentSeat: 0, correram: new[] { 3 });
            m.DrawFromStock(0);
            var r = m.Bater(0, null);
            Assert.True(r.Ok, r.Error);
            Assert.True(m.Log.Last(e => e.Type == GameEventType.Bateu).BatidaDeDez);
            Assert.Equal(7, m.GetTentos(1));
            Assert.Equal(7, m.GetTentos(2));
            Assert.Equal(8, m.GetTentos(3));
        }

        [Fact]
        public void BatidaDeDezNaoAceitaQuadraNoLugarDaSequencia()
        {
            // 4♥ 5♥ 6♥ | 7♦ 8♦ 9♦ | quadra de 9 (♥ ♠ ♣ ♦): 10 cartas completas, mas sem sequência de 4
            var hand = H("4c 5c 6c 7o 8o 9o 9c 9e 9p 9o#");
            var rules = RuleSet.Default();
            Assert.True(HandEvaluator.Arrange(hand, rules, JokerInfo.None).IsComplete);
            Assert.Null(HandEvaluator.BatidaDeDez(hand, rules, JokerInfo.None));
        }

        [Fact]
        public void BatidaDeDezPodeUsarCuringaNaSequenciaDeQuatro()
        {
            // Vira 2♣ => 3♣ é curinga e entra no lugar do Q♥: J♥ _ K♥ A♥
            var hand = H("4c 5c 6c 9o 9e 9p Jc 3p Kc Ac");
            var jokers = new JokerInfo(Card.Parse("2p"), JokerSuitRule.MesmoNaipe);
            Assert.NotNull(HandEvaluator.BatidaDeDez(hand, RuleSet.Default(), jokers));
        }

        [Fact]
        public void BaterAtravessandoComODescarteDeOutro()
        {
            var m = NewMatch();
            m.StartNextRound();
            // P1 descarta o 6♥, que fecha a mão do P0 fora da vez.
            m.DebugSetup(new List<IList<Card>>
            {
                H("4c 5c 9o 9e 9p Jc Qc Kc 2o"),
                H("Ao 2e 3o 4o 5e 6e 7o 8o 6c"),
                H("Ap 2p 3e 4e 5p 6p 7p 8p 9c#"),
                H("Ae 2c 3c 7c 8c 10o Jo Qo Ko"),
            }, vira: Card.Parse("10e"), stock: H("Ko 2e"), currentSeat: 1);
            m.DrawFromStock(1);
            m.Discard(1, Card.Parse("6c"));
            Assert.Equal(2, m.CurrentSeat);
            Assert.False(m.CanAtravessar(1));  // quem descartou não pode
            Assert.True(m.CanAtravessar(0));
            Assert.True(m.BaterAtravessando(0, Card.Parse("2o")).Ok);
            var bateu = m.Log.Last(e => e.Type == GameEventType.Bateu);
            Assert.Equal(0, bateu.Seat);
            Assert.True(bateu.Atravessando);
            Assert.Equal(TurnPhase.FimDaRodada, m.Phase);
        }

        [Fact]
        public void NaoAtravessaDepoisQueOProximoComprou()
        {
            var m = NewMatch();
            m.StartNextRound();
            m.DebugSetup(MaoQuaseBatendo(), vira: Card.Parse("10e"), stock: H("Ko 2e 3e"), currentSeat: 1);
            m.DrawFromStock(1);
            m.Discard(1, m.GetHand(1)[0]);
            m.DrawFromStock(2);
            Assert.False(m.CanAtravessar(0));
        }

        [Fact]
        public void BaterFuradoFicaAProximaRodadaDeFora()
        {
            var m = NewMatch();
            m.StartNextRound();
            m.DebugSetup(MaoQuaseBatendo(), vira: Card.Parse("10e"), stock: H("Ko 2e"), currentSeat: 1);
            m.DrawFromStock(1);
            m.Discard(1, Card.Parse("Ao"));   // o A♦ não fecha a mão do P0
            Assert.True(m.BaterAtravessando(0, Card.Parse("2o")).Ok);
            Assert.Contains(m.Log, e => e.Type == GameEventType.BaterFurado && e.Seat == 0);
            Assert.True(m.WillSitOutNext(0));
            Assert.Equal(Card.Parse("Ao"), m.DiscardTop); // a carta voltou para o descarte
            Assert.Equal(9, m.GetHand(0).Count);
            Assert.Equal(TurnPhase.Comprar, m.Phase);     // a rodada continua

            // Termina a rodada e confere que o P0 fica de fora na próxima.
            m.DebugSetup(MaoQuaseBatendo(), vira: Card.Parse("10e"), stock: H("Ko 6c"), currentSeat: 0);
            m.DrawFromStock(0);
            m.Bater(0, Card.Parse("2o"));
            m.StartNextRound();
            Assert.Equal(SeatStatus.Sentado, m.GetStatus(0));
            Assert.Empty(m.GetHand(0));
            Assert.NotEqual(0, m.CurrentSeat);
        }

        [Fact]
        public void BotCorajosoVaiMaisQueOMedroso()
        {
            int corajoso = 0, medroso = 0;
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var m = NewMatch(4, seed);
                m.StartNextRound();
                var v = m.GetView(m.CurrentSeat);
                if (new SimpleBot(BotDifficulty.Dificil, seed, BotPersonality.Kamikaze()).ChooseVai(v)) corajoso++;
                if (new SimpleBot(BotDifficulty.Dificil, seed, BotPersonality.Medroso()).ChooseVai(v)) medroso++;
            }
            Assert.True(corajoso > medroso * 2, $"kamikaze {corajoso}, medroso {medroso}");
        }

        [Fact]
        public void PowerUpsNaoExistemEBatidaLimpaNaoDobra()
        {
            var r = RuleSet.Default();
            Assert.False(r.CleanBaterDoubles);
            Assert.Equal(3, r.MinPlayers);
            Assert.Equal(8, r.MaxPlayers);
        }
    }
}
