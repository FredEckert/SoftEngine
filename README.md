# SoftEngine

A software wireframe renderer in C#, rebuilt from David Rousset's 2013 3D Soft Engine.
It uses one handedness throughout (right-handed `System.Numerics`), clips against the near
plane, and stores orientation as Jed Margolin-style unit vectors instead of Euler angles.
The window and input go through SDL2, so it runs on Windows and Linux.

```
SoftEngine.Core/    engine only: OBJ loader, Frame (unit vectors), FrameBuffer, WireframeRenderer
SoftEngine.App/     SDL2 host: window, streaming texture, keyboard + gamepad
SoftEngine.Tests/   xUnit tests for the projection, near clipping, orthonormality and OBJ loading
models/             ship01.obj, ship04.obj
```

## Setup

Requires the .NET 8 SDK or later. To use a newer SDK, change `net8.0` to `net10.0` in the three .csproj files.

**Linux (Debian):**
```
sudo apt install libsdl2-2.0-0
```

**Windows:** download the SDL2 release zip `SDL2-2.x.x-win32-x64.zip` from
https://github.com/libsdl-org/SDL/releases. Use the **SDL2** release, not SDL3.
Put `SDL2.dll` in `SoftEngine.App/` and the build copies it to the output folder.

## Run

```
dotnet run --project SoftEngine.App -- models
dotnet run --project SoftEngine.App -- models/ship01.obj path/to/other.obj
dotnet test SoftEngine.Tests
```

To use Visual Studio, create a solution file:
```
dotnet new sln -n SoftEngine
dotnet sln add SoftEngine.Core SoftEngine.App SoftEngine.Tests
```

## Controls

| Action | Logitech F310 (switch on **X**) | Keyboard |
|---|---|---|
| Turn model left/right, tip up/down | Left stick | Arrow keys |
| Roll | Right stick X | Q / E |
| Zoom in/out (you can fly into the model) | Right stick Y | W / S |
| Next / previous model | RB / LB | Tab |
| Reset view | A | R |
| Auto-spin | X | Space |
| Quit | Back | Esc |

If pushing the stick up tips the model the wrong way, set `InvertPitch` in `Program.cs`.

## OBJ support

Reads `v`, `f`, `l` and `o` lines. Index tokens can be `7`, `7/2`, `7//3` or `7/2/3`, and
negative (relative) indices are supported. Every face becomes a closed outline, and an edge
shared by two faces is drawn once. `vt`, `vn`, `mtllib`, `usemtl`, `s` and `g` are ignored.
