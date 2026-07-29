#!/usr/bin/env python3
"""Verify the declared NuGet dependencies against the marketingcloudsdk .pom chain.

The binding's PackageReference set is hand-maintained; the .poms are what upstream actually
links against. On every native bump the two can drift - a dependency upstream added, or a
version it raised - and the failure a consumer sees is a runtime NoClassDefFoundError, not a
build error. This repository opts out of the SDK's Java dependency verification (the BOM
imports in the .poms cannot be resolved from Salesforce's repository - see Sfmc.Binding.props),
which makes this script the only automated check of the dependency mapping.

The umbrella .pom plus every module .pom this package ships are verified, so a dependency that
only a module declares (pushfeaturemodule's firebase-messaging, say) is still accounted for.
"""

import re
import sys
import urllib.request
from pathlib import Path
from xml.etree import ElementTree

REPO = "https://salesforce-marketingcloud.github.io/MarketingCloudSDK-Android/repository"
ROOT = Path(__file__).resolve().parent.parent

# Maven coordinate -> (NuGet id, MSBuild property in Directory.Build.props holding the pin).
COORDINATE_TO_NUGET = {
    "org.jetbrains.kotlin:kotlin-stdlib": ("Xamarin.Kotlin.StdLib", "KotlinStdLibVersion"),
    "com.google.firebase:firebase-messaging": ("Xamarin.Firebase.Messaging", "FirebaseMessagingVersion"),
    "com.google.android.gms:play-services-tasks": ("Xamarin.GooglePlayServices.Tasks", "GooglePlayServicesTasksVersion"),
    "com.google.android.gms:play-services-base": ("Xamarin.GooglePlayServices.Base", "GooglePlayServicesBaseVersion"),
    "androidx.activity:activity": ("Xamarin.AndroidX.Activity", "AndroidXActivityVersion"),
    "androidx.collection:collection": ("Xamarin.AndroidX.Collection.Ktx", "AndroidXCollectionKtxVersion"),
    "androidx.collection:collection-ktx": ("Xamarin.AndroidX.Collection.Ktx", "AndroidXCollectionKtxVersion"),
    "androidx.fragment:fragment-ktx": ("Xamarin.AndroidX.Fragment.Ktx", "AndroidXFragmentKtxVersion"),
    "androidx.constraintlayout:constraintlayout": ("Xamarin.AndroidX.ConstraintLayout", "AndroidXConstraintLayoutVersion"),
    # Satisfied transitively through SFMCSDK.Net.Android, whose repository pins them; listed so
    # the module .poms that restate them do not report as unmapped.
    "androidx.core:core-ktx": (None, None),
    "androidx.lifecycle:lifecycle-process": (None, None),
    "androidx.annotation:annotation": (None, None),
    "com.google.android.gms:play-services-basement": (None, None),
    # Managed BOMs: version-less imports the binding satisfies with the concrete pins above.
    "com.google.firebase:firebase-bom": (None, None),
    "androidx.compose:compose-bom": (None, None),
    # Kotlin merged the jdk8 extensions into the main stdlib at 1.8; see the csproj comment.
    "org.jetbrains.kotlin:kotlin-stdlib-jdk8": (None, None),
}

# Salesforce-internal coordinates satisfied inside this package or its SFMC-family dependency.
IN_REPO = {
    "com.salesforce.marketingcloud:sfmcsdk": "SfmcCorePackageVersion",
    "com.salesforce.marketingcloud:common-internal": None,
    "com.salesforce.marketingcloud:pushfeaturemodule": "SfmcPushFeatureVersion",
    "com.salesforce.marketingcloud:pushmodelsmodule": "SfmcPushModelsVersion",
    "com.salesforce.marketingcloud:inappmessagingfeaturemodule": "SfmcInAppMessagingFeatureVersion",
    "com.salesforce.marketingcloud:inappmessagingmodelsmodule": "SfmcInAppMessagingModelsVersion",
    "com.salesforce.marketingcloud:common": "SfmcCommonVersion",
    "com.salesforce.marketingcloud:legacy-crypto": "SfmcLegacyCryptoVersion",
}

GMS_PREFIX = "com.google.android.gms:"
GOOGLE_PREFIXED = ("com.google.android.gms:", "com.google.firebase:")


def prop(name: str) -> str:
    text = (ROOT / "Directory.Build.props").read_text()
    match = re.search(rf"<{name}>([^<]+)</{name}>", text)
    if not match:
        sys.exit(f"error: {name} not found in Directory.Build.props")
    return match.group(1).strip()


def parse_version(value: str) -> tuple[int, ...]:
    return tuple(int(part) for part in re.findall(r"\d+", value)[:4]) or (0,)


def nuget_wraps(coordinate: str, nuget_version: str, pom_version: str) -> bool:
    """A NuGet binding at X.Y.Z[.R] wraps native X.Y.Z; Google-family ids carry a leading 1."""
    native = parse_version(pom_version)
    nuget = parse_version(nuget_version)
    if coordinate.startswith(GOOGLE_PREFIXED) and nuget and nuget[0] >= 100:
        nuget = (nuget[0] - 100,) + nuget[1:]
    return nuget[: len(native)] >= native


def pom(artifact: str, version: str) -> ElementTree.Element:
    url = f"{REPO}/com/salesforce/marketingcloud/{artifact}/{version}/{artifact}-{version}.pom"
    with urllib.request.urlopen(url) as response:
        return ElementTree.fromstring(response.read())


def main() -> int:
    ns = {"m": "http://maven.apache.org/POM/4.0.0"}
    failures = []
    checked = 0

    poms = [
        ("marketingcloudsdk", prop("SfmcNativeVersion")),
        ("pushfeaturemodule", prop("SfmcPushFeatureVersion")),
        ("pushmodelsmodule", prop("SfmcPushModelsVersion")),
        ("inappmessagingfeaturemodule", prop("SfmcInAppMessagingFeatureVersion")),
        ("inappmessagingmodelsmodule", prop("SfmcInAppMessagingModelsVersion")),
        ("common", prop("SfmcCommonVersion")),
        ("legacy-crypto", prop("SfmcLegacyCryptoVersion")),
    ]

    for artifact, version in poms:
        for dependency in pom(artifact, version).findall(".//m:dependency", ns):
            group = dependency.findtext("m:groupId", "", ns)
            artifact_id = dependency.findtext("m:artifactId", "", ns)
            pom_version = dependency.findtext("m:version", "", ns)
            coordinate = f"{group}:{artifact_id}"

            if coordinate in IN_REPO:
                prop_name = IN_REPO[coordinate]
                if prop_name is None:
                    continue
                pinned = prop(prop_name)
                checked += 1
                if parse_version(pinned)[: len(parse_version(pom_version))] < parse_version(pom_version):
                    failures.append(
                        f"{artifact}: {coordinate} wants {pom_version}, {prop_name} pins {pinned}")
                continue

            if coordinate not in COORDINATE_TO_NUGET:
                failures.append(
                    f"{artifact}: {coordinate} {pom_version} has no NuGet mapping in "
                    "build/verify-pom-deps.py - upstream added a dependency; map it and reference "
                    "the binding package")
                continue

            nuget_id, prop_name = COORDINATE_TO_NUGET[coordinate]
            if nuget_id is None:
                continue

            pinned = prop(prop_name)
            checked += 1
            if pom_version and not nuget_wraps(coordinate, pinned, pom_version):
                failures.append(
                    f"{artifact}: {coordinate} wants {pom_version}, but {nuget_id} is pinned at "
                    f"{pinned} ({prop_name})")

    if failures:
        print("marketingcloudsdk .pom chain disagreements:", file=sys.stderr)
        for failure in failures:
            print(f"  - {failure}", file=sys.stderr)
        return 1

    print(f"verified {checked} declared dependencies against 7 .poms")
    return 0


if __name__ == "__main__":
    sys.exit(main())
