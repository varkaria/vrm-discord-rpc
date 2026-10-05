Unity recompilation and Play Mode now keep one persistent Discord connection and the original elapsed-time timestamp. An external helper owns presence for each Unity editor process, preventing reloads from creating competing connections.

Includes project/scene privacy preferences, automatic reconnects, crash cleanup, and updated dependencies. Requires Unity 2022.3; validated with 2022.3.22f1.

**Upgrade:** restart Unity once after updating from 0.0.x. Remove any old copy imported under Assets before installing through VCC. Settings are in Edit → Preferences → VRM Discord RPC.

This release ships a VPM ZIP. Existing repository subscriptions continue to work at https://vpm.varkaria.works/index.json. Vixen Plus remains prepared but unpublished.
