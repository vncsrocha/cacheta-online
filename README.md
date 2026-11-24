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
UnityProject/Assets/Scripts/               o jogo no Unity (arte provisória, tudo montado por código)
  App/        GameApp (entrada, troca de telas), dados do perfil/opções/sala, palco 3D do menu
  UI/         kit de interface provisório (IMGUI) e telas: menu, perfil, opções, criar/entrar em sala
  Visual/     personagens, ambientes e cartas provisórios (primitivos e texturas geradas)
  Table/      TableController: mesa 3D, câmera, arrastar cartas, bots e HUD
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

## Testando no Unity

A pasta `UnityProject/` é um projeto Unity pronto, que já aponta para o pacote do motor.

1. Instale o [Unity Hub](https://unity.com/download) e uma versão **Unity 6 LTS** (6000.x).
2. No Hub: **Add > Add project from disk** e escolha a pasta `UnityProject`.
3. Aperte **Play** em qualquer cena: o jogo se monta sozinho (não há cenas nem prefabs ainda).

O que existe (tudo funcional, com arte provisória):

- **Menu inicial**: nome do jogo e seu personagem à esquerda; à direita Jogar Cacheta, Jogar Cachetex,
  Entrar em uma sala, Personalizar perfil, Opções e Sair. Na primeira vez abre direto a criação do personagem.
- **Perfil**: nome, gênero, tom de pele, cabelo e cor, camisa, calça, chapéu, óculos e bigode, com pré-visualização.
- **Opções**: volume (geral, música, efeitos), resolução, tela cheia, qualidade, VSync, velocidade dos bots,
  dicas e confirmação antes de bater. Tudo salvo.
- **Criar sala**: modo, ambiente (Quintal ao entardecer, Boteco, Quiosque na praia), número de bots e dificuldade.
  A sala online e o "Entrar em uma sala" já têm a tela, mas só funcionam quando o multiplayer existir.
- **Mesa 3D**: vista sentado à mesa com os adversários em volta; zoom na mão (botão direito, roda do mouse,
  Espaço ou botão "Ver minha mão"); **arraste as cartas** para reorganizar a mão e solte no descarte para
  descartar; clique no monte ou no descarte para comprar; HUD fixa no canto com a mesa vista de cima
  (vira, monte, descarte, tentos e o que aconteceu); Esc pausa.

O Cachetex ainda não tem regras próprias: por enquanto é a Cacheta sem limite de curingas por jogo
(`RoomConfig.BuildRules`).

Para usar o motor em outro projeto Unity: **Window > Package Manager > + > Add package from disk...** e escolha
`unity-package/com.vncsrocha.cacheta.core/package.json`.

Regra de ouro: a cena **nunca** muda o estado do jogo por conta própria. Ela chama ações
(`DrawFromStock`, `DrawFromDiscard`, `Discard`, `Bater`), recebe `ActionResult` e reage aos
`GameEvent`. Assim, no online basta trocar a chamada local por uma mensagem ao servidor.

## Regras padrão (Cacheta Clássica)

Tudo isso está em `RuleSet` e pode mudar por sala.

**Vai ou corre.** A vira aparece, cada um olha a mão e, na ordem da mesa, diz se vai ou corre. Quem fala primeiro
gira a cada rodada (como o botão do dealer). Se todo mundo correr menos um, ele ganha a rodada sem jogar.
Quem correu não joga a rodada.

| Regra | Padrão | Opções |
|---|---|---|
| Baralhos / jogadores / cartas na mão | 2 / 3 a 8 / 9 | configurável |
| Curinga | carta seguinte à vira, **mesmo naipe** (depois do K vem o A) | mesma cor, qualquer naipe, sem curinga |
| Curingas por jogo | no máximo 1 | sem limite |
| Curinga em trinca | pode | só em sequência |
| Sequência | 3+ do mesmo naipe; Ás baixo (A-2-3) e alto (Q-K-A), sem dar a volta | permitir K-A-2; Ás só baixo |
| Trinca | 3 ou 4 do mesmo valor, naipes diferentes | aceitar naipe repetido |
| Como bate | compra, descarta 1 e as 9 restantes fecham em jogos | baixar as 10 sem descartar; os dois |
| Bater com carta do descarte | pode | não pode |
| Descartar a carta que pegou do descarte | não pode | pode |
| Monte acabou | reembaralha o descarte (menos a de cima) | rodada anulada |
| Tentos | cada um começa com 10 | configurável |
| Quando alguém bate | quem correu perde 1, quem foi e perdeu perde 2, quem bateu não perde | configurável |
| Batida de 10 (dois jogos de 3 + sequência de 4, pode ter curinga na de 4) | quem correu perde 2, quem foi perde 3 | desligar |
| Bater atravessando | qualquer um pode pegar o descarte de outro, fora da vez, só para bater na hora | desligar |
| Bater furado (pediu para bater atravessando sem a mão fechar) | fica a próxima rodada de fora, a carta volta para o descarte | configurável |
| Batida limpa (sem curinga como coringa) | normal, não dobra | dobrar |
| Batida de mão (primeira jogada) | normal | os outros perdem em dobro |
| Fim | quem chega a 0 tentos sai; o último que sobra leva a mesa | — |

Curinga usado no próprio lugar (ex.: 3♣ curinga em 2♣ 3♣ 4♣) conta como carta normal, então a batida continua limpa.

## Bots

`SimpleBot` olha só a `PlayerView` (não vê a mão dos outros). Mede a mão por cartas soltas e "quase-jogos",
pega o descarte quando melhora de verdade e descarta a carta que menos faz falta. Fácil erra ~30% dos descartes
e quase nunca pega do descarte; médio erra ~8%; difícil não erra.
