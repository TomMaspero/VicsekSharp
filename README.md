# VicsekSharp

Interactive simulation of the [Vicsek model](https://en.wikipedia.org/wiki/Vicsek_model) of collective motion, in C# with [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) and [Dear ImGui](https://github.com/ocornut/imgui).

Rewrite of my earlier C/GLUT version, [VicsekModel](https://github.com/TomMaspero/VicsekModel).

## The model

N particles move at constant speed in a periodic square box. Each step, every particle:

1. takes the mean heading of all particles within radius R (itself included), averaging unit vectors: `atan2(Σ sin θ, Σ cos θ)`;
2. adds a random angle uniform in [-η/2, η/2];
3. moves `speed` units along the new heading.

The polar order parameter φ = |Σ v̂ᵢ| / N goes from about 0 (disordered) to 1 (all aligned). Lowering the noise η or raising the density makes the system go through a transition to collective motion.

Neighbours are found with a cell list (cells at least R wide, 3x3 block search) and minimum-image distances, so the cost per step is about O(N).

## Running

Requires the .NET 10 SDK.

```bash
dotnet run --project src/Vicsek.App -c Release
```

```bash
dotnet test
```

## Controls

| Key | Action |
| --- | --- |
| Space | Pause / resume |
| S | Single step (while paused) |
| R | Reset with the current parameters |

The side panel shows φ over time. Noise, radius and speed act live; particle count and box size are applied on reset.

## Layout

- `src/Vicsek.Core` – the simulation, no graphics dependencies.
- `src/Vicsek.App` – Raylib window and ImGui panel.
- `tests/Vicsek.Tests` – xUnit tests for the model (angle averaging across 0, neighbours across cells and the periodic boundary, cell list vs brute force).

## Changes from the C version

- Neighbour search covers the 3x3 surrounding cells; the C version only searched the particle's own cell.
- Headings are averaged as vectors; the C version averaged the raw angles, so 359° and 1° gave 180°.
- Particles include themselves in the average, as in the original model.
- Distances use the minimum image, so particles interact across the periodic boundary.
- Cell index can no longer go out of range at the box edge.
- Parameters are adjustable at runtime, and φ is shown.
