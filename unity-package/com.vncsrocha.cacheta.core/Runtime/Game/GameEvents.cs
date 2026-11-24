using System.Collections.Generic;

namespace Cacheta.Core
{
    public enum GameEventType
    {
        RoundStarted,
        Decidiu,            // público: o jogador disse se vai (Amount = 1) ou corre (Amount = 0)
        RodadaSemDisputa,   // público: todos correram menos um, que ganhou a rodada sem jogar
        BaterFurado,        // público: pediu para bater atravessando sem ter a mão; fica a próxima rodada de fora
        SentadoDeFora,      // público: o jogador está cumprindo a rodada de fora
        HandDealt,          // privado: as cartas que o jogador recebeu
        DrewFromStock,      // público: alguém comprou do monte (sem mostrar a carta)
        CardReceived,       // privado: qual carta veio do monte
        DrewFromDiscard,    // público: alguém pegou a carta do descarte (todos veem qual)
        Discarded,
        StockReshuffled,
        Bateu,
        RoundAnnulled,
        TentosChanged,
        PlayerEliminated,
        MatchEnded,
    }

    /// <summary>
    /// Tudo que acontece na partida vira um evento. A apresentação (Unity) anima a partir deles e o servidor
    /// os envia pela rede. <see cref="VisibleToSeat"/> nulo = público; senão, só aquele jogador pode ver.
    /// </summary>
    public sealed class GameEvent
    {
        public GameEventType Type;
        public int? VisibleToSeat;
        public int Seat = -1;
        public int RoundNumber;
        public Card? Card;
        public IReadOnlyList<Card> Cards;
        public HandArrangement Arrangement;
        public int Amount;
        public bool Clean;
        public bool FirstTurn;
        /// <summary>Batida de 10 cartas (2 jogos de 3 e 1 sequência de 4).</summary>
        public bool BatidaDeDez;
        /// <summary>Bateu pegando o descarte de outro jogador fora da vez.</summary>
        public bool Atravessando;
        public string Message;

        public bool IsVisibleTo(int seat) => VisibleToSeat == null || VisibleToSeat == seat;

        public override string ToString()
        {
            string who = Seat >= 0 ? $" jogador {Seat}" : "";
            string card = Card.HasValue ? " " + Card.Value : "";
            string priv = VisibleToSeat.HasValue ? $" (privado p/ {VisibleToSeat})" : "";
            return $"[{Type}]{who}{card}{(Message != null ? " " + Message : "")}{priv}";
        }
    }

    /// <summary>Resultado de uma ação: deu certo ou o motivo da recusa.</summary>
    public readonly struct ActionResult
    {
        public readonly bool Ok;
        public readonly string Error;

        private ActionResult(bool ok, string error)
        {
            Ok = ok;
            Error = error;
        }

        public static ActionResult Success() => new ActionResult(true, null);
        public static ActionResult Fail(string error) => new ActionResult(false, error);
        public override string ToString() => Ok ? "ok" : "erro: " + Error;
    }
}
