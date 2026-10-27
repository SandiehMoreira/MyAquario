# MyAquario 🐟

Aquário virtual que mora na área de trabalho do Windows: um pet que você alimenta e cuida.
Começa com um betta num aquário de 10 L e, conforme você cuida, ganha aquários maiores e novos peixes.

**Baixar:** [MyAquario-Setup.exe](https://github.com/SandiehMoreira/MyAquario/releases/latest/download/MyAquario-Setup.exe)
(Windows 10/11, não precisa de administrador nem instalar nada além dele).

## Como jogar

| Ação | Como |
|---|---|
| Dar comida | Clique no potinho de ração ao lado do aquário |
| Chamar o betta | Clique no vidro: ele nada até lá (se estiver com fome, não vem). Não bata demais! |
| Ver o status | Passe o mouse em cima do aquário: as informações aparecem numa janelinha acima dele |
| Mover | Arraste o aquário |
| Limpar, gráficos, configurações | Botão direito no aquário ou no ícone perto do relógio |
| Aquário maior | Clique no selo dourado quando ele aparecer |

- **Fome** sobe com o tempo (cerca de 16 h até ficar faminto), inclusive com o PC desligado.
- **Água** suja aos poucos (cerca de 3,5 dias). Comida que sobra no fundo suja mais rápido.
- **Humor** depende das duas. Feliz, o betta faz ninho de bolhas na superfície; muito feliz, ele dá pulinhos para fora d'água.
- **Pontos de cuidado** vêm de tempo feliz, de alimentar quando está com fome e de limpar quando está sujo.
- Os peixes **não morrem**: ficam pálidos e lentos, e se recuperam quando você cuida.
- A luz do aquário apaga à noite (22h–7h) e os peixes descansam.

| Nível | Aquário | Pontos | Chegam |
|---|---|---|---|
| 1 | Aquário do Betta (10 L) | — | 1 betta |
| 2 | Aquário Plantado (30 L) | 150 | 3 coridoras (limpam a comida do fundo) |
| 3 | Aquário Comunitário (60 L) | 500 | 6 rasboras-arlequim |
| 4 | Aquário Grande (120 L) | 1200 | 8 neons |

## Gráficos

| Nível | Estilo | Onde está |
|---|---|---|
| Básico | pixel art | dentro do programa |
| Normal | 2D cartoon com contorno | `pack-normal.zip` (~35 KB), baixado quando escolhido |
| Ultra | realista: luz no fundo, raios de sol, sombras, texturas HD | `pack-ultra.zip` (~0,9 MB), baixado quando escolhido |

- **Automático** olha RAM, processador e placa de vídeo e escolhe o gráfico.
- Ao descer de gráfico, o programa pergunta se quer **apagar os pacotes** que ficaram sem uso, ou mantê-los para trocar quando quiser.
- Os pacotes vão para `%LOCALAPPDATA%\MyAquario\packs`.

## Economia de energia

| Situação | O que acontece |
|---|---|
| Usando o PC | gráfico escolhido (20 / 30 / 40 quadros por segundo) |
| PC parado há X minutos (padrão 5) | **repouso**: pixel art a 12 quadros/s, texturas liberadas da memória |
| Tela bloqueada, jogo/vídeo em tela cheia, aquário escondido | **pausado**: animação parada, praticamente 0% de CPU |
| Notebook na bateria | Ultra vira Normal e no máximo 20 quadros/s |

Uso medido: cerca de 25 MB de RAM.

## Compilar

Precisa do .NET SDK 8 (só para compilar). Quem usa o programa não precisa instalar nada: ele roda no .NET Framework 4.8, que já vem no Windows 10 e 11.

```powershell
.\build.ps1              # build\MyAquario\ com os pacotes ao lado (testar sem internet)
.\build.ps1 -SemPacotes  # sem os pacotes: o programa baixa do GitHub Releases
```

O build gera:
- `build\MyAquario\MyAquario.exe` (~130 KB)
- `build\MyAquario-Setup.exe`: instalador (Inno Setup 6, `installer\MyAquario.iss`)
- `build\packs\pack-normal.zip` e `pack-ultra.zip`

Os três vão no GitHub Releases. O programa baixa os pacotes de
`https://github.com/SandiehMoreira/MyAquario/releases/latest/download/pack-<id>.zip`,
então **toda versão nova precisa levar os dois zips junto**.

Arte realista para o Ultra (imagens dos peixes): veja [ARTE.md](ARTE.md).

Para conferir os gráficos sem abrir a janela:

```powershell
build\MyAquario\MyAquario.exe --snapshot <pasta-saida> build\packs
```

## Estrutura

```
src/MyAquario/
  Game/       regras do pet (fome, água, humor, pontos), aquários, peixes e comportamento
  Render/     PixelRenderer (Básico), CartoonRenderer (Normal), UltraRenderer (Ultra), Overlay
  Platform/   janela transparente, Win32, pacotes, detecção do PC, iniciar com o Windows
  UI/         AquarioApp (bandeja, energia, avisos), TankWindow, SettingsForm
tools/PackBuilder/   gera as texturas dos pacotes Normal/Ultra e o ícone
```

Dados do jogador ficam em `%LOCALAPPDATA%\MyAquario` (`save.json`, `settings.json`, `log.txt`).
