<div align="center">

<img src="../icon.png" width="96" alt="Icône d'OpenWinBox" />

# OpenWinBox

**Des widgets open source pour le bureau Windows : vos dossiers, votre musique, l'heure, la météo et le son, toujours à portée de vue.**

[![Télécharger](https://img.shields.io/badge/T%C3%A9l%C3%A9charger-Programme%20d%27installation%20Windows-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![Version](https://img.shields.io/badge/version-1.0.0--beta.2-orange)
![Plateforme](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Licence](https://img.shields.io/badge/licence-MIT-green)

[English](../../README.md) · [Español](README.es.md) · **Français** · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

<br>

<img src="../banner.svg" width="100%" alt="Illustration des widgets OpenWinBox sur un bureau : un widget de dossiers avec onglets, une horloge, la météo, la musique et des commandes audio" />

<sub>Illustration des types de widgets. L'apparence réelle dépend de votre thème et de vos réglages.</sub>

</div>

> [!NOTE]
> OpenWinBox est en **bêta**. Certaines choses peuvent changer ou ne pas fonctionner d'une version à l'autre. Signalez les problèmes dans l'onglet [Issues](https://github.com/yosefhdev/Open-WinBox/issues).

## Qu'est-ce qu'OpenWinBox ?

Le bureau Windows se remplit vite de fichiers en vrac, et voir l'heure à l'étranger ou changer de morceau oblige à ouvrir une autre application. OpenWinBox place de petits widgets sur le bureau pour garder tout cela sous les yeux.

Il fonctionne depuis la zone de notification, ne demande aucun compte, et retirer un widget ne supprime jamais vos fichiers.

## Widgets

### Widgets de dossiers

Transformez n'importe quel dossier en widget affichant ses fichiers et raccourcis.

- **Glisser-déposer :** faites glisser des fichiers ou des raccourcis sur un widget pour les ajouter, et double-cliquez pour les ouvrir.
- **Onglets comme dans un navigateur :** regroupez plusieurs widgets en un seul, réorganisez les onglets en les faisant glisser ou sortez-en un pour le séparer.
- **À votre goût :** renommez les widgets, choisissez un emoji ou une image comme icône, verrouillez leur position et rangez les icônes dans l'ordre voulu.

### Widgets fonctionnels

Activez ou désactivez chacun séparément, et cliquez sur sa ligne dans les réglages pour voir ses options.

| Widget | Ce qu'il fait |
|---|---|
| 🎵 **Musique** | Affiche ce qui joue dans n'importe quelle application, avec les commandes de lecture et trois styles |
| 🟢 **Spotify** | La même idée pour Spotify uniquement, avec lecture aléatoire et répétition |
| 🕒 **Glance** | Grande horloge sur la photo du jour de Bing, avec second fuseau horaire en option et format 12 ou 24 heures |
| ☀️ **Météo** | La météo actuelle de votre position ou de n'importe quelle ville |
| 🔊 **Audio** | Changez la sortie et le micro, et réglez le volume de chaque application, comme avec Win+G |

## Pourquoi vous pourriez l'apprécier

- **Reste sur le bureau.** Les widgets restent derrière vos applications, restent visibles avec Win+D et n'apparaissent jamais dans la barre des tâches ni dans Alt+Tab.
- **Se place tout seul.** Les widgets s'alignent en douceur entre eux et sur les bords de l'écran.
- **Thèmes clair et sombre.** Suivez le thème du système ou choisissez-en un.
- **Discret au repos.** Il utilise environ 0,05 seconde de CPU par minute.
- **Importe depuis DeskBox.** Si DeskBox 1.5 est installé, vos widgets, leur ordre et leurs icônes sont importés au premier lancement.

## Installation

**Il vous faut** Windows 10 version 1809 ou ultérieure, ou Windows 11 (64 bits). Le programme d'installation inclut tout le reste : inutile d'installer .NET.

1. Téléchargez le dernier `OpenWinBox-<version>-Setup.exe` depuis la page [Releases](https://github.com/yosefhdev/Open-WinBox/releases).
2. Exécutez-le. Il s'installe uniquement pour votre utilisateur et ne demande pas de droits d'administrateur.
3. Ouvrez OpenWinBox. Un guide rapide s'ouvre la première fois.

> [!IMPORTANT]
> Le programme d'installation n'est pas encore signé : Windows peut afficher *Windows a protégé votre ordinateur*. Choisissez **Informations complémentaires** puis **Exécuter quand même**. Pour vérifier le téléchargement, comparez son empreinte avec `SHA256SUMS.txt` dans la version publiée. Si le contrôle intelligent des applications est activé, Windows peut bloquer l'application sans possibilité de continuer.

## Utilisation

> [!TIP]
> L'interface est pour l'instant en **espagnol**. Les libellés affichés à l'écran sont indiqués en *italique*.

Les deux commandes à retenir sont **F7**, qui affiche ou masque tous les widgets, et l'**icône de la zone de notification**, qui ouvre le menu d'un clic droit.

<details open>
<summary><b>Actions courantes</b></summary>
<br>

| Action | Comment |
|---|---|
| Afficher ou masquer tous les widgets | Appuyez sur **F7** ou faites un clic gauche sur l'icône de la zone de notification |
| Ouvrir les réglages | Clic droit sur l'icône de la zone de notification → *Configuración…*, ou relancez l'application |
| Nouveau widget | **+** sur un widget → *Nuevo widget*, ou *Configuración → Widgets de archivos → Nuevo widget* |
| Nouvel onglet dans un widget | **+** → *Nueva pestaña en este widget* |
| Ajouter des fichiers | Faites glisser des fichiers ou des raccourcis sur le widget (maintenez **Ctrl** pour copier un raccourci au lieu de le déplacer) |

</details>

<details>
<summary><b>Organiser les widgets</b></summary>
<br>

| Action | Comment |
|---|---|
| Renommer, changer l'icône, verrouiller, regrouper | **⋯** sur le widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Regrouper deux widgets | Faites glisser un widget par sa barre et maintenez-le sur la barre d'un autre jusqu'à ce que *Suelta para unir* apparaisse, puis relâchez |
| Réorganiser ou séparer des onglets | Faites glisser un onglet latéralement pour le déplacer, ou hors du widget pour le rendre indépendant |
| Déplacer un widget fonctionnel | Survolez-le et faites glisser la poignée en haut |

</details>

<details>
<summary><b>Réglages</b></summary>
<br>

| Action | Comment |
|---|---|
| Activer ou désactiver les widgets fonctionnels | *Configuración → Widgets funcionales* |
| Changer le thème | *Configuración → Apariencia → Tema* |
| Démarrer avec Windows | *Configuración → General → Lanzar al inicio*, ou *Iniciar con Windows* dans le menu de la zone de notification |

</details>

## Mises à jour

Ouvrez *Configuración → Acerca de* et cliquez sur *Buscar actualizaciones*. S'il existe une nouvelle version, *Actualizar* la télécharge, vérifie son empreinte et l'installe en conservant vos réglages. L'application se ferme un instant puis se rouvre.

Dans *Canal de actualizaciones*, choisissez *Oficiales* pour les versions stables uniquement, ou *Betas* pour recevoir aussi les versions de test. OpenWinBox ne se connecte à GitHub pour cela que lorsque vous cliquez sur le bouton.

## Confidentialité

OpenWinBox n'a ni compte ni analyse d'utilisation propre. Vos réglages restent sur votre PC, dans `%LOCALAPPDATA%\OpenWinBox`.

Deux widgets se connectent directement depuis votre PC à des services en ligne, sans clé d'API :

| Widget | Service | Utilisation |
|---|---|---|
| Météo | [Open-Meteo](https://open-meteo.com/) | Prévisions et recherche de villes |
| Météo | [BigDataCloud](https://www.bigdatacloud.com/) | Nom de la ville de votre position |
| Météo | [ipapi.co](https://ipapi.co/) | Position approximative lorsque la localisation Windows est désactivée |
| Glance | Microsoft Bing | Photo du jour |

Les widgets Musique et Spotify lisent ce que Windows signale comme étant en lecture : aucune connexion n'est demandée. Les conditions de chaque service se trouvent dans [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).

## Désinstallation

1. Choisissez *Salir* dans le menu de la zone de notification.
2. Désinstallez OpenWinBox depuis **Paramètres Windows → Applications → Applications installées**. Cela supprime aussi son démarrage avec Windows.
3. Facultatif : supprimez `%LOCALAPPDATA%\OpenWinBox` pour effacer aussi vos réglages.

## Compiler depuis les sources

Il vous faut le [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

L'application est créée dans `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

Pour générer le programme d'installation, installez [Inno Setup 6](https://jrsoftware.org/isinfo.php) et exécutez `tools/publish.ps1`. Le résultat se trouve dans `dist/`.

Réalisé avec C#, [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) et le [Windows App SDK](https://github.com/microsoft/WindowsAppSDK).

## Versions

OpenWinBox suit le [versionnage sémantique](https://semver.org/lang/fr/). Les bêtas sont numérotées `1.0.0-beta.N` jusqu'à la version finale `1.0.0`. Voir le [CHANGELOG.md](../../CHANGELOG.md).

## Contribuer

Les rapports de bugs, idées et pull requests sont les bienvenus. Pour un changement important, ouvrez d'abord une issue. Pour commencer avec le code, consultez le [guide du développeur](../development.md) (en anglais).

## Licence

[MIT](../../LICENSE) © 2026 Yosefh. Les composants et services tiers sont listés dans [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Données météo par [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox est un projet indépendant, sans lien avec Microsoft, Spotify ou DeskBox.
