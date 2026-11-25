# Ossos dos dedos dos personagens

Os modelos 3D dos personagens vêm do gerador com 24 ossos e **sem dedos** (a mão inteira é um osso só). Estes scripts
acham os dedos na malha de cada mão e criam os ossos e os pesos, para as mãos poderem segurar as cartas.

O que sai em cada mão:

- 20 nós novos, filhos de `LeftHand` / `RightHand`: `{Lado}Hand{Index|Middle|Ring|Pinky|Thumb}{1..4}`.
  Os nós 1 a 3 são ossos de verdade (30 ossos a mais por personagem, 54 no total); o 4 marca só a ponta do dedo.
- Os nós novos saem sem giro (rotação identidade). Por isso, no Unity, a mesma direção serve para dobrar as três
  falanges de um dedo (veja `CardHolder.MakeHandRig`).
- Pesos: a parte da mão em cada vértice é dividida entre base, meio e ponta do dedo, com transições suaves nas juntas;
  o polegar pega também a base da palma do lado dele.
- O resto do arquivo (malha, textura, animação de sentado) fica igual.

## Como usar

Precisa de Python 3 com `numpy`, `scipy` e `Pillow`.

Os scripts trabalham na pasta `~/cena`: o modelo original fica em `~/cena/<Nome>.glb` e o resultado sai em
`~/cena/rig/<Nome>.glb`, junto de `~/cena/rig/<Nome>.json` (posição das juntas de cada dedo, para conferir).

```sh
# cria os dedos (um ou vários personagens)
python3 -I writeglb.py TioBeto DonaCleide Marcela

# desenha as juntas achadas em cada mão, para conferir (sai em ~/rig/diag2.jpg)
python3 -I diag2.py TioBeto

# mão que veio fundida (dedos grudados, como a da Ana): troca pela mão de outro personagem já com dedos.
# O doador precisa ter passado pelo writeglb.py antes. Ajusta tamanho e cor da pele e liga no punho.
python3 -I transplante.py Ana Marcela
```

Depois, suba o `.glb` novo para algum lugar público e troque a URL em
`UnityProject/Assets/Editor/BaixarPersonagens.cs`: o Unity baixa a versão nova sozinho.

## Arquivos

| Arquivo | O que faz |
| --- | --- |
| `glb.py` | Lê o `.glb` (JSON + binário) e calcula a posição de cada nó no mundo. |
| `view.py` | Carrega malha, ossos e pesos; eixos da mão; desenho simples para conferir. |
| `zmap.py` | Mapa de profundidade da mão (de cima), usado para achar os vãos entre os dedos. |
| `dedos.py` | Mão como grafo; separa o polegar cortando a malha. |
| `dedos2.py` | Acha os vãos e os quatro dedos, as juntas de cada um e os pesos novos. |
| `diag2.py` | Junta tudo por mão (`analisa`) e desenha a folha de conferência. |
| `writeglb.py` | Grava o `.glb` novo com os ossos, os pesos e as matrizes de pose de origem. |
| `transplante.py` | Troca uma mão fundida pela mão de outro personagem. |

Medidas que funcionaram nos 8 personagens: base do dedo a 1,22 do comprimento visível a partir da ponta, meio do
dedo a 46% e ponta a 75% (da base para a ponta); polegar com eixo pelo PCA da parte solta.
