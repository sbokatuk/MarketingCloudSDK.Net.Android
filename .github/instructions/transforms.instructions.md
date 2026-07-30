---
applyTo: "src/MarketingCloudSDK.Net.Android/Transforms/**"
---

# Binding transforms

- Rules are exceptions, not the norm — the generator handles the rest of the surface unaided. Add a
  rule only when a build actually fails without it.
- Every rule carries a comment naming **what breaks without it**, with the compiler or generator
  error code (CS0738, CS0102, CS0111, CS0533, CS0535, CS0542, JAVAC0000). A rule without that
  reasoning cannot be re-validated on the next upgrade.
- Prefer one semantic rule over many brittle ones:
  `//class[implements[@name='android.os.Parcelable.Creator']]` covers 55 classes that would
  otherwise need 55 XPaths.
- An XPath that stops matching is **silent** — no warning, no error. On every native bump, re-check
  each rule against the new `.aar`s and delete the ones whose upstream shape is gone.
- Removing a package removes it from the **projection** only; the classes still dex into the
  consuming app. Removals are for internals the SDK reaches from its own Kotlin
  (`com.salesforce.marketingcloud.push.*`, `pushmodels.data.*`), never for surface consumers use —
  notification customisation lives in `notifications.*` and stays fully bound.
- When adding a `starts-with()` package rule, add the exact-name rule alongside it: the
  trailing-dot form deliberately does not match the parent package.
- Renames that migrating 8.x consumers depend on (`GetRefreshCenter`, `GetRefreshRadius`,
  `CompareTo` on `Region`) are compatibility promises — do not rename them again.
- Any change here is a projection change: rebuild, run
  `dotnet test tests/MarketingCloudSDK.Net.Android.PackageTests`, and run the emulator legs
  including `r8`.
