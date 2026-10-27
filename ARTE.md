# Arte do gráfico Ultra

O Ultra já tem ambiente realista (luz no fundo, raios de sol, sombras, texturas). Os peixes ainda são desenhados
por código. Para ficarem com cara de foto, basta **uma imagem PNG por peixe**: o programa faz a cauda e o corpo
ondularem sozinhos ao nadar.

## Onde colocar

Salve as imagens em `art\ultra\` com estes nomes e rode o build. Elas entram no `pack-ultra.zip` automaticamente.

| Arquivo | O que é |
|---|---|
| `fish_betta_0.png` | Betta **vermelho** |
| `fish_betta_1.png` | Betta **azul** |
| `fish_betta_2.png` | Betta **roxo** |
| `fish_betta_3.png` | Betta **turquesa** |
| `fish_betta.png` | (opcional) um betta só, usado para qualquer cor que faltar |
| `fish_corydora.png` | Coridora |
| `fish_rasbora.png` | Rasbora-arlequim |
| `fish_neon.png` | Neon |
| `backdrop_hd.png` | (opcional) fundo: pedras e plantas ao longe, desfocado. Substitui o gerado por código |
| `gravel_hd.png` | (opcional) cascalho visto de lado, emendável na horizontal |

Se faltar alguma imagem, aquele peixe continua desenhado por código. Dá para ir trocando um por um.

## Regras para as imagens dos peixes

1. Peixe **de lado**, corpo inteiro, **cabeça para a DIREITA**.
2. **Fundo transparente** (PNG), sem sombra, sem água, sem texto.
3. **Recortado rente**: pouca margem em volta do peixe.
4. Nadadeiras abertas e relaxadas (o programa faz o movimento).
5. Pelo menos **1024 px de largura** (o build reduz para 512).
6. Luz vindo de cima, sem reflexo forte de flash.

Se a IA não fizer fundo transparente, tire o fundo depois: o **Paint do Windows 11** tem "Remover fundo"
(menu Imagem), ou use o site remove.bg.

## Prompts prontos

Em inglês costuma dar resultado melhor. Troque a cor/espécie em cada um.

**Betta:**
```
Photorealistic side view of a red halfmoon betta fish, full body, fins and tail fully spread,
facing right, isolated on a transparent background, soft top lighting, high detail scales,
no shadow, no water, no text, centered, tightly cropped
```
Cores: `red`, `royal blue`, `purple`, `turquoise`.

**Coridora:**
```
Photorealistic side view of a bronze corydoras catfish, full body, facing right, isolated on a
transparent background, soft top lighting, high detail, no shadow, no water, no text, tightly cropped
```

**Rasbora-arlequim:**
```
Photorealistic side view of a harlequin rasbora fish (orange body with black triangular patch),
full body, facing right, isolated on a transparent background, soft lighting, no shadow, no text, tightly cropped
```

**Neon:**
```
Photorealistic side view of a neon tetra fish (iridescent blue stripe, red tail half), full body,
facing right, isolated on a transparent background, soft lighting, no shadow, no text, tightly cropped
```

**Fundo (opcional):**
```
Underwater aquarium background, distant rocks and aquatic plants, soft blue-green light,
shallow depth of field, blurred, no fish, wide 2:1 aspect ratio
```

## Qual ferramenta usar

| Ferramenta | Observação |
|---|---|
| **ChatGPT** (geração de imagem) | Fácil, aceita pedir "fundo transparente" direto |
| **Adobe Firefly** | Feito para uso comercial (bom se for vender o jogo) |
| **Leonardo.ai** | Tem plano grátis e opção de fundo transparente |
| **Midjourney** | Qualidade muito alta; fundo precisa ser removido depois |
| **Stable Diffusion** (no seu PC) | Grátis e ilimitado; sua placa de vídeo aguenta |

Se o jogo for vendido ou tiver anúncios, confira nos termos da ferramenta se o uso comercial é permitido.
