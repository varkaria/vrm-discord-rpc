# VRMDiscordRPC · Varkaria Packages

Discord Rich Presence for the Unity Editor, distributed through [vpm.varkaria.works](https://vpm.varkaria.works). This repository also hosts the Varkaria VPM package listing.

## Install or update

In VRChat Creator Companion, open **Settings → Packages → Add Repository** and paste:

```text
https://vpm.varkaria.works/index.json
```

Enable the listing and add **VRMDiscordRPC** to your project. Existing subscribers can keep their repository: the listing ID and all published 0.0.x versions are preserved. Version 0.1.0 requires **Unity 2022.3**, tested with VRChat's 2022.3.22f1. New releases ship a VPM ZIP; legacy `.unitypackage` downloads remain available in historical releases.

After upgrading from 0.0.x, restart Unity once to unload its old native SDK and any leaked connections. If you previously imported an Assets copy manually, remove that old copy before installing the VPM package, so two implementations do not run together.

Presence starts automatically in an interactive editor. **Edit → Preferences → VRM Discord RPC** controls enablement, project/scene name sharing, and the application ID. Batch builds and test runners never automatically connect to Discord. The package is editor-only and is not included in uploaded avatars or game builds.

## Reload and Play Mode behavior

Unity reloads its scripting domain during compilation and most Play Mode transitions. Static C# objects cannot keep a connection across that boundary. Saving a timestamp alone preserves elapsed time but still reconnects the SDK.

Version 0.1.0 moves the connection into a small managed helper launched with Unity's bundled Mono runtime. It requires no separate runtime installation. Unity sends atomic local JSON snapshots under `Library/VRMDiscordRPC`; the helper owns the Discord pipe and runs no Unity APIs.

- One exclusive file lock per Unity process prevents competing helpers from opening duplicate connections.
- The same helper PID and connection survive recompilation, scene changes, Play Mode, and domain reload. The start timestamp lives in `SessionState`, with the helper retaining the first received value.
- Discord restart uses the SDK's reconnect loop. If the helper crashes, Unity starts a replacement with the original timestamp, after verifying the old PID and its creation time are gone.
- Disabling presence sends a stop request. Closing or crashing Unity is detected by its PID **and start time**, preventing PID reuse from keeping an orphan alive. Removing the package also stops the helper.
- The helper sends a null activity and waits for its transport worker on shutdown. A stuck worker terminates the helper before any replacement connection is allowed.
- Unchanged activity is not resent. Changes are coalesced at a 15-second minimum interval. Privacy changes may therefore take up to 15 seconds to reach Discord.

This removes reload-driven disconnects and competing connections. Discord controls its profile UI and activity history; it may briefly retain stale activity after a client or helper crash. Multiple Unity editor processes each have their own presence session. Windows is verified locally; Linux helper lifecycle tests run in CI. macOS and live Discord UI behavior still need verification.

## Build and test

The helper source is in `tools/Broker`; shared snapshot, protocol, and scheduling code is in the package. The shipped executable under `Editor/Broker~` is ignored by Unity's assembly importer. Build with .NET SDK **10.0.401**:

```sh
dotnet build tools/Broker/Broker.csproj -c Release -o Logs/Broker
# Copy Logs/Broker/VrmPresence.exe to Packages/dev.varkaria.discordrpc/Editor/Broker~/
dotnet build tools/Broker/Broker.csproj -c Release -p:BrokerTest=true -o Logs/BrokerTest
python -m unittest discover -s tools -p 'test_*.py' -v
```

Set `MONO` to Unity's `Editor/Data/MonoBleedingEdge/bin/mono.exe` on Windows, or install Mono for command-line tests. The recording helper is a separate test build; it cannot broadcast to a Discord account. CI compares the production executable against a fresh deterministic build.

Open this project in Unity 2022.3.22f1 and run the Edit Mode tests after building the recording helper. Tests cover the send scheduler, reconnect handling, Unicode limits, privacy, actual Play Mode transitions, script reload, unchanged helper identity, competing launches, parent crashes, and helper recovery. For batch execution, use absolute paths for `-testResults` and `-logFile`.

## Listing and releases

`source.json` follows the [official listing template](https://github.com/vrchat-community/template-package-listing): `githubRepos` discovers public stable release ZIPs, and `packages` adds explicit package URLs. The local Python builder retains history, validates package identity, hashes archive bytes, rejects changed versions, and never forwards GitHub API credentials to download hosts. It fails on source errors rather than deploying an incomplete listing. The website uses local assets and escaped DOM text, with no CDN or template runtime.

```sh
python tools/listing.py --offline  # Historical listing and website preview in .site
python tools/listing.py            # Fetch and verify all public releases
python tools/package.py Packages/dev.varkaria.discordrpc
python -m http.server 8080 --directory .site
```

Merge reviewed changes into `main`, then run **Build Release** manually. It verifies the helper, runs Python integration tests, creates a deterministic VPM ZIP and checksum, and publishes an immutable version tag. Bump `package.json` and update `RELEASE-NOTES.md` first; rerunning an existing release fails instead of overwriting its files. Run Unity tests before release; the GitHub jobs do not have a Unity license. A successful release rebuilds the Pages listing. Website/source changes also deploy on main; pull requests only run checks.

`listing/history.json` is the preserved public listing from 2026-10-06. Keep it when adding packages. Package/version removal requires an intentional history edit; removing a release alone does not erase the record.

### Vixen Plus: prepared only

`listing/vixen-plus.pending.json` contains the three proposed 1.5.0 package entries. It is **not consumed by the builder**. The repository remains private and no Vixen ZIP is published. After separately approving distribution and publishing the corresponding public release files, copy the three entries into `source.json`'s `packages` list. Original package IDs and GUIDs must be preserved for existing avatars. Users also need the VRChat, Hai, and nadena dependency listings.

## Dependencies and attribution

See [dependency review](Documentation/Dependency-Review.md), [third-party notices](Packages/dev.varkaria.discordrpc/Third-Party-Notices.md), and [changelog](CHANGELOG.md). Package ID `dev.varkaria.discordrpc`, original script GUID, assembly name, Discord application ID, and image keys remain compatible.
