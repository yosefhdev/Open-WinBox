<div align="center">

<img src="../icon.png" width="96" alt="Icône d'OpenWinBox" />

# OpenWinBox

**Des widgets open source pour le bureau Windows : vos dossiers, votre musique, l'heure, la météo et le son, toujours à portée de vue.**

![Version](https://img.shields.io/badge/version-1.0.0--beta.1-orange)
![Plateforme](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Licence](https://img.shields.io/badge/licence-MIT-green)

[English](../../README.md) · [Español](README.es.md) · **Français** · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

</div>

> [!NOTE]
> OpenWinBox est en **bêta**. Certaines choses peuvent changer ou ne pas fonctionner d'une version à l'autre. Signalez les problèmes dans l'onglet Issues.

## Fonctionnalités

- **Widgets de dossiers.** N'importe quel dossier devient un widget avec ses fichiers et raccourcis. Glissez-y des fichiers ou des raccourcis pour les ajouter ; double-cliquez pour ouvrir.
- **Onglets comme dans un navigateur.** Regroupez plusieurs widgets en un seul : glissez un widget sur un autre, réorganisez les onglets en les faisant glisser ou sortez un onglet pour le séparer.
- **À votre goût.** Renommez les widgets, choisissez un emoji ou une image comme icône, verrouillez leur position et rangez les icônes dans l'ordre voulu.
- **Toujours sur le bureau.** Les widgets restent derrière vos applications, n'apparaissent ni dans la barre des tâches ni dans Alt+Tab et s'alignent en douceur entre eux et sur les bords de l'écran.
- **Widgets fonctionnels :**
  - **Musique** : ce qui joue dans n'importe quelle application, avec commandes et trois styles.
  - **Spotify** : uniquement Spotify, avec lecture aléatoire et répétition.
  - **Glance** : grande horloge sur la photo du jour de Bing, second fuseau horaire en option, 12 h ou 24 h.
  - **Météo** : la météo de votre position ou de n'importe quelle ville.
  - **Audio** : changez la sortie et le micro, et réglez le volume de chaque application (comme Win+G).
- **Léger.** Environ 0,005 % de CPU au repos.
- **Importation depuis DeskBox.** Si DeskBox 1.5 est installé, vos widgets, leur ordre et leurs icônes sont importés au premier lancement.

## Configuration requise

- Windows 10 version 1809 ou ultérieure, ou Windows 11 (64 bits).
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64). Pour compiler depuis les sources, il faut le SDK .NET 8.

## Installation

### Option 1 : télécharger une version

1. Ouvrez la page **Releases** de ce dépôt et téléchargez le dernier `OpenWinBox-<version>-win-x64.zip`.
2. Décompressez-le dans un dossier que vous garderez, par exemple `C:\Apps\OpenWinBox`.
3. Lancez `OpenWinBox.exe`. Un guide rapide s'ouvre la première fois.

> Les paquets seront publiés avec la première bêta publique.

### Option 2 : compiler depuis les sources

```bash
git clone <url-du-depot>
cd <dossier-du-depot>
dotnet build DeskBox/OpenWinBox.csproj -c Release
```

L'application se trouve dans `DeskBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

## Utilisation

> L'interface est pour l'instant en **espagnol** ; les libellés exacts sont indiqués en *italique*.

| Action | Comment |
|---|---|
| Afficher ou masquer tous les widgets | Appuyez sur **F7** ou cliquez sur l'icône de la zone de notification |
| Ouvrir les paramètres | Clic droit sur l'icône de la zone de notification → *Configuración…*, ou relancez l'application |
| Nouveau widget | **+** sur un widget → *Nuevo widget*, ou *Configuración → Widgets de archivos → Nuevo widget* |
| Nouvel onglet dans un widget | **+** → *Nueva pestaña en este widget* |
| Renommer, changer l'icône, verrouiller, regrouper | **⋯** sur le widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Ajouter des fichiers | Glissez des fichiers ou des raccourcis sur le widget (maintenez **Ctrl** pour copier un raccourci au lieu de le déplacer) |
| Regrouper deux widgets | Faites glisser un widget par sa barre et maintenez-le sur la barre d'un autre jusqu'à voir *Suelta para unir*, puis relâchez |
| Réorganiser ou séparer des onglets | Glissez un onglet sur le côté pour le déplacer, ou hors du widget pour le rendre indépendant |
| Déplacer un widget fonctionnel | Survolez-le et faites glisser la poignée en haut |
| Activer ou désactiver les widgets fonctionnels | *Configuración → Widgets funcionales* (cliquez sur une ligne pour ses options) |
| Démarrer avec Windows | *Configuración → General → Lanzar al inicio*, ou *Iniciar con Windows* dans le menu de la zone de notification |

Les paramètres sont enregistrés dans `%LOCALAPPDATA%\OpenWinBox`. Supprimer un widget ou un onglet ne supprime jamais vos fichiers.

## Désinstallation

1. Désactivez *Lanzar al inicio* dans les paramètres, puis choisissez *Salir* dans le menu de la zone de notification.
2. Supprimez le dossier de l'application.
3. Facultatif : supprimez `%LOCALAPPDATA%\OpenWinBox` pour effacer vos paramètres.

## Versions

OpenWinBox suit le [versionnage sémantique](https://semver.org/lang/fr/). Les bêtas sont numérotées `1.0.0-beta.N` jusqu'à la version finale `1.0.0`. Voir le [CHANGELOG.md](../../CHANGELOG.md).

## Contribuer

Les rapports de bugs, idées et pull requests sont les bienvenus. Pour un changement important, ouvrez d'abord une issue.

## Licence

[MIT](../../LICENSE) © 2026 Yosefh. Les composants et services tiers sont listés dans [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Données météo par [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox est un projet indépendant, sans lien avec Microsoft, Spotify ou DeskBox.
