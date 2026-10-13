# DrugExpansion

DrugExpansion is a real Schedule I mod and a public reference implementation for
S1API custom product kinds. Version 1 adds MDMA as the first content module.

The architecture is intentionally not MDMA-specific. Each module owns stable
product-kind and product IDs, persistence, presentation, packaging, production,
and optional mixing. A new custom drug kind does not need to be added to a central
enum and is not made non-mixable merely because Schedule I has no matching built-in
drug type.

## MDMA v1

- A custom logical MDMA product kind and fixed product definition.
- The attributed `Abstract Heart` GLB for the pressed tablet's loose, held,
  stored, station, functional, consumption, and generated inventory-icon
  presentation.
- Native baggie and jar shells populated with custom pill meshes; no extracted
  game packaging assets are redistributed.
- A Chemistry Station batch recipe that produces 10 MDMA crystals. Crystals
  are unfinished quality items rather than products: they cannot be packaged,
  consumed, sold to customers, or listed in Product Manager. Their custom
  four-variant GLB remains available for held, stored, station, icon, and press
  presentation.
- Disco Davey, a persistent native-style supplier connected to Uptown
  customers Herbert Bleuball and Tobias Wentworth. Davey is recommended after
  enough successful deals build either relationship, naturally placing him
  after Uptown opens at Baron I.
  All four precursors become purchasable once Davey is unlocked, without
  additional ingredient rank requirements.
  He sells low-, standard-, and high-grade Safrole plus Methylamine through the
  native supplier flow. Davey remains hidden while idle and appears only at a
  player-requested supplier meeting, matching base-game suppliers. His native
  Sneakers and CollarJacket retain their original mesh, rig, LODs, and texture
  detail while original transparent overlays add his rave palette at runtime;
  an original fitted GLB supplies the festival sling bag. Both original
  low-poly precursor container assets reuse the
  native Acid station scaffold, so dragging, constrained rotation, pouring,
  particles, and fill detection remain game behavior rather than custom
  approximations.
- The Chemistry Station recipe now requires one Safrole variant, one
  Methylamine, and one Acid. Producing crystals does not discover MDMA. The
  first successful tablet-press cycle discovers it without listing it for sale;
  listing remains an explicit Product Manager action.
- An original, floor-standing manual tablet-press GLB with a validated
  player-height pedestal, native Brick Press-style handle/ram anchors, and a
  reference press-cycle animation. It is sold at Handy Hank's Hardware and
  Dan's Hardware for $5,000 and requires Baron I. Each full wheel cycle
  converts one compatible crystal into one heart tablet.
  See
  [the integration contract](docs/manual-tablet-press-integration.md).
- Save-provider reconstruction, Product Manager metadata, discovery, and listing.
- Mono and IL2CPP build targets.

Mixing is deliberately isolated from product identity. `MdmaModule` implements
the `IMixingCapability` boundary and explicitly selects a native mixer-map
execution strategy while preserving the custom MDMA kind for generated outputs.
The module is not reclassified as marijuana, methamphetamine, cocaine, or shrooms
simply to become mixable.

## Build

For Schedule I **0.4.7f12**, use matching Mono/IL2CPP **S1API 3.2.1** DLLs and
S1MAPI dependencies. Launch the updated IL2CPP game with MelonLoader once before
building to regenerate its wrappers; older cached wrappers can still compile
while referencing members removed from the installed game.

1. Copy `local.build.props.example` to `local.build.props`.
2. Update the local game, S1API, and MAPI assembly paths.
3. Build both runtimes:

```powershell
dotnet build .\src\DrugExpansion\DrugExpansion.csproj -c Mono
dotnet build .\src\DrugExpansion\DrugExpansion.csproj -c Il2cpp
```

Install the matching DrugExpansion DLL in the game `Mods` directory. Install the
matching S1API and S1MAPI builds as dependencies. All multiplayer peers need the
same mod and assets.

For a quick manual test, the S1API console aliases are:

```text
give tabletpress
give mdmacrystals 10
give mdma
give safrole
give safrolelow
give safrolehigh
give methylamine
```

For the full production flow, add one Safrole variant, Methylamine, and Acid to
the Chemistry Station and make a 10-crystal batch. Place the press on a floor
grid, insert one or more compatible MDMA crystals,
begin the native-style task, and rotate the wheel clockwise through three full
turns. Each cycle consumes one crystal and produces one tablet, so processing
the chemistry station's 10-crystal batch takes 10 separate press cycles. The
tray animation is local presentation derived from the replicated quantity;
save/load and late join rebuild a deterministic settled tray rather than
serializing cosmetic rigidbodies.

Fresh saves keep MDMA undiscovered and unlisted until that first completed
press. Saves created by older builds may already contain ambiguous native
discovery or listing state; DrugExpansion preserves that state instead of
destructively guessing whether it was legitimate.

## Adding another drug

Implement `IDrugContentModule` in a new content folder and add it to the catalog
in `Core`. Keep the module's durable IDs stable after release. Mixing is an
independent optional capability; a native compatibility value is only a legacy
save/UI interoperability hint where the current game representation requires it.

## Testing policy

Pure unit tests may be committed. Runtime smoke mods, local game saves, extracted
game assets, generated wrappers, screenshots, and test launchers stay local and
must not be committed.

## Licenses

DrugExpansion source code is GPL-3.0-or-later. Bundled assets use the per-asset
licenses in [ASSET-LICENSE.md](ASSET-LICENSE.md); `heartpill.glb` is CC BY 4.0.
