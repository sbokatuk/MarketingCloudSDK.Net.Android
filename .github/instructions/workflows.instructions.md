---
applyTo: ".github/workflows/*.yml"
---

# Workflows

- `build.yml` is the only place that builds. `pr.yml` and `release.yml` call it; neither duplicates
  a pack, test or emulator step. New verification belongs in `build.yml`.
- Honour the `verify` input: it gates the package tests, the sample build and the e2e matrix.
  Releases pack with `verify: false` because the PR that produced the commit already verified it.
  Never gate the checksum, R8-rule, `CheckReadmeVersions.sh` or `verify-pom-deps.py` steps on it —
  those run on every pack.
- Keep the release chain intact: `auto-release.yml` fires only on an **added**
  `docs/release-notes/<four-part-version>.md` on `main`, tags `v<version>` and dispatches
  `release.yml`; `release.yml`'s guard job refuses a tag that is not an ancestor of the default
  branch. Do not add a path that publishes without both.
- Publishing is nuget.org trusted publishing only: `environment: nuget.org`, `id-token: write`,
  `NuGet/login@v1`. Never add an API-key secret, and never widen `permissions` beyond the job that
  needs it.
- Beta versions are `<native>.<revision>-beta.<pr>.<run>`, and the publish job stays conditional on
  the PR head being in this repository — forks must not reach the nuget.org environment.
- `packages.tsv` and `upstream.tsv` are machine-read here. Read them; never hard-code a package id,
  artifact id or Maven coordinate into a workflow.
- Keep the shape uniform with the sibling `SFMCSDK.Net.Android` repository so a fix ports as one
  file: same job names, same inputs, same step order.
