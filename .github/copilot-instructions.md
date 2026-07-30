# MarketingCloudSDK.Net.Android — agent instructions

## What this repository is

- A .NET for Android binding for the Salesforce Marketing Cloud MobilePush umbrella module
  `com.salesforce.marketingcloud:marketingcloudsdk` (11.0.1, v11 Unified SDK generation) — push
  registration, inbox, in-app messages, geofence and beacon messaging.
- **One** NuGet package, `MarketingCloudSDK.Net.Android`, carrying **seven** `.aar`s: the umbrella
  plus `pushfeaturemodule`, `pushmodelsmodule`, `inappmessagingfeaturemodule`,
  `inappmessagingmodelsmodule`, `common`, `legacy-crypto`.
- The **models** modules are bound (`Bind="true"`): the umbrella's public listeners take their
  types, and an unbound parameter type makes the generator drop the method and breaks consumers'
  Java Callable Wrappers (JAVAC0000). The **feature** modules, `common` and `legacy-crypto` ride
  payload-only — generator failures over API nothing calls from C#; their classes still dex.
- The package depends on the sibling `SFMCSDK.Net.Android` (the `sfmcsdk` core), mirroring how the
  native umbrella depends on `sfmcsdk`.
- v11 initialisation is `SFMCSdk.Configure(context, SFMCSdkModuleConfig { EngagementModuleConfig =
  MarketingCloudConfig }, callback)`. `MarketingCloudSdk.Init` is the pre-Unified entry point and
  initialises nothing. Contact-key **writes** moved to unified identity (`SFMCSdk.Identity`, in the
  core package); the registration editor here carries tags and attributes.
- Target frameworks `net8.0-android34.0;net9.0-android35.0;net10.0-android36.0`, floor API 26.

## Build and verify

```sh
./build/BuildNugets.sh                 # needs .NET 9 + .NET 10 SDKs and their android workloads
dotnet test tests/MarketingCloudSDK.Net.Android.PackageTests
./.github/scripts/run-emulator-tests.sh 11.0.1.2 net9.0-android35.0        # emulator must be up
./.github/scripts/run-emulator-tests.sh 11.0.1.2 net10.0-android36.0 r8    # the shrunk leg
```

- Pass the emulator script the version `BuildNugets.sh` produced —
  `$(SfmcNativeVersion).$(SfmcBindingRevision)`.
- `BuildNugets.sh` packs twice — net9 band, then net10 from a scratch `global.json` — and
  `build/merge-packages.py` merges the two into `artifacts/`. The repo `global.json` pins SDK
  9.0.100 with `rollForward: latestFeature`.
- On **any** dependency or version change run `./build/CheckReadmeVersions.sh` and
  `python3 build/verify-pom-deps.py`; CI runs both and fails on either.
- After a Maven version change run `./build/UpdateMavenChecksums.sh` and review the diff; after a
  change to what ships or what R8 must keep, run `./build/generate-r8-rules.sh`. CI regenerates
  both and fails on drift.

## Layout

| Path | What |
| --- | --- |
| `src/Sfmc.Binding.props` | TFM bands, Maven resolution, checksum verification, packaging — a one-file port with the sibling `SFMCSDK.Net.Android`; fix in both |
| `src/MarketingCloudSDK.Net.Android/` | The binding: `Transforms/Metadata.xml`, `Additions/`, consumer R8 rules under `buildTransitive/` |
| `build/` | Pack, checksum, R8-rule, `.pom`-verification and upgrade scripts; `packages.tsv` and `upstream.tsv` are the rosters |
| `tests/` | `PackageTests` (in the solution, nupkg shape) and `DeviceTests` (outside it, consumes packed nupkgs from `artifacts/`) |
| `samples/` | A MAUI app, also consuming packed nupkgs |

## Conventions

- Versions are four-part: `<marketingcloudsdk version>.<binding revision>` from `SfmcNativeVersion`
  + `SfmcBindingRevision` in `Directory.Build.props`. Bump the revision when the bindings or
  packaging change and the native artifacts stay put.
- One MSBuild property per module version (`SfmcPushFeatureVersion`, `SfmcPushModelsVersion`, …) so
  a bump is one edit plus a checksum regeneration.
- Keep `Sfmc*` property names uniform with `SFMCSDK.Net.Android` (`SfmcNativeVersion`, never
  `McNativeVersion`) — that uniformity is what keeps `Sfmc.Binding.props` and the scripts portable.
- Every `Transforms/Metadata.xml` rule must state **what breaks without it**. XPaths go silently
  stale: re-validate each one on a native bump.
- Third-party pins are `.pom`-exact, with the net8 exceptions documented inline. Preserve the long
  comments; they are the reasoning, not decoration.
- The device tests and the sample carry the known consumer workaround
  `Xamarin.AndroidX.Compose.Runtime.Annotation.Jvm` with `ExcludeAssets="all"` (JAVA0000 duplicate
  type otherwise); keep it, the README troubleshooting entry and both csprojs in step.
- British spelling in README and docs.

## CI and release flow

- `build.yml` is reusable (`verify` input; pack, sample and e2e jobs on `ubuntu-latest`).
- Every PR builds and publishes `-beta.<pr>.<run>` to nuget.org; PRs from forks skip publish.
- **Merging `docs/release-notes/<four-part-version>.md` to `main` is the release**: `auto-release.yml`
  tags `v<version>` and dispatches `release.yml`, which guards (the tag must be an ancestor of the
  default branch), packs with `verify: false` and publishes through nuget.org trusted publishing
  (OIDC, environment `nuget.org`).
- `upstream-drift.yml` (daily) and `upstream-watch.yml` (weekly) read `build/upstream.tsv` and
  `build/packages.tsv`; keep both rosters in step with what the package ships.
- Release train: when `sfmcsdk` moved, bump the sibling core binding **first**, then this
  repository, then the umbrellas.

## Testing

- `PackageTests` always — it is the guard that a binding assembly is a real binding, not an empty
  shell. Add the emulator legs, `r8` included, when touching `Transforms/`, `Additions/`, the R8
  rules or any dependency.
- Device tests prove the JNI surface, that classes across all seven `.aar`s are present and that
  configuration reaches the SDK. They are credential-free by design and never exercise tenant
  traffic.

## Hard rules

- **Never** commit native artifacts. Resolution is direct download from Salesforce's Maven
  repository plus SHA-256 pins in `build/maven-checksums.txt`. A changed hash for an unchanged
  version is an incident — investigate, do not regenerate.
- **Never** ship `sfmcsdk` or `common-internal` from this package; they arrive through the
  `SFMCSDK.Net.Android` dependency, and duplicating them is XA4301 in every consumer.
- **Never** flip a module between `Bind="true"` and payload-only without the empirical
  justification: models modules are bound because public listeners need their types, feature
  modules stay payload-only. Change `Transforms/Metadata.xml` and its rationale comments together
  with any projection change.
- **Never** bracket-pin the `SFMCSDK.Net.Android` reference — Java dependency verification cannot
  parse ranges (XA4241). Keep it bare and let `verify-pom-deps.py` guard the native line.
- Keep that bare pin on a **stable** `SFMCSDK.Net.Android` version, never a `-beta.<pr>.<run>`
  build; a prerelease pin must never reach a release-note merge or a tagged release.
- **Never** hand-edit `buildTransitive/*.pro`; regenerate with `./build/generate-r8-rules.sh`.
- **Never** "modernise" the AndroidX, Firebase or Play services pins. They mirror the `.pom`s and
  the net8 asset floor, and must stay generation-consistent with `SFMCSDK.Net.Android`.
- Any native bump: run `./build/BumpNativeVersion.sh <version>`, review the checksum diff,
  re-validate every `Metadata.xml` XPath, update the README pins (`CheckReadmeVersions.sh` enforces
  them) and keep `build/packages.tsv` and `build/upstream.tsv` in step.
- Docs and samples initialise via `SFMCSdk.Configure` + `SFMCSdkModuleConfig`. Never present
  `MarketingCloudSdk.Init` as the entry point.
- Release only through the release-note merge chain, with trusted publishing only (no API keys),
  and never tag a commit that is not on the default branch.
- **Never** commit Marketing Cloud credentials (app id, access token, tenant URL, MID); samples take
  them as runtime input.

## References

- Salesforce MobilePush Android docs: <https://developer.salesforce.com/docs/marketing/mobilepush/guide/>
- Salesforce Maven repository (the group is **not** on Maven Central):
  <https://salesforce-marketingcloud.github.io/MarketingCloudSDK-Android/repository>
- Siblings: [`SFMCSDK.Net.Android`](https://github.com/sbokatuk/SFMCSDK.Net.Android) (core),
  [`MarketingCloudSDK.Net.iOS`](https://github.com/sbokatuk/MarketingCloudSDK.Net.iOS),
  [`MarketingCloudSDK.Net`](https://github.com/sbokatuk/MarketingCloudSDK.Net)
- `build/packages.tsv` — the roster, including what is deliberately not bound and why.

Trust these instructions, and search the codebase only when something here is incomplete or wrong.
