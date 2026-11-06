# Known limitations

This branch is not a working TWLTL traffic mod. It must not be published as one.

- The Road Builder adapter and diagnostics are implemented from inspected public API usage but have not been compiled against the licensed game or loaded. Closed-lane picker visibility and regeneration remain untested.
- The opt-in section has no car lanes. It occupies plain surface and provides no actual turn markings or TWLTL traffic function. It is explicitly labelled CLOSED. Once registered, turning the setting off does not unregister a live prefab; restart removes the picker entry.
- There is no verified representation of opposite logical lanes, native `LaneOverlap` injection, graph restriction, entry enforcement, car braking/speed integration, native reservation adapter or lane-change actuator. All game traffic activation is unavailable.
- Configuration validation accepts explicit physical strip classifications. It is not connected to an automatic pre-generation Road Builder validator or a placed-network classifier. Runtime evidence is caller-supplied; it is not inferred from member presence.
- Core calculations support arbitrary compatible lane counts, asymmetry and both handedness conventions, but this has only been tested in the standalone model.
- Full route swept intervals are conservative, including for same-direction vehicles. Space is not reused merely because an ETA passed. Fairness/priority ageing and native lane-object occupancy reconstruction are not implemented.
- Overdue occupied vehicles retain safety holds and close admissions. Automatic stuck-vehicle recovery needs real occupancy, a verified exit and native navigation integration. Releasing a hold by timeout would be unsafe.
- Gap advice assumes complete local observations and constant-speed neighbours. It considers three acceleration choices; it is not an exhaustive search and may yield despite a feasible gap. No result authorizes a lane change. Hysteresis currently affects preferences among candidate gaps and is tested for unchanged-observation stability, not fluctuating game traffic.
- Intersection transition intervals are calculated; actual lane geometry, turn connection generation and signals are not changed. Short segments get a closed fallback.
- Tram support is not runtime-tested. Crossable tracks produce a configuration WARNING; impassable tracks are INVALID. Native turning-car/tram conflict processing is unknown.
- Save/load, updates, mod disposal and removal have not been tested with research roads. Road Builder documents rebuilding roads without missing custom sections. This can change a road layout; do not assume uninstall safety or save compatibility.
- No automated compatibility enforcement for other mods exists. The API audit logs assemblies loaded at that instant; it can miss later-loaded assemblies. Source review identified overlapping systems but no combined playset was tested.
- The current managed controller allocates during batches and reconciliation, is not thread-safe or Burst-ready, and has no measured large-city budget. Diagnostic captures may synchronize jobs and stall; they run only when requested and disclose truncated counts.
- Only actual research controls appear in game options. Intended/exit/urgent distances, speed, safety clearance and transition length are configurable constructor parameters in the tested core; player-facing traffic settings and debug visualization are intentionally not exposed until they have a working game adapter.
- No Paradox dependency metadata/package or release is provided. Road Builder is the intended required mod; the others are optional research/compatibility targets.
