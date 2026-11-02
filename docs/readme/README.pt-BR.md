<div align="center">

<img src="../icon.png" width="96" alt="Ícone do OpenWinBox" />

# OpenWinBox

**Widgets de código aberto para a área de trabalho do Windows: suas pastas, música, relógio, clima e áudio sempre à vista.**

![Versão](https://img.shields.io/badge/versão-1.0.0--beta.2-orange)
![Plataforma](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Licença](https://img.shields.io/badge/licença-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · **Português** · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

</div>

> [!NOTE]
> O OpenWinBox está em **beta**. Algumas coisas podem mudar ou falhar entre versões. Relate problemas na aba Issues.

## Recursos

- **Widgets de pastas.** Qualquer pasta vira um widget com seus arquivos e atalhos. Arraste arquivos ou atalhos para adicioná-los; clique duas vezes para abrir.
- **Abas como no navegador.** Junte vários widgets em um: arraste um widget sobre outro, reordene as abas arrastando ou puxe uma aba para fora para separá-la.
- **Do seu jeito.** Renomeie widgets, escolha um emoji ou uma imagem como ícone, trave a posição e organize os ícones na ordem que quiser.
- **Sempre na área de trabalho.** Os widgets ficam atrás dos seus apps, não aparecem na barra de tarefas nem no Alt+Tab e se alinham suavemente entre si e às bordas da tela.
- **Widgets funcionais:**
  - **Música**: o que está tocando em qualquer app, com controles e três estilos.
  - **Spotify**: só o Spotify, com aleatório e repetir.
  - **Glance**: relógio grande sobre a foto do dia do Bing, segundo fuso horário opcional, 12 h ou 24 h.
  - **Clima**: o tempo da sua localização ou de qualquer cidade.
  - **Áudio**: troque a saída e o microfone e ajuste o volume de cada app (como o Win+G).
- **Leve.** Cerca de 0,005 % de CPU em repouso.
- **Importa do DeskBox.** Se o DeskBox 1.5 estiver instalado, seus widgets, ordem e ícones são importados na primeira execução.

## Requisitos

- Windows 10 versão 1809 ou posterior, ou Windows 11 (64 bits).
- O instalador inclui todo o resto. Para compilar a partir do código, use o [SDK do .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

## Instalação

### Opção 1: baixar uma versão

1. Abra a página **Releases** deste repositório e baixe o `OpenWinBox-<versão>-Setup.exe` mais recente.
2. Execute-o. Ele instala apenas para o seu usuário e não pede permissões de administrador.
3. Abra o OpenWinBox. Na primeira vez, um guia rápido é aberto.

> O instalador ainda não é assinado, então o Windows pode mostrar *O Windows protegeu o computador*. Escolha **Mais informações** e depois **Executar assim mesmo**. Você pode comparar o hash com o `SHA256SUMS.txt` da versão publicada.

### Opção 2: compilar a partir do código

```bash
git clone <url-do-repositorio>
cd <pasta-do-repositorio>
dotnet build DeskBox/OpenWinBox.csproj -c Release
```

O app fica em `DeskBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

## Como usar

> Por enquanto a interface está em **espanhol**; os textos exatos aparecem em *itálico*.

| Ação | Como |
|---|---|
| Mostrar ou ocultar todos os widgets | Pressione **F7** ou clique no ícone da bandeja |
| Abrir as configurações | Clique com o botão direito no ícone da bandeja → *Configuración…*, ou abra o app de novo |
| Novo widget | **+** em qualquer widget → *Nuevo widget*, ou *Configuración → Widgets de archivos → Nuevo widget* |
| Nova aba em um widget | **+** → *Nueva pestaña en este widget* |
| Renomear, trocar ícone, travar, agrupar | **⋯** no widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Adicionar arquivos | Arraste arquivos ou atalhos para o widget (segure **Ctrl** para copiar um atalho em vez de movê-lo) |
| Agrupar dois widgets | Arraste um widget pela barra e segure sobre a barra de outro até aparecer *Suelta para unir*; depois solte |
| Reordenar ou separar abas | Arraste uma aba para os lados para reordenar, ou para fora do widget para torná-la independente |
| Mover um widget funcional | Passe o mouse sobre ele e arraste a alça no topo |
| Ativar ou desativar widgets funcionais | *Configuración → Widgets funcionales* (clique em uma linha para ver as opções) |
| Iniciar com o Windows | *Configuración → General → Lanzar al inicio*, ou *Iniciar con Windows* no menu da bandeja |

As configurações ficam em `%LOCALAPPDATA%\OpenWinBox`. Remover um widget ou uma aba nunca apaga seus arquivos.

## Desinstalar

1. Desative *Lanzar al inicio* nas configurações e escolha *Salir* no menu da bandeja.
2. Apague a pasta do app.
3. Opcional: apague `%LOCALAPPDATA%\OpenWinBox` para remover suas configurações.

## Versões

O OpenWinBox usa [versionamento semântico](https://semver.org/lang/pt-BR/). As betas são numeradas `1.0.0-beta.N` até a versão final `1.0.0`. Veja o [CHANGELOG.md](../../CHANGELOG.md).

## Contribuir

Relatos de bugs, ideias e pull requests são bem-vindos. Para mudanças grandes, abra uma issue antes.

## Licença

[MIT](../../LICENSE) © 2026 Yosefh. Componentes e serviços de terceiros estão em [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Dados do clima por [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

O OpenWinBox é um projeto independente, sem vínculo com Microsoft, Spotify ou DeskBox.
