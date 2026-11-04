<div align="center">

<img src="../icon.png" width="96" alt="Ícone do OpenWinBox" />

# OpenWinBox

**Widgets de código aberto para a área de trabalho do Windows: suas pastas, música, relógio, clima e áudio sempre à vista.**

[![Baixar](https://img.shields.io/badge/Baixar-Instalador%20para%20Windows-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![Versão](https://img.shields.io/badge/versão-1.0.0--beta.3-orange)
![Plataforma](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Licença](https://img.shields.io/badge/licença-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · **Português** · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

<br>

<img src="../banner.svg" width="100%" alt="Ilustração dos widgets do OpenWinBox em uma área de trabalho: um widget de pastas com abas, um relógio, o clima, música e controles de áudio" />

<sub>Ilustração dos tipos de widget. A aparência real depende do seu tema e das suas configurações.</sub>

</div>

> [!NOTE]
> O OpenWinBox está em **beta**. Algumas coisas podem mudar ou falhar entre versões. Relate problemas na aba [Issues](https://github.com/yosefhdev/Open-WinBox/issues).

## O que é o OpenWinBox?

A área de trabalho do Windows costuma ficar cheia de arquivos soltos, e ver a hora de outro país ou trocar de música exige abrir outro aplicativo. O OpenWinBox coloca pequenos widgets na área de trabalho para deixar tudo isso sempre à vista.

Ele funciona na bandeja do sistema, não precisa de conta, e remover um widget nunca apaga seus arquivos.

## Widgets

### Widgets de pastas

Transforme qualquer pasta em um widget que mostra seus arquivos e atalhos.

- **Arrastar e soltar:** arraste arquivos ou atalhos para um widget para adicioná-los e clique duas vezes para abri-los.
- **Abas como no navegador:** agrupe vários widgets em um só, reordene as abas arrastando ou puxe uma para fora para separá-la.
- **Do seu jeito:** renomeie widgets, use um emoji ou uma imagem como ícone, trave a posição e organize os ícones na ordem que quiser.

### Widgets funcionais

Ative ou desative cada um separadamente e clique na linha dele nas configurações para ver as opções.

| Widget | O que faz |
|---|---|
| 🎵 **Música** | Mostra o que está tocando em qualquer aplicativo, com controles de reprodução e três estilos |
| 🟢 **Spotify** | A mesma ideia só para o Spotify, com aleatório e repetição |
| 🕒 **Glance** | Relógio grande sobre a foto do dia do Bing, com segundo fuso horário opcional e formato de 12 ou 24 horas |
| ☀️ **Clima** | O clima atual da sua localização ou de qualquer cidade |
| 🔊 **Áudio** | Troque a saída e o microfone e ajuste o volume de cada aplicativo, como no Win+G |

## Por que você pode gostar

- **Fica na área de trabalho.** Os widgets ficam atrás dos seus aplicativos, continuam visíveis com Win+D e nunca aparecem na barra de tarefas nem no Alt+Tab.
- **Encaixa no lugar.** Os widgets se alinham suavemente entre si e com as bordas da tela.
- **Temas claro e escuro.** Siga o tema do sistema ou escolha um.
- **Discreto em repouso.** Usa cerca de 0,05 segundo de CPU por minuto.
- **Importa do DeskBox.** Se o DeskBox 1.5 estiver instalado, seus widgets, a ordem e os ícones são importados na primeira execução.

## Instalação

**Você precisa de** Windows 10 versão 1809 ou posterior, ou Windows 11 (64 bits). O instalador inclui todo o resto, então não é preciso instalar o .NET.

1. Baixe o `OpenWinBox-<versão>-Setup.exe` mais recente na página [Releases](https://github.com/yosefhdev/Open-WinBox/releases).
2. Execute-o. Ele instala apenas para o seu usuário e não pede permissões de administrador.
3. Abra o OpenWinBox. Na primeira vez, um guia rápido é aberto.

> [!IMPORTANT]
> O instalador ainda não é assinado, então o Windows pode mostrar *O Windows protegeu o computador*. Escolha **Mais informações** e depois **Executar assim mesmo**. Para conferir o download, compare o hash com o `SHA256SUMS.txt` da versão publicada. Se o Controle Inteligente de Aplicativos estiver ativado, o Windows pode bloquear o aplicativo sem opção de continuar.

## Como usar

> [!TIP]
> A interface está disponível em **inglês** e **espanhol**. Se o Windows estiver em outro idioma, ela aparece em inglês; você pode mudar em *Settings → General → Language*. Os textos que você verá na tela aparecem em *itálico*, em inglês.

Os dois controles para lembrar são **F7**, que mostra ou oculta todos os widgets, e o **ícone da bandeja**, que abre o menu com o botão direito.

<details open>
<summary><b>Ações do dia a dia</b></summary>
<br>

| Ação | Como |
|---|---|
| Mostrar ou ocultar todos os widgets | Pressione **F7** ou clique com o botão esquerdo no ícone da bandeja |
| Abrir as configurações | Botão direito no ícone da bandeja → *Settings*, ou abra o aplicativo de novo |
| Novo widget | **+** em qualquer widget → *New widget*, ou *Settings → File widgets → New widget* |
| Nova aba em um widget | **+** → *New tab in this widget* |
| Adicionar arquivos | Arraste arquivos ou atalhos para o widget (segure **Ctrl** para copiar um atalho em vez de movê-lo) |

</details>

<details>
<summary><b>Organizar widgets</b></summary>
<br>

| Ação | Como |
|---|---|
| Renomear, trocar o ícone, travar, agrupar | **⋯** no widget → *Rename*, *Change icon…*, *Lock position*, *Group with…* |
| Agrupar dois widgets | Arraste um widget pela barra e segure sobre a barra de outro até aparecer *Release to merge*; depois solte |
| Reordenar ou separar abas | Arraste uma aba para os lados para reordená-la, ou para fora do widget para torná-la independente |
| Mover um widget funcional | Passe o cursor sobre ele e arraste a alça na parte superior |

</details>

<details>
<summary><b>Configurações</b></summary>
<br>

| Ação | Como |
|---|---|
| Ativar ou desativar widgets funcionais | *Settings → Functional widgets* |
| Mudar o tema | *Settings → Appearance → Theme* |
| Iniciar com o Windows | *Settings → General → Launch at startup*, ou *Start with Windows* no menu da bandeja |

</details>

## Atualizações

Abra *Settings → About* e clique em *Check for updates*. Se houver uma versão nova, *Update* baixa, confere o hash e instala, mantendo suas configurações. O aplicativo fecha por um instante e abre de novo.

Em *Update channel*, escolha *Official* para receber só versões estáveis, ou *Betas* para receber também versões de teste. O OpenWinBox só se conecta ao GitHub para isso quando você clica no botão.

## Privacidade

O OpenWinBox não tem conta nem análises próprias. Suas configurações ficam no seu PC, em `%LOCALAPPDATA%\OpenWinBox`.

Dois widgets se conectam diretamente do seu PC a serviços on-line, sem chave de API:

| Widget | Serviço | Para quê |
|---|---|---|
| Clima | [Open-Meteo](https://open-meteo.com/) | Previsão e busca de cidades |
| Clima | [BigDataCloud](https://www.bigdatacloud.com/) | Nome da cidade da sua localização |
| Clima | [ipapi.co](https://ipapi.co/) | Localização aproximada quando a localização do Windows está desativada |
| Glance | Microsoft Bing | Foto do dia |

Os widgets Música e Spotify leem o que o Windows informa estar tocando, então não pedem login. Os termos de cada serviço estão em [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).

## Desinstalar

1. Escolha *Exit* no menu da bandeja.
2. Desinstale o OpenWinBox em **Configurações do Windows → Aplicativos → Aplicativos instalados**. Isso também remove a inicialização com o Windows.
3. Opcional: apague `%LOCALAPPDATA%\OpenWinBox` para remover também as suas configurações.

## Compilar a partir do código

Você precisa do [SDK do .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

O aplicativo é criado em `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

Para gerar o instalador, instale o [Inno Setup 6](https://jrsoftware.org/isinfo.php) e execute `tools/publish.ps1`. O resultado fica em `dist/`.

Feito com C#, [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) e o [Windows App SDK](https://github.com/microsoft/WindowsAppSDK).

## Versões

O OpenWinBox usa [versionamento semântico](https://semver.org/lang/pt-BR/). As betas são numeradas `1.0.0-beta.N` até a versão final `1.0.0`. Veja o [CHANGELOG.md](../../CHANGELOG.md).

## Contribuir

Relatos de bugs, ideias e pull requests são bem-vindos. Para mudanças grandes, abra uma issue antes. Para começar com o código, consulte o [guia para desenvolvedores](../development.md) (em inglês).

## Licença

[MIT](../../LICENSE) © 2026 Yosefh. Componentes e serviços de terceiros estão em [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Dados do clima por [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

O OpenWinBox é um projeto independente, sem vínculo com Microsoft, Spotify ou DeskBox.
