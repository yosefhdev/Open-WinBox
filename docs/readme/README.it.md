<div align="center">

<img src="../icon.png" width="96" alt="Icona di OpenWinBox" />

# OpenWinBox

**Widget open source per il desktop di Windows: cartelle, musica, orologio, meteo e audio sempre a portata di vista.**

![Versione](https://img.shields.io/badge/versione-1.0.0--beta.1-orange)
![Piattaforma](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Licenza](https://img.shields.io/badge/licenza-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · **Italiano** · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

</div>

> [!NOTE]
> OpenWinBox è in **beta**. Alcune cose possono cambiare o non funzionare tra una versione e l'altra. Segnala i problemi nella scheda Issues.

## Funzionalità

- **Widget di cartelle.** Qualsiasi cartella diventa un widget con i suoi file e collegamenti. Trascina file o collegamenti sopra per aggiungerli; doppio clic per aprire.
- **Schede come nel browser.** Unisci più widget in uno: trascina un widget su un altro, riordina le schede trascinandole o tira fuori una scheda per separarla.
- **Come piace a te.** Rinomina i widget, scegli un'emoji o un'immagine come icona, blocca la posizione e disponi le icone nell'ordine che vuoi.
- **Sempre sul desktop.** I widget restano dietro le tue app, non compaiono nella barra delle applicazioni né in Alt+Tab e si allineano dolcemente tra loro e ai bordi dello schermo.
- **Widget funzionali:**
  - **Musica**: ciò che suona in qualsiasi app, con controlli e tre stili.
  - **Spotify**: solo Spotify, con riproduzione casuale e ripetizione.
  - **Glance**: grande orologio sulla foto del giorno di Bing, secondo fuso orario opzionale, 12 o 24 ore.
  - **Meteo**: il tempo nella tua posizione o in qualsiasi città.
  - **Audio**: cambia uscita e microfono e regola il volume di ogni app (come Win+G).
- **Leggero.** Circa lo 0,005 % di CPU a riposo.
- **Importa da DeskBox.** Se DeskBox 1.5 è installato, al primo avvio vengono importati i tuoi widget, il loro ordine e le icone.

## Requisiti

- Windows 10 versione 1809 o successiva, oppure Windows 11 (64 bit).
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64). Per compilare dal codice serve l'SDK di .NET 8.

## Installazione

### Opzione 1: scaricare una versione

1. Apri la pagina **Releases** di questo repository e scarica l'ultimo `OpenWinBox-<versione>-win-x64.zip`.
2. Estrailo in una cartella che conserverai, per esempio `C:\Apps\OpenWinBox`.
3. Avvia `OpenWinBox.exe`. La prima volta si apre una guida rapida.

> I pacchetti verranno pubblicati con la prima beta pubblica.

### Opzione 2: compilare dal codice

```bash
git clone <url-del-repository>
cd <cartella-del-repository>
dotnet build DeskBox/OpenWinBox.csproj -c Release
```

L'app si trova in `DeskBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

## Come si usa

> Per ora l'interfaccia è in **spagnolo**; le etichette esatte sono in *corsivo*.

| Azione | Come |
|---|---|
| Mostrare o nascondere tutti i widget | Premi **F7** o fai clic sull'icona nell'area di notifica |
| Aprire le impostazioni | Clic destro sull'icona nell'area di notifica → *Configuración…*, oppure riapri l'app |
| Nuovo widget | **+** su un widget → *Nuevo widget*, oppure *Configuración → Widgets de archivos → Nuevo widget* |
| Nuova scheda in un widget | **+** → *Nueva pestaña en este widget* |
| Rinominare, cambiare icona, bloccare, raggruppare | **⋯** sul widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Aggiungere file | Trascina file o collegamenti sul widget (tieni premuto **Ctrl** per copiare un collegamento invece di spostarlo) |
| Raggruppare due widget | Trascina un widget dalla sua barra e tienilo sulla barra di un altro finché compare *Suelta para unir*, poi rilascia |
| Riordinare o separare le schede | Trascina una scheda di lato per riordinarla, o fuori dal widget per renderla indipendente |
| Spostare un widget funzionale | Passaci sopra col mouse e trascina la maniglia in alto |
| Attivare o disattivare i widget funzionali | *Configuración → Widgets funcionales* (clic su una riga per le opzioni) |
| Avviare con Windows | *Configuración → General → Lanzar al inicio*, oppure *Iniciar con Windows* nel menu dell'area di notifica |

Le impostazioni sono salvate in `%LOCALAPPDATA%\OpenWinBox`. Rimuovere un widget o una scheda non cancella mai i tuoi file.

## Disinstallazione

1. Disattiva *Lanzar al inicio* nelle impostazioni e scegli *Salir* nel menu dell'area di notifica.
2. Elimina la cartella dell'app.
3. Facoltativo: elimina `%LOCALAPPDATA%\OpenWinBox` per rimuovere le impostazioni.

## Versioni

OpenWinBox usa il [versionamento semantico](https://semver.org/lang/it/). Le beta sono numerate `1.0.0-beta.N` fino alla versione finale `1.0.0`. Vedi il [CHANGELOG.md](../../CHANGELOG.md).

## Contribuire

Segnalazioni di bug, idee e pull request sono benvenute. Per modifiche importanti apri prima una issue.

## Licenza

[MIT](../../LICENSE) © 2026 Yosefh. Componenti e servizi di terze parti sono elencati in [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Dati meteo di [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox è un progetto indipendente, non affiliato a Microsoft, Spotify o DeskBox.
