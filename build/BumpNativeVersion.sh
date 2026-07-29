#!/bin/sh
# Runs every scripted step of a marketingcloudsdk upgrade, in order, stopping at the first
# failure.
#
#   ./build/BumpNativeVersion.sh 11.1.0
#
# The module .aars (pushfeaturemodule, inappmessaging*, common, legacy-crypto) version
# independently: read the new umbrella .pom first and update their properties in
# Directory.Build.props BY HAND before running this - verify-pom-deps.py fails below when any
# disagrees with the .pom chain, which is the guard for forgetting one. The SFMCSDK.Net.Android
# pin (SfmcCorePackageVersion) usually moves in the same event; bump the sibling repository
# first.
#
# What it does NOT automate:
#   - reviewing the checksum diff (a hash that changed for an unchanged version is the event
#     the pins exist to catch);
#   - re-checking the Transforms rules against the new .aars - an XPath that stops matching is
#     silent (see src/MarketingCloudSDK.Net.Android/Transforms/Metadata.xml);
#   - the README's version pins (build/CheckReadmeVersions.sh will tell you);
#   - a release-notes file under docs/release-notes/;
#   - bumping SfmcBindingRevision back to 1 for the new native line.
set -eu

version="$1"

case "$version" in
    *[!0-9.]*|'')
        echo "usage: $0 <marketingcloudsdk-version>" >&2
        exit 1
        ;;
esac

root="$(cd "$(dirname "$0")/.." && pwd)"
props="$root/Directory.Build.props"

echo "==> pinning SfmcNativeVersion $version"
sed -i '' "s:<SfmcNativeVersion>.*</SfmcNativeVersion>:<SfmcNativeVersion>$version</SfmcNativeVersion>:" "$props"

echo "==> verifying the dependency mapping against the new .pom chain"
python3 "$root/build/verify-pom-deps.py"

echo "==> regenerating Maven checksums"
"$root/build/UpdateMavenChecksums.sh"

echo "==> regenerating consumer R8 rules"
"$root/build/generate-r8-rules.sh"

echo "==> packing"
"$root/build/BuildNugets.sh"

echo "==> package tests"
dotnet test "$root/tests/MarketingCloudSDK.Net.Android.PackageTests"

echo "==> done. Review the diffs (especially build/maven-checksums.txt), update the README and"
echo "    docs/release-notes/ - this script does not do that for you."
