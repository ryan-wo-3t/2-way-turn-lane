# Contributing

Use a branch and a reviewable pull request. Run the core executable tests with the commands in README. Changes to safety calculations need counterexample and lifecycle tests; a compiling mod does not establish safe traffic behaviour.

Keep `Twtl.Core` free of game, Unity, Road Builder and Harmony dependencies. Use metres and simulation seconds; record coordinate orientation, vehicle entity generation, road revision and authority of each observation. Serialize calls to the current managed reservation prototype; it is not job-safe.

When changing game adapters, record the game display version, Game.dll file version and MVID, Road Builder commit/assembly version, exact member signatures, system update order and local compiler output. Include actual evidence for each runtime claim. Never publish proprietary binaries or decompiled game implementation. Do not copy third-party code without verifying its licence and retaining required notices.

Run game tests only in disposable cities. Keep all opposing traffic closed until the entry gate, route restriction and collision/braking experiment pass independent review. A future live adapter must prevent unreserved entry at the pathfinding graph and navigation levels; a reservation record alone is not enforcement.

Use the acceptance IDs in docs/TEST_PLAN.md. Include before/after road layouts, enabled mods, save/load and deletion tests, videos/captures, and reproducible timing measurements. Report NOT RUN rather than PASS when game access is unavailable.

Do not publish a Paradox Mods release or enable production traffic without the owner's test and approval.
