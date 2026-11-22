# Cacheta Online

Jogo de Cacheta para desktop/Steam, feito em Unity. Este repositório começa pelo **motor de regras**:
C# puro, sem dependência de Unity, para que o mesmo código rode no jogo, nos testes e depois no servidor
do multiplayer (servidor autoritativo).

## Estrutura

```
unity-package/com.vncsrocha.cacheta.core/   pacote Unity (o código de verdade fica aqui)
  Runtime/Model/      Card, Rng (embaralhamento determinístico)
  Runtime/Rules/      RuleSet (regras configuráveis), JokerInfo, HandEvaluator (valida jogos), Meld
  Runtime/Game/       CachetaMatch (partida, rodadas, tentos), GameEvent, PlayerView
  Runtime/Bots/       SimpleBot (fácil/médio/difícil), BotDriver
  Samples~/MesaLocal/ exemplo de MonoBehaviour usando o motor
dotnet/                                    projetos .NET que compilam o mesmo código fora do Unity
  Cacheta.Core/       biblioteca netstandard2.1 / C# 9 (mesmas restrições do Unity)
  Cacheta.Tests/      testes xUnit
  Cacheta.Cli/        protótipo jogável no terminal + simulador de partidas de bots
```

## Rodando sem Unity

Precisa do [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd dotnet
dotnet test                                         # testes
dotnet run --project Cacheta.Cli                    # você contra 3 bots no terminal
dotnet run --project Cacheta.Cli -- --bots 5        # você contra 5 bots
dotnet run --project Cacheta.Cli -- --sim 100       # 100 partidas de 6 bots, com estatísticas
```

## Usando no Unity

No projeto Unity: **Window > Package Manager > + > Add package from disk...** e escolha
`unity-package/com.vncsrocha.cacheta.core/package.json`. O exemplo "Mesa local" aparece na aba Samples do pacote.

Regra de ouro: a cena **nunca** muda o estado do jogo por conta própria. Ela chama ações
(`DrawFromStock`, `DrawFromDiscard`, `Discard`, `Bater`), recebe `ActionResult` e anima a partir dos
`GameEvent`. Assim, no online basta trocar a chamada local por uma mensagem ao servidor.

## Regras padrão ("regra da casa")

Tudo isso está em `RuleSet` e pode mudar por sala.

| Regra | Padrão | Opções |
|---|---|---|
| Baralhos / jogadores / cartas na mão | 2 / 2 a 8 / 9 | configurável |
| Curinga | carta seguinte à vira, **mesmo naipe** (depois do K vem o A) | mesma cor, qualquer naipe, sem curinga |
| Curingas por jogo | no máximo 1 | sem limite |
| Curinga em trinca | pode | só em sequência |
| Sequência | 3+ do mesmo naipe; Ás baixo (A-2-3) e alto (Q-K-A), sem dar a volta | permitir K-A-2; Ás só baixo |
| Trinca | 3 ou 4 do mesmo valor, naipes diferentes | aceitar naipe repetido |
| Como bate | compra, descarta 1 e as 9 restantes fecham em jogos | baixar as 10 sem descartar; os dois |
| Bater com carta do descarte | pode | não pode |
| Descartar a carta que pegou do descarte | não pode | pode |
| Monte acabou | reembaralha o descarte (menos a de cima) | rodada anulada |
| Tentos | cada um começa com 10; quem não bateu perde 1 | configurável |
| Batida limpa (sem curinga como coringa) | os outros perdem 2 | desligar |
| Batida de mão (primeira jogada) | normal | os outros perdem em dobro |
| Fim | quem chega a 0 tentos sai; o último que sobra leva a mesa | — |

Curinga usado no próprio lugar (ex.: 3♣ curinga em 2♣ 3♣ 4♣) conta como carta normal, então a batida continua limpa.

## Bots

`SimpleBot` olha só a `PlayerView` (não vê a mão dos outros). Mede a mão por cartas soltas e "quase-jogos",
pega o descarte quando melhora de verdade e descarta a carta que menos faz falta. Fácil erra ~30% dos descartes
e quase nunca pega do descarte; médio erra ~8%; difícil não erra.
