# Plantas dos cenários

Cada arquivo aqui é a planta de um cenário, igual à do artefato "Plantas dos cenários":
medidas da sala, vãos nas paredes (porta de enrolar, vão livre, janela) e cada peça com
posição, tamanho e giro, em metros (mesa na origem, você em -Z, parede do fundo em +Z).

- `Plantas/<Cenario>.json`: a planta atual. É o que o jogo usa.
- `Plantas/Originais/<Cenario>.json`: a planta de partida, que é o que o código monta.

O `EnvironmentBuilder` monta o cenário pelo código e depois aplica a diferença entre as duas:
peça movida, girada ou com outro tamanho muda de lugar; peça apagada some; peça nova
aparece como um bloco na cor e no tamanho da planta, até ganhar um modelo.
Os vãos e as medidas da sala vêm direto da planta atual.

Para levar as mudanças do artefato para o jogo, os arquivos de `Plantas/` são trocados
pelos da coleção `plantas` do artefato (a coleção `originais` não muda).
