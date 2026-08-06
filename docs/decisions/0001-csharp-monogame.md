# ADR 0001: C# and MonoGame

Status: Accepted

## Context

The Pygame prototype proved the ecosystem concept but accumulated coupled rules,
unbounded searches, and limited runtime diagnostics. Its replacement needs better
performance without adopting a full commercial engine.

## Decision

Use modern C# on .NET 10. Use MonoGame DesktopGL as the presentation and platform
layer. Keep simulation code in a framework-independent class library.

## Consequences

- The simulation runs headlessly in tests and benchmarks.
- DesktopGL provides one project for Windows, Linux, and macOS.
- Managed memory remains available, but hot paths avoid allocations.
- MonoGame supplies presentation primitives without dictating game structure.
