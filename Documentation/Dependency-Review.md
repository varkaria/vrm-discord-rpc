# Dependency and lifecycle review — 2026-10-06

## Why a helper

[Unity domain reload](https://docs.unity3d.com/2022.3/Documentation/Manual/DomainReloading.html) resets static managed state. [SessionState](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/SessionState.html) preserves data across assembly reload, but cannot preserve a pipe worker or delegate. [Discord SET_ACTIVITY](https://github.com/discord/discord-api-docs/blob/main/developers/topics/rpc.mdx) includes the application's PID. A helper outside the editor's scripting domain preserves both the owner PID and transport; atomic file messages let reloads happen without sockets, delegates, or shutdown callbacks crossing the domain boundary.

Disabling Unity's domain reload would only mask one trigger and does not cover recompilation. Reconnecting on every reload with a saved timestamp fixes the clock but still churns the connection. A native global client could survive reload, but would require platform-specific builds and careful lifetime handling for managed callbacks. The bundled Mono helper keeps the implementation managed and independently testable.

## RPC library

The old Discord Game SDK is legacy. Discord's [migration guidance](https://support-dev.discord.com/hc/en-us/articles/30125671534359-Migrating-from-the-Legacy-Game-SDK-to-the-Discord-Social-SDK) recommends Social SDK for its broader social features. This editor extension only needs desktop Rich Presence; maintained managed RPC avoids importing a game/social runtime into every avatar project.

[Lachee v1.6.1](https://github.com/Lachee/discord-rpc-csharp/releases/tag/v1.6.1) is the current stable release checked in this audit; v1.6.2 is prerelease. NuGet 1.6.1.70 supplies the matching assembly. Release notes include pipe discovery, Flatpak support, and image fixes. The net45 assembly runs on Unity 2022.3's bundled Mono without the extra registry dependency of its netstandard2 build. The wrapper waits for the SDK's asynchronous shutdown worker rather than assuming Dispose closes it synchronously.

Unity Newtonsoft.Json stays at the current compatible **3.2.2**, and the standalone build references stable NuGet **13.0.4** (assembly identity 13.0.0.0). The helper loads the Unity-resolved assembly at runtime; Unity lifecycle tests verify this combination. Build reference assemblies use Microsoft.NETFramework.ReferenceAssemblies **1.0.3**. .NET SDK **10.0.401** builds net472 executables; users do not need that SDK.

Project test/IDE dependencies: Unity Test Framework **1.4.6**, NUnit **2.0.5**, Rider **3.0.40**, Visual Studio **2.0.28**, TMP **3.0.9**, Timeline **1.8.12**. The deprecated VS Code package is removed. This package has no VRChat SDK dependency.

## Listing template and automation

Audited [template-package-listing](https://github.com/vrchat-community/template-package-listing/tree/0c6013555c9566a1658320e5495576f48a4424a0d) and [package-list-action](https://github.com/vrchat-community/package-list-action/tree/cb31c3b5d17d1070d7741c61de2ca1b219224039). The examined action fetches the current listing with a GitHub bearer header, skips URLs already in it, and builds a new package set. This project instead explicitly merges preserved history, verifies immutable hashes, and limits authentication to GitHub API requests. Tests cover unchanged rebuilds and source failures.

GitHub Actions were checked against their stable release tags and pinned to commit SHAs: checkout **7.0.1**, setup-python **7.0.0**, setup-dotnet **6.0.0**, configure-pages **6.0.0**, upload-pages-artifact **5.0.0**, deploy-pages **5.0.1**. No unpinned template checkout or third-party tag/JSON/release action remains.

## Validation limits

Local Unity/Mono verification uses **2022.3.22f1 on Windows**. Recording transport integration tests exercise real helper processes, locks, timestamps, and parent death without modifying a Discord profile. Unity tests also exercise actual Play Mode and script reload. They do not prove how the current Discord UI clears cached activity. macOS support and a real Discord profile smoke test remain unverified. The listing's historical archives are downloaded and checked against the preserved hashes before deployment. Vixen Plus entries are prepared separately and excluded from the published listing.
