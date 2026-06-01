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

## Functional differences from upstream

The list below is the **net** difference between `main-prod` and `main-base` (the last upstream
snapshot we merged from), captured 2026-06-01 with `main-prod` at `227442b0a`. It is intended as a
checklist for deciding which fork changes are still required once we move to the latest upstream
Filament. Regenerate with:

```sh
git diff --stat origin/main-base..main-prod
```

### 1. Renderer-level frame-completed callback

Adds an asynchronous "frame done" callback. New `Renderer::setFrameCallback(callback, destroyCallback,
user)` (C++) and `Renderer.setFrameCallback(handler, runnable)` (Java); the backend `endFrame()`
signature gains a callback + user pointer, and the GL backend runs a dedicated fence-wait thread that
fires the callback once the frame's fence signals. Replaces an earlier post-frame callback that
created excessive JNI global references.

**Why / client use:** `FXRRenderSystem.kt` (avn.graphics.fframe) and `OXRRenderSystem.kt`
(avn.platform.oxr) call `renderer.setFrameCallback(frameCallbackExecutor) { … }` to run work after
each frame completes (OpenXR frame submit / eye-buffer handoff).<br>
**Scope:** GL backend only — Metal/Vulkan/Noop accept the new `endFrame` args and ignore them.<br>
**Files:** `filament/include/filament/Renderer.h`, `filament/src/Renderer.cpp`,
`filament/src/details/Renderer.{h,cpp}`, `filament/backend/include/private/backend/DriverAPI.inc`,
`filament/backend/src/Driver.cpp`, `filament/backend/src/{opengl/OpenGLDriver.{h,cpp},
metal/MetalDriver.mm, noop/NoopDriver.cpp, vulkan/VulkanDriver.cpp}`, `android/common/CallbackUtils.{h,cpp}`,
`android/filament-android/.../cpp/Renderer.cpp`, `.../java/.../Renderer.java`, plus 10 backend
`test_*.cpp` files (mechanical `endFrame(0)` → `endFrame(0, nullptr, nullptr)` updates).<br>
**Upstream check:** upstream has its own frame-completed mechanism (`setFrameCompletedCallback` on the
SwapChain) — verify whether that can replace this before dropping the fork code.

### 2. RenderTarget MSAA `samples()`

Adds `RenderTarget::Builder::samples(n)` (C++/Java/JNI) so an offscreen `RenderTarget` can request
MSAA on mobile.

**Why / client use:** `FSwapChain.kt` builds each eye target with `.samples(headset.mssaSamples)`.<br>
**Files:** `filament/include/filament/RenderTarget.h`, `filament/src/details/RenderTarget.cpp`,
`android/filament-android/.../cpp/RenderTarget.cpp`, `.../java/.../RenderTarget.java`.<br>
**Upstream check:** confirm whether current upstream `RenderTarget` already exposes a sample count.

### 3. Simplified exponential fog (three.js `FogExp2`)

Rewrites `shaders/src/fog.fs` to `exp2(-density² · dist²)`, mixing the fog colour by distance only,
and pre-squares density in `PerViewUniforms.cpp`. **Drops** Filament's height fog, height falloff, sun
in-scattering, IBL-derived fog colour and max-opacity.

**Why / client use:** `FogComponent.kt` and the render systems set `View.FogOptions`. With this fork
only `density` and `color` affect the result — `heightFalloff` (and the other `FogOptions` fields) are
inert despite still being set by the client.<br>
**Files:** `shaders/src/fog.fs`, `filament/src/PerViewUniforms.cpp`.<br>
**Upstream check:** dropping the fork restores full upstream fog, which **changes fog appearance** and
re-activates `heightFalloff`/in-scattering/etc. Decide whether the simplified look is still wanted.

### 4. Mali-T `glTexSubImage` source over-read workaround (CVR1)

Adds an OpenGL bug flag `texture_upload_source_overrun` (set for Mali-T) and, in
`OpenGLDriver::setTextureData`, probes the source buffer's trailing page with `mincore()`; only when
that page is unmapped does it upload from an over-allocated staging copy. Works around a Mali-T
(rk3288, r11p0) driver that over-reads the upload source and intermittently SIGSEGVs.

**Why / client use:** protects texture uploads on legacy Mali-T ClassVR headsets.<br>
**Files:** `filament/backend/src/opengl/OpenGLContext.{h,cpp}`, `filament/backend/src/opengl/OpenGLDriver.cpp`.<br>
**Upstream check:** still required as long as legacy Mali-T devices are supported; not an upstream concern.

### 5. Build — keep native debug symbols (Android)

`android/build.gradle` keeps `**/*.so` debug symbols for non-release variants so tombstones can be
symbolicated with `addr2line`; release variants stay stripped.

**Files:** `android/build.gradle`.<br>
**Upstream check:** local build-tooling preference; carry forward regardless of upstream version.

### 6. Build — libpng/libz fix for modern macOS SDK

Drops `|| defined(TARGET_OS_MAC)` from the classic-Mac branch in `third_party/libpng/pngpriv.h` and
`third_party/libz/zutil.h` (it pulled in the long-gone classic Mac `<fp.h>` / `fdopen` macro) so the
host tools compile on current macOS SDKs.

**Files:** `third_party/libpng/pngpriv.h`, `third_party/libz/zutil.h`.<br>
**Upstream check:** re-apply if upstream still vendors these third-party copies; may be fixed upstream.

### Already absorbed by upstream / reverted (no longer fork differences)

These were present earlier on the branch but net out to zero against `main-base` — useful to know so
they are not re-introduced during the upstream move:

- **`Texture.Builder.importTexture`** (was `importShared`) — now native upstream; client `FSwapChain.kt`
  uses it.
- **ASTC texture enum additions** (`Texture.java`) — now upstream.
- **Morph-target normal contribution** (`shaders/src/main.vs`) — temporary disable reverted after
  upstream added support.
- **Temporary bound-count limit** (`EngineEnums.h`), an **uninitialized-pointer** tweak
  (`details/Renderer.h`), and profiling **"hacks"** (`Camera.java`, `TransformManager.java`) —
  overwritten/reverted by later upstream merges.
- **Color-grading disable** — disabled then undone within the branch.
- **filament-utils-android runtime-load-failure packaging option** — dropped in a later merge; only a
  trailing-newline change remains in `android/filament-utils-android/build.gradle`.
