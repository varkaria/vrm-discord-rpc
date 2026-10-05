# Varkaria Packages

Public downloads and the VPM listing for Varkaria's Unity tools. Development is maintained in a private monorepo; this repository contains generated website output and public release documentation. Historical source commits and release downloads remain available.

## Install

In VRChat Creator Companion, open **Settings → Packages → Add Repository** and paste:

```text
https://vpm.varkaria.works/index.json
```

Existing subscriptions continue to work. Package IDs and the repository ID are unchanged. See [all releases](https://github.com/varkaria/vrm-discord-rpc/releases) and the [package website](https://vpm.varkaria.works).

## VRMDiscordRPC

Shows Unity editor activity in Discord. The published releases remain available through VCC.

The prepared **0.1.0** update requires Unity 2022.3 and keeps one persistent helper across script recompilation and Play Mode. It preserves the elapsed-time timestamp, prevents duplicate helper connections, and adds project/scene privacy controls under **Edit → Preferences → VRM Discord RPC**. Batch runners never automatically broadcast activity. This update is available only once a 0.1.0 release appears in the listing.

When upgrading from 0.0.x, restart Unity once to unload the old native SDK. Remove any old copy imported manually under Assets before installing with VCC.

[Changelog](CHANGELOG.md) · [0.1.0 release notes](docs/RPC-RELEASE-NOTES.md) · [Report an issue](https://github.com/varkaria/vrm-discord-rpc/issues)

## Distribution maintenance

`site/` is generated output. The Pages workflow validates the listing and deploys that directory after a reviewed update reaches `main`. It does not fetch private source or build Unity packages. Release ZIPs are uploaded from the monorepo's allowlisted export. Do not restore the old source/build workflows here.

Vixen Plus is not published by this migration. Historical versions are retained; no existing release assets are deleted or replaced.
