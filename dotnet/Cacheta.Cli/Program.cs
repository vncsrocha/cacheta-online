using System.Diagnostics;
using System.Text;
using Cacheta.Core;
using Cacheta.Core.Bots;

// Protótipo jogável no terminal, antes de existir a tela no Unity.
//   dotnet run --project Cacheta.Cli                -> você contra 3 bots
//   dotnet run --project Cacheta.Cli -- --bots 5    -> você contra 5 bots
//   dotnet run --project Cacheta.Cli -- --sim 200   -> 200 partidas só de bots, com estatísticas

Console.OutputEncoding = Encoding.UTF8;
int botCount = 3;
int sim = 0;
ulong seed = 0;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--bots") botCount = int.Parse(args[++i]);
    else if (args[i] == "--sim") sim = int.Parse(args[++i]);
    else if (args[i] == "--seed") seed = ulong.Parse(args[++i]);
}

if (sim > 0) { Simulate(sim, seed == 0 ? 1 : seed); return; }
PlayInteractive(botCount, seed);

static void Simulate(int matches, ulong seed)
{
    var wins = new int[6];
    var rounds = new List<int>();
    var turnsPerRound = new List<int>();
    int clean = 0, bateu = 0, annulled = 0;
    var sw = Stopwatch.StartNew();
    for (int g = 0; g < matches; g++)
    {
        var names = Enumerable.Range(0, 6).Select(i => $"Bot{i}").ToList();
        var m = new CachetaMatch(names, RuleSet.Default(), seed + (ulong)g, firstDealer: g % 6);
        // Assentos 0-1 fáceis, 2-3 médios, 4-5 difíceis.
        var bots = Enumerable.Range(0, 6).Select(i => (IBot)new SimpleBot((BotDifficulty)(i / 2), seed * 31 + (ulong)(g * 7 + i))).ToList();
        int? w = BotDriver.PlayWholeMatch(m, bots);
        if (w.HasValue) wins[w.Value]++;
        rounds.Add(m.RoundNumber);
        int turns = 0;
        foreach (var e in m.Log)
        {
            if (e.Type == GameEventType.Discarded || e.Type == GameEventType.Bateu) turns++;
            if (e.Type == GameEventType.Bateu) { bateu++; if (e.Clean) clean++; turnsPerRound.Add(turns); turns = 0; }
            if (e.Type == GameEventType.RoundAnnulled) { annulled++; turns = 0; }
        }
    }
    Console.WriteLine($"{matches} partidas de 6 bots em {sw.Elapsed.TotalSeconds:F1}s");
    Console.WriteLine($"Vitórias  fácil: {wins[0] + wins[1]}  médio: {wins[2] + wins[3]}  difícil: {wins[4] + wins[5]}");
    Console.WriteLine($"Rodadas por partida: média {rounds.Average():F1}");
    Console.WriteLine($"Jogadas por rodada (todos os jogadores): média {turnsPerRound.Average():F1}");
    Console.WriteLine($"Batidas limpas: {100.0 * clean / Math.Max(1, bateu):F0}%   rodadas anuladas: {annulled}");
}

static void PlayInteractive(int botCount, ulong seed)
{
    var names = new List<string> { "Você" };
    string[] botNames = { "Zé do Bar", "Dona Cida", "Tonhão", "Marquinho", "Seu Juca", "Neide", "Baiano" };
    for (int i = 0; i < botCount; i++) names.Add(botNames[i % botNames.Length]);
    var m = new CachetaMatch(names, RuleSet.Default(), seed);
    var bots = names.Select((_, i) => (IBot)new SimpleBot(BotDifficulty.Medio, (ulong)(i + 11))).ToList();

    m.EventRaised += e =>
    {
        if (!e.IsVisibleTo(0)) return;
        string who = e.Seat >= 0 && e.Seat < names.Count ? names[e.Seat] : "";
        switch (e.Type)
        {
            case GameEventType.RoundStarted:
                Console.WriteLine($"\n===== Rodada {e.RoundNumber} | carteador: {who} | vira: {e.Card} | curinga: {m.Jokers.JokerRank switch { 1 => "A", 11 => "J", 12 => "Q", 13 => "K", var r => r.ToString() }} ({m.Rules.JokerRule}) =====");
                break;
            case GameEventType.DrewFromStock when e.Seat != 0: Console.WriteLine($"  {who} comprou do monte"); break;
            case GameEventType.DrewFromDiscard when e.Seat != 0: Console.WriteLine($"  {who} pegou {e.Card} do descarte"); break;
            case GameEventType.Discarded when e.Seat != 0: Console.WriteLine($"  {who} descartou {e.Card}"); break;
            case GameEventType.StockReshuffled: Console.WriteLine("  (monte acabou: descarte reembaralhado)"); break;
            case GameEventType.Bateu:
                Console.WriteLine($"\n*** {who} BATEU{(e.Clean ? " LIMPO" : "")}! {e.Message}  (os outros perdem {e.Amount})"); break;
            case GameEventType.RoundAnnulled: Console.WriteLine($"\n*** Rodada anulada: {e.Message}"); break;
            case GameEventType.PlayerEliminated: Console.WriteLine($"  {who} ficou sem tentos e saiu"); break;
            case GameEventType.MatchEnded: Console.WriteLine($"\n##### {who} levou a mesa! #####"); break;
        }
    };

    Console.WriteLine("Cacheta — protótipo de regras. Cartas: A 2..10 J Q K com ♣ ♦ ♥ ♠. * = curinga.");
    while (!m.IsOver)
    {
        if (m.Phase == TurnPhase.FimDaRodada)
        {
            Console.WriteLine("Tentos: " + string.Join("  ", m.AliveSeats.Select(s => $"{names[s]}={m.GetTentos(s)}")));
            Console.Write("[enter] para a próxima rodada ");
            Console.ReadLine();
            m.StartNextRound();
            continue;
        }
        if (m.CurrentSeat != 0)
        {
            var r = BotDriver.Step(m, bots[m.CurrentSeat]);
            if (!r.Ok) Console.WriteLine("Bot errou: " + r.Error);
            continue;
        }
        HumanStep(m);
    }

    static void HumanStep(CachetaMatch m)
    {
        var v = m.GetView(0);
        var hand = v.Hand.OrderBy(c => c).ToList();
        string Fmt(Card c) => c + (v.Jokers.IsJoker(c) ? "*" : "");
        Console.WriteLine();
        Console.WriteLine($"Monte: {v.StockCount} | Descarte: {(v.DiscardTop.HasValue ? Fmt(v.DiscardTop.Value) : "vazio")}");
        Console.WriteLine("Sua mão: " + string.Join("  ", hand.Select((c, i) => $"{i + 1}:{Fmt(c)}")));
        Console.WriteLine("Organizada: " + HandEvaluator.Arrange(hand, v.Rules, v.Jokers));

        if (v.Phase == TurnPhase.Comprar)
        {
            Console.Write("Comprar do [m]onte ou [d]escarte? ");
            var a = (Console.ReadLine() ?? "m").Trim().ToLowerInvariant();
            var r = a.StartsWith("d") ? m.DrawFromDiscard(0) : m.DrawFromStock(0);
            if (!r.Ok) Console.WriteLine("  " + r.Error);
            else if (m.Phase == TurnPhase.Descartar)
            {
                var got = m.GetHand(0).Except(hand).FirstOrDefault();
                Console.WriteLine($"  Você pegou {Fmt(got)}");
            }
            return;
        }

        Console.Write("Número da carta para descartar (ou 'b N' para bater descartando a carta N): ");
        var input = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        bool bater = input.StartsWith("b");
        if (bater) input = input.Substring(1).Trim();
        if (!int.TryParse(input, out int idx) || idx < 1 || idx > hand.Count) { Console.WriteLine("  Número inválido"); return; }
        var card = hand[idx - 1];
        var res = bater ? m.Bater(0, card) : m.Discard(0, card);
        if (!res.Ok) Console.WriteLine("  " + res.Error);
    }
}
