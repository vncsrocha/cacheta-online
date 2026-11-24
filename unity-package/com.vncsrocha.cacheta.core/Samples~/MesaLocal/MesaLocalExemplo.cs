using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cacheta.Core;
using Cacheta.Core.Bots;
using UnityEngine;

/// <summary>
/// Exemplo mínimo de como a cena do Unity conversa com o motor de regras.
/// Coloque num GameObject vazio e dê Play: o jogador 0 é você (use os métodos públicos a partir de botões),
/// os outros são bots que jogam com uma pausa para dar tempo de animar.
/// A arte/animação de verdade fica na camada de apresentação: ela só reage aos GameEvents.
/// </summary>
public class MesaLocalExemplo : MonoBehaviour
{
    [Range(2, 7)] public int numeroDeBots = 3;
    public BotDifficulty dificuldade = BotDifficulty.Medio;
    public float pausaDosBots = 0.8f;

    private CachetaMatch _match;
    private List<IBot> _bots;

    private void Start()
    {
        var nomes = new List<string> { "Você" };
        for (int i = 0; i < numeroDeBots; i++) nomes.Add("Bot " + (i + 1));
        _match = new CachetaMatch(nomes, RuleSet.Default());
        _bots = nomes.Select((_, i) => (IBot)new SimpleBot(dificuldade, (ulong)(i + 1))).ToList();
        _match.EventRaised += OnGameEvent;
        _match.StartNextRound();
        StartCoroutine(LoopDosBots());
    }

    private void OnGameEvent(GameEvent e)
    {
        if (!e.IsVisibleTo(0)) return; // nunca mostre cartas que não são suas
        Debug.Log(e);                  // aqui entram animações, sons, atualização da mão etc.
    }

    private IEnumerator LoopDosBots()
    {
        while (!_match.IsOver)
        {
            if (_match.Phase == TurnPhase.FimDaRodada)
            {
                yield return new WaitForSeconds(2f);
                _match.StartNextRound();
            }
            else if (_match.CurrentSeat != 0)
            {
                yield return new WaitForSeconds(pausaDosBots);
                // Antes de comprar, qualquer bot pode bater atravessando com o descarte de outro.
                if (_match.Phase == TurnPhase.Comprar && BotDriver.TryAtravessar(_match, _bots, s => s != 0)) continue;
                BotDriver.Step(_match, _bots[_match.CurrentSeat]);
            }
            else yield return null; // vez do jogador humano
        }
    }

    // Ligue estes métodos a botões / arrastar cartas.
    public void Vou() => Log(_match.Decide(0, true));
    public void Corro() => Log(_match.Decide(0, false));
    public void BaterAtravessando(int cardIdDescarte) => Log(_match.BaterAtravessando(0, Card.FromId(cardIdDescarte)));
    public void ComprarDoMonte() => Log(_match.DrawFromStock(0));
    public void ComprarDoDescarte() => Log(_match.DrawFromDiscard(0));
    public void Descartar(int cardId) => Log(_match.Discard(0, Card.FromId(cardId)));
    public void Bater(int cardIdDescarte) => Log(_match.Bater(0, Card.FromId(cardIdDescarte)));

    private static void Log(ActionResult r)
    {
        if (!r.Ok) Debug.LogWarning(r.Error);
    }
}
