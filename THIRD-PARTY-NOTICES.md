# Third-party notices

OpenWinBox is licensed under the [MIT License](LICENSE). It uses the following third-party
components and online services, each under its own terms.

## Libraries

| Component | License | Notes |
|---|---|---|
| [.NET 8](https://github.com/dotnet/runtime) | MIT | Runtime (installed separately by the user). |
| [Windows App SDK / WinUI 3](https://github.com/microsoft/WindowsAppSDK) | Microsoft Software License Terms (Windows App SDK) | Redistributable runtime is bundled with release builds, as allowed by section 3 "Distributable Code". The full terms ship in the NuGet package (`license.txt`). |
| [NAudio.Wasapi / NAudio.Core 2.2.1](https://github.com/naudio/NAudio) | MIT, © Mark Heath | Audio devices and per-app volume. |

NAudio license text: MIT, identical to [LICENSE](LICENSE) with "Copyright (c) Mark Heath".

## Online services

The app calls these services directly from the user's PC. No account or API key is needed.

| Service | Used for | Terms |
|---|---|---|
| [GitHub](https://github.com/) | Looking for and downloading new versions, only when the user presses the button | [GitHub Terms of Service](https://docs.github.com/site-policy/github-terms/github-terms-of-service). |
| [Open-Meteo](https://open-meteo.com/) | Weather forecast and city search | Data under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); attribution is shown in **Settings → About**. The free API is for **non-commercial use only** ([terms](https://open-meteo.com/en/terms)). |
| [BigDataCloud](https://www.bigdatacloud.com/) | City name for the device location (client-side reverse geocoding) | [Free client-side API](https://www.bigdatacloud.com/free-api/free-reverse-geocode-to-city-api) and fair-use policy; called only with the device's own current location. |
| [ipapi.co](https://ipapi.co/) | Approximate location when Windows location is off | [ipapi.co terms](https://ipapi.co/terms/). |
| Microsoft Bing (image of the day) | Background photos of the Glance widget | Photos are copyrighted by their owners and are only displayed, never redistributed. |

## Trademarks and affiliation

OpenWinBox is an independent project. It is not affiliated with or endorsed by Microsoft, Spotify, or the
authors of DeskBox. It can import the layout of an existing DeskBox installation, but it contains none of
DeskBox's code or assets. All trademarks belong to their respective owners.

If you plan to use OpenWinBox **commercially** (for example, a paid or ad-supported fork), you need your own
commercial weather data provider: the free Open-Meteo API does not allow it.
