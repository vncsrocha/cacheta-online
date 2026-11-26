# Peças 3D e texturas dos cenários

Cada cenário tem um **pacote** `.glb` com as peças 3D dele (geladeira, balcão, TV...), uma peça por nó. O pacote não
vai para o git (é grande): o Unity baixa sozinho para `Assets/Resources/Cenarios/` (veja
`Assets/Editor/BaixarCenarios.cs`, menu **Carteado > Baixar cenários 3D**). Sem o pacote, o cenário monta cada peça
com formas simples, como antes.

As texturas e quadros próprios (ladrilho, azulejo, cartazes...) são pequenos e ficam no git, em
`Assets/Resources/Texturas/<Cenario>/`.

## Como o jogo usa

- `PropLibrary.Spawn(pai, "BarDoTioBeto", "Geladeira", posição, giro)` põe uma cópia da peça. Toda peça do pacote
  já vem em metros, centrada, apoiada no chão (y = 0) e com a frente para +Z.
- Os materiais viram o shader do jogo (`Cacheta/ToonOutline`, chapado com contorno). O nome do material liga opções:
  `__glowN` brilha sozinho com força N (lâmpadas, telas), `__cut` recorta pela transparência, `__2s` desenha as duas
  faces, `__nolinha` fica sem contorno.
- O pós-efeito (`PostFx`: brilho em volta das lâmpadas, cor quente, vinheta) é ligado por cenário, em
  `EnvironmentBuilder` (`PostFx.Apply(cam, PostLook.Boteco)`).

## Como refazer um pacote

Precisa de Node 18+ (`npm install` nesta pasta) e Python 3 com `numpy` e `Pillow`.

```sh
# 1. peças grátis do Poly Haven (CC0), em glTF 1k  ->  ph/<id>/<id>_1k.gltf
python3 dl.py ceiling_fan television_02 wall_clock boombox steel_frame_shelves_01 \
               cardboard_box_01 plastic_crate_03 small_lpg_tank metal_trash_can

# 2. cada peça fica leve para o jogo (malha simplificada, texturas em JPEG)  ->  out/<id>.glb
#    as peças feitas com IA entram aqui também: out/ia_<nome>.glb
node prep.mjs ph/ceiling_fan/ceiling_fan_1k.gltf out/ceiling_fan.glb 8000 512
node prep.mjs ia3d/balcao.glb out/ia_balcao.glb 20000 1024

# 3. junta tudo num pacote só, com o tamanho e a âncora de cada peça (rode de dentro de uma pasta irmã de out/)
node pack.mjs BarDoTioBeto.glb BarDoTioBeto.json

# 4. texturas e quadros  ->  tex/*.jpg  (copie para Assets/Resources/Texturas/Bar/)
python3 texturas.py
```

Depois de subir o pacote novo para um endereço público, troque a URL dele em `BaixarCenarios.cs`: trocar a URL faz
cada máquina baixar a versão nova.

`BarDoTioBeto.json` descreve o pacote do bar: de que arquivo vem cada peça, a medida (`fit`), escala, âncora (`base`
no chão, `top` no teto, `wall`/`wallbase` na parede), materiais que brilham (`glow`), recortam (`cut`) ou somem
(`hide`). A explicação de cada campo está no começo de `pack.mjs`.

## Bar do Tio Beto (26/11/2026)

Peças grátis do [Poly Haven](https://polyhaven.com) (licença CC0, uso livre, sem precisar de crédito):

| Peça no jogo | Modelo |
|---|---|
| Ventilador | `ceiling_fan` (as pás giram: nó `ceiling_fan_blades`) |
| TV | `television_02` (a tela com o jogo é um quadro do próprio jogo por cima) |
| Relogio | `wall_clock` |
| Radio | `boombox` |
| Estante | `steel_frame_shelves_01` com `cardboard_box_01` e `plastic_crate_03` nas prateleiras |
| Botijao | `small_lpg_tank` |
| Lixeira | `metal_trash_can` |

Peças próprias, feitas com IA (imagem de referência própria e depois imagem para 3D): **Balcao** (azulejo verde,
tampo de granito e estufa de salgados), **Geladeira** de bebidas, **Freezer** de picolé, **Prateleira** de cachaças,
**Banqueta**, **Planta** (espada-de-são-jorge) e **Engradados**.

Texturas e quadros próprios: o desenho do ladrilho hidráulico, o quadro de preços, a placa "Fiado só amanhã", o
cartaz do torneio de cacheta, a foto do time, a tela da TV e a lousa do cavalete foram feitos com IA; azulejo,
parede e forro são desenhados pelo `texturas.py`.

As luminárias são feitas no próprio código (calha com duas lâmpadas que brilham), não vêm do pacote.

Regra das artes: tudo é próprio. Pode lembrar o clima de um boteco de bairro, mas nada copiado de outro jogo, e
nenhum nome de marca ou de jogo nas descrições usadas para gerar as imagens.
