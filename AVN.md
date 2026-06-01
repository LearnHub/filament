# AVN Filament Fork

This repository is the AVN (Avantis) fork of Google's [Filament](https://github.com/google/filament)
real-time rendering engine: <https://github.com/LearnHub/filament>. It carries minor changes on top
of upstream and produces the Android AARs consumed by the AVN app.

The build requires the Filament **desktop** tools as well, because the material compiler (`matc`) runs
on the host. Environments (HDR cube maps for skyboxes) are compiled independently and stored online.

> This document summarises the AVN-specific setup. For upstream build details see
> [BUILDING.md](BUILDING.md); for upstream branching see [BRANCHING.md](BRANCHING.md).

## Branches

**`main-prod`** — the active AVN production branch.<br>
**`main-base`** — the last upstream branch we merged from, kept for reference.<br>
**`production`** — old; contains a previous attempt at a post-frame callback. Do not use.

## Prerequisites

CMake and Ninja are required and are installed via Homebrew (they are not provided by
`xcode-select`):

```sh
brew install cmake
brew install ninja
```

Add the following to `~/.zshenv`:

```sh
export JAVA_HOME=/Applications/Android\ Studio.app/Contents/jre/Contents/Home/
export ANDROID_HOME=/Users/rupert/Library/Android/sdk
```

`python` is no longer installed by default on macOS, so add it to your `PATH`:

```sh
export PATH="/opt/homebrew/opt/python@3.9/libexec/bin:$PATH"
```

See the upstream [macOS requirements](https://github.com/google/filament/blob/main/BUILDING.md#macos)
and the [Android samples instructions](https://github.com/google/filament/tree/master/android/samples)
for anything else.

## Build

Build the desktop tools and the Android libraries:

```sh
git clean -fdx
./build.sh -p desktop -i release debug
./build.sh -p android -i release debug
```

## Install into the AVN app

Copy the built `filament` and `filamat` AAR files from `filament/out` into the AVN project by running
the install script that lives alongside the AVN app:

```sh
sh ~/GitHub/LearnHub/AVN/filament/install.sh
```

That script copies the `filament`, `filamat`, `filament-utils`, and `gltfio-android` artifacts from
`filament/out` into the corresponding AVN project folders.

## On update

- Check that `MaterialCache.Version` matches
  `filament/out/release/filament/include/filament/MaterialEnums.h`.
- The material compiler is a modified version of `FilamentPlugin.groovy`.

## Troubleshooting

- Occasional build hysteresis bugs can often be fixed by clearing the Gradle cache:

  ```sh
  rm -rf ~/.gradle
  ```

- To build the Filament Android samples, go to the `android` folder and run:

  ```sh
  ./gradlew assembleDebug
  ```
