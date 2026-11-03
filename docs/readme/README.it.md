<div align="center">

<img src="../icon.png" width="96" alt="Icona di OpenWinBox" />

# OpenWinBox

**Widget open source per il desktop di Windows: cartelle, musica, orologio, meteo e audio sempre a portata di vista.**

[![Scarica](https://img.shields.io/badge/Scarica-Programma%20di%20installazione%20per%20Windows-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![Versione](https://img.shields.io/badge/versione-1.0.0--beta.1-orange)
![Piattaforma](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Licenza](https://img.shields.io/badge/licenza-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · **Italiano** · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

<br>

<img src="../banner.svg" width="100%" alt="Illustrazione dei widget di OpenWinBox su un desktop: un widget di cartelle con schede, un orologio, il meteo, la musica e i controlli audio" />

<sub>Illustrazione dei tipi di widget. L'aspetto reale dipende dal tema e dalle impostazioni.</sub>

</div>

> [!NOTE]
> OpenWinBox è in **beta**. Alcune cose possono cambiare o non funzionare tra una versione e l'altra. Segnala i problemi nella scheda [Issues](https://github.com/yosefhdev/Open-WinBox/issues).

## Che cos'è OpenWinBox?

Il desktop di Windows tende a riempirsi di file sparsi, e per vedere l'ora di un altro paese o cambiare brano bisogna aprire un'altra app. OpenWinBox mette piccoli widget sul desktop per avere tutto sempre sott'occhio.

Funziona dall'area di notifica, non richiede un account e rimuovere un widget non cancella mai i tuoi file.

## Widget

### Widget di cartelle

Trasforma qualsiasi cartella in un widget che mostra i suoi file e collegamenti.

- **Trascina e rilascia:** trascina file o collegamenti su un widget per aggiungerli e fai doppio clic per aprirli.
- **Schede come nel browser:** raggruppa più widget in uno, riordina le schede trascinandole o tirane fuori una per separarla.
- **Il tuo stile:** rinomina i widget, usa un'emoji o un'immagine come icona, blocca la posizione e disponi le icone nell'ordine che vuoi.

### Widget funzionali

Attiva o disattiva ciascuno separatamente e fai clic sulla sua riga nelle impostazioni per vederne le opzioni.

| Widget | Cosa fa |
|---|---|
| 🎵 **Musica** | Mostra cosa è in riproduzione in qualsiasi app, con i controlli e tre stili |
| 🟢 **Spotify** | La stessa idea solo per Spotify, con riproduzione casuale e ripetizione |
| 🕒 **Glance** | Grande orologio sulla foto del giorno di Bing, con secondo fuso orario opzionale e formato a 12 o 24 ore |
| ☀️ **Meteo** | Il meteo attuale della tua posizione o di qualsiasi città |
| 🔊 **Audio** | Cambia uscita e microfono e regola il volume di ogni app, come con Win+G |

## Perché potrebbe piacerti

- **Resta sul desktop.** I widget stanno dietro le app, restano visibili con Win+D e non compaiono mai nella barra delle applicazioni né in Alt+Tab.
- **Si allinea da solo.** I widget si allineano dolcemente tra loro e ai bordi dello schermo.
- **Temi chiaro e scuro.** Segui il tema di sistema o scegline uno.
- **Tranquillo a riposo.** Usa circa 0,05 secondi di CPU al minuto.
- **Importa da DeskBox.** Se DeskBox 1.5 è installato, i tuoi widget, il loro ordine e le icone vengono importati al primo avvio.

## Installazione

**Ti serve** Windows 10 versione 1809 o successiva, oppure Windows 11 (64 bit). Il programma di installazione include tutto il resto, quindi non devi installare .NET.

1. Scarica l'ultimo `OpenWinBox-<versione>-Setup.exe` dalla pagina [Releases](https://github.com/yosefhdev/Open-WinBox/releases).
2. Eseguilo. Si installa solo per il tuo utente e non chiede i diritti di amministratore.
3. Apri OpenWinBox. La prima volta si apre una guida rapida.

> [!IMPORTANT]
> Il programma di installazione non è ancora firmato, quindi Windows può mostrare *PC protetto da Windows*. Scegli **Ulteriori informazioni** e poi **Esegui comunque**. Per verificare il download, confronta l'hash con `SHA256SUMS.txt` nella versione pubblicata. Se il controllo intelligente delle app è attivo, Windows può bloccare l'app senza possibilità di continuare.

## Come si usa

> [!TIP]
> L'interfaccia è per ora in **spagnolo**. Le etichette che vedrai sullo schermo sono riportate in *corsivo*.

I due comandi da ricordare sono **F7**, che mostra o nasconde tutti i widget, e l'**icona nell'area di notifica**, che apre il menu con il clic destro.

<details open>
<summary><b>Azioni quotidiane</b></summary>
<br>

| Azione | Come |
|---|---|
| Mostrare o nascondere tutti i widget | Premi **F7** o fai clic sinistro sull'icona nell'area di notifica |
| Aprire le impostazioni | Clic destro sull'icona nell'area di notifica → *Configuración…*, oppure riavvia l'app |
| Nuovo widget | **+** su un widget → *Nuevo widget*, oppure *Configuración → Widgets de archivos → Nuevo widget* |
| Nuova scheda in un widget | **+** → *Nueva pestaña en este widget* |
| Aggiungere file | Trascina file o collegamenti sul widget (tieni premuto **Ctrl** per copiare un collegamento invece di spostarlo) |

</details>

<details>
<summary><b>Organizzare i widget</b></summary>
<br>

| Azione | Come |
|---|---|
| Rinominare, cambiare icona, bloccare, raggruppare | **⋯** sul widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Raggruppare due widget | Trascina un widget per la barra e tienilo sulla barra di un altro finché non appare *Suelta para unir*, poi rilascia |
| Riordinare o separare le schede | Trascina una scheda di lato per riordinarla, o fuori dal widget per renderla indipendente |
| Spostare un widget funzionale | Passaci sopra con il cursore e trascina la maniglia in alto |

</details>

<details>
<summary><b>Impostazioni</b></summary>
<br>

| Azione | Come |
|---|---|
| Attivare o disattivare i widget funzionali | *Configuración → Widgets funcionales* |
| Cambiare il tema | *Configuración → Apariencia → Tema* |
| Avviare con Windows | *Configuración → General → Lanzar al inicio*, oppure *Iniciar con Windows* nel menu dell'area di notifica |

</details>

## Aggiornamenti

Apri *Configuración → Acerca de* e fai clic su *Buscar actualizaciones*. Se c'è una nuova versione, *Actualizar* la scarica, ne verifica l'hash e la installa, conservando le impostazioni. L'app si chiude per un momento e si riapre.

In *Canal de actualizaciones*, scegli *Oficiales* per ricevere solo le versioni stabili, o *Betas* per ricevere anche le versioni di prova. OpenWinBox si collega a GitHub per questo solo quando premi il pulsante.

## Privacy

OpenWinBox non ha account né analisi proprie. Le tue impostazioni restano sul tuo PC, in `%LOCALAPPDATA%\OpenWinBox`.

Due widget si collegano direttamente dal tuo PC a servizi online, senza chiave API:

| Widget | Servizio | A cosa serve |
|---|---|---|
| Meteo | [Open-Meteo](https://open-meteo.com/) | Previsioni e ricerca delle città |
| Meteo | [BigDataCloud](https://www.bigdatacloud.com/) | Nome della città della tua posizione |
| Meteo | [ipapi.co](https://ipapi.co/) | Posizione approssimativa quando la posizione di Windows è disattivata |
| Glance | Microsoft Bing | Foto del giorno |

I widget Musica e Spotify leggono ciò che Windows segnala in riproduzione, quindi non chiedono di accedere. I termini di ogni servizio sono in [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).

## Disinstallazione

1. Scegli *Salir* nel menu dell'area di notifica.
2. Disinstalla OpenWinBox da **Impostazioni di Windows → App → App installate**. Viene rimosso anche l'avvio con Windows.
3. Facoltativo: elimina `%LOCALAPPDATA%\OpenWinBox` per rimuovere anche le impostazioni.

## Compilare dal codice

Serve l'[SDK di .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

L'app viene creata in `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

Per generare il programma di installazione, installa [Inno Setup 6](https://jrsoftware.org/isinfo.php) ed esegui `tools/publish.ps1`. Il risultato si trova in `dist/`.

Realizzato con C#, [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) e il [Windows App SDK](https://github.com/microsoft/WindowsAppSDK).

## Versioni

OpenWinBox usa il [versionamento semantico](https://semver.org/lang/it/). Le beta sono numerate `1.0.0-beta.N` fino alla versione finale `1.0.0`. Vedi il [CHANGELOG.md](../../CHANGELOG.md).

## Contribuire

Segnalazioni di bug, idee e pull request sono benvenute. Per modifiche importanti apri prima una issue. Per iniziare con il codice, consulta la [guida per sviluppatori](../development.md) (in inglese).

## Licenza

[MIT](../../LICENSE) © 2026 Yosefh. Componenti e servizi di terze parti sono elencati in [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Dati meteo di [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox è un progetto indipendente, non affiliato a Microsoft, Spotify o DeskBox.
