# Nibrāsk AR Navigation System

An augmented reality navigation system designed for airport terminals (and expandable to other indoor spaces). It uses Unity's AR Foundation to anchor a virtual terminal graph to the real world, providing the user with an interactive floating UI, grounded spatial paths, directional arrows, and real-time navigation feedback.

---

## 🏛️ System Architecture

The project is built on a highly decoupled, state-driven architecture. Systems communicate with each other exclusively through a centralized event bus (`AppEvents`) and state machine (`AppStateManager`).

### 1. State Management (`AppStateManager.cs`)
The application is strictly driven by an `AppState` enum:
*   **`Onboarding`**: Initial instructions display.
*   **`Scanning`**: Floor detection and terminal anchor placement.
*   **`DestinationSelection`**: Display floating 3D menu.
*   **`Navigating`**: Active traversal mode with path rendering and distance tracking.
*   **`Arrival`**: User has reached their destination.

### 2. Event Bus (`AppEvents.cs`)
All cross-component communication happens here. 
*   **AR Events:** `OnFloorDetected`, `OnTerminalAnchorPlaced`
*   **Navigation Actions:** `OnDestinationSelected`, `OnNavigationStarted`, `OnArrived`, `OnNavigateAgain`
*   **Tracking Updates:** `OnDistanceUpdated`, `OnCheckpointReached`, `OnOffRoute`, `OnBackOnRoute`, `OnRouteRecalculated`
*   **Obstacle Detection:** `OnObstacleDetected(nodeA, nodeB)`, `OnObstacleCleared(nodeA, nodeB)`

This architecture allows the UI, Audio, Haptics, and AR visualizers to react to events without tightly coupling their references to the Navigation engine.

---

## 🗺️ Data & Graph Logic (Nibrask.Data & Nibrask.Navigation)

### Terminal Map Data
The layout of the environment is defined via ScriptableObjects.
*   **`TerminalMapData.cs`**: Contains the full network of abstract waypoints (nodes and relative positions) and references to all destinations. It also features a `scaleFactor` to expand or shrink the entire terminal without modifying individual coordinates.
*   **`DestinationData.cs`**: Represents specific POIs (Gates, Services, Exits, Restrooms). Contains UI metadata (Icons, Flight Numbers, Names) and hooks into the waypoint graph via a `nearestWaypointIndex`.

### Graph Building (`WaypointGraph.cs` & `WaypointNode.cs`)
When `OnTerminalAnchorPlaced` is fired by the AR system:
1.  `WaypointGraph.BuildGraph()` runs.
2.  It spawns unseen `WaypointNode` GameObjects in the scene.
3.  Each node's position is calculated **relative to the terminal AR anchor**, making the entire graph shift and rotate perfectly to match wherever the user tapped on the floor.
4.  Nodes parse their `connectedNodeIds` to create bidirectional edges for traversal.

### Pathfinding (`PathFinder.cs`)
An optimized **A* (A-Star) search algorithm** that takes a start node and an end node, utilizing the world-space distance between nodes as both the *g-cost* and *h-cost* (heuristic). It returns a `List<WaypointNode>` representing the shortest path. Supports an optional `HashSet<(int,int)> blockedEdges` parameter — edges in this set are skipped during neighbor exploration, allowing A* to naturally route around obstacles.

---

## 🚶‍♂️ Navigation Engine (Nibrask.Navigation)

The heart of the application, orchestrating the journey from point A to point B.

### `NavigationManager.cs`
The brain. When a destination is selected:
1.  Projects the user's `Camera.main` position down to the floor (Y = Terminal Anchor Y) to eliminate eye-height bias.
2.  Queries `WaypointGraph` for the closest starting node based purely on XZ (horizontal) distance.
3.  Fetches the destination node and asks `PathFinder` for the route.
4.  Hands the resulting `List<WaypointNode>` to the visualizers and `DistanceTracker`.
5.  If an `OnOffRoute` or `OnObstacleDetected` event fires, it automatically initiates a `RecalculateRoute` loop (cooldown 1.5s). Obstacle events that arrive during cooldown are queued via a `pendingReroute` flag and processed on the next Update tick.

### `DistanceTracker.cs`
Runs on an `InvokeRepeating` loop (every 0.2s) to monitor user progress.
*   **Checkpoint Parsing:** Checks if the user is within `checkpointReachDistance` (1.2m) of their next target `WaypointNode`. If so, advances the index, fires `OnCheckpointReached`, and trims the path line.
*   **Distance to Path:** Uses geometric point-to-line-segment math horizontally (XZ plane) to calculate how far the user has strayed from the active line segment. If distance > `offRouteThreshold` (3.0m), fires `OnOffRoute`.
*   **Arrival Detection:** Checks distance directly against the absolute final destination. If within `arrivalDistance` (1.5m), fires `OnArrived`.
*   **Time Calculation:** Yields `distanceMeters` and `estimatedTimeSeconds` (assuming 1.4m/s walking speed) via `OnDistanceUpdated`.

---

## 🎨 Visuals & UI

### Navigation Renderers
*   **`PathRenderer.cs`**: Uses a `LineRenderer` to draw a flowing line connecting all path nodes. Trims itself backwards natively as the user passes checkpoints. Uses a cloned runtime material instance to animate UV offsets, creating a forward-flowing chevron effect on the line without corrupting the material asset in the Editor.
*   **`ArrowGenerator.cs`**: Spawns physical 3D arrow prefabs along the path segments. Arrow spacing is configured to ~1.2m. Arrows are managed via an Object Pool. As the user passes checkpoints, arrows behind the user are disabled to keep the AR view clean.

### User Interface (`Nibrask.UI`)
*   **`OnboardingUI`**: Manages the start screen, transitions, and faked loading bar during floor detection. Shuts off `Update` loop calculations nicely when invisible.
*   **`DestinationSelectionMenu`**: A `RenderMode.WorldSpace` floating canvas. Dynamically instantiates categorized buttons based on the `TerminalMapData`. Leverages the `Billboard.cs` component to constantly face the camera while locking pitch/roll so it stays upright.
*   **`GateInfoPanel`**: Persistent floating UI panel anchored safely in the user's view displaying current destination name, active ETA, and distance updates.
*   **`FeedbackPanel`**: Handles the top-of-screen progress bar, showing `Checkpoints: X/Y`.
*   **`ArrivalPanel`**: Spawns exactly 1.2m in front of the user when `OnArrived` fires, congratulating them and giving them a "Navigate Again" button, which loops the state back to `DestinationSelection`.

---

## 📱 AR Subsystems (`AREnvironmentManager.cs`)

Manages the core `ARFoundation` components (`ARPlaneManager`, `ARRaycastManager`, `ARAnchorManager`, and optionally `ARMeshManager`).
*   **Scanning State:** Enables plane visuals so the user can see horizontal planes being tracked. Touch input runs raycasts against planes.
*   **Anchor Placement:** When the user taps a valid plane, an `ARAnchor` is spawned. This becomes the `Terminal Origin`.
*   **Navigation State:** Disables the plane visualizers. If an `ARMeshManager` is assigned, the mesh subsystem stays **active** (for obstacle detection raycasts) but mesh **visuals are hidden** so the AR view stays clean. Exposes a `MeshingAvailable` property for other systems to check.
*   **Arrival State:** Disables both plane and mesh detection to save battery.

---

## 🔔 Feedback Managers (`Nibrask.Feedback`)

*   **`HapticFeedbackManager.cs`**: Uses `AndroidJavaClass` to access native Android vibrator systems. Triggers varied pulse durations based on events (Light pulse for checkpoints, long heavy vibration for off-route or arrival).
*   **`AudioFeedbackManager.cs`**: Uses pooled `AudioSource` components with cooldown mechanisms to drop spatial/UI sound effects for events without overlapping awkwardly.

---

## 🛠️ Debug & Utilities

*   **`CanvasDebugger.cs`**: An invisible GUI element that overlays raw manual force-transitions for `AppStateManager` for easy Editor/device testing without needing physical space.
*   **`DestinationMarkerSpawner.cs`**: Tooling to easily spawn generic physical markers (cubes) or specific prefabs mapped to `DestinationTypes` (Gates, Restrooms). Spawns `TextMeshPro` world-space labels above them, dynamically adjusting backplate width and scale to ensure readable text against varying world backgrounds.

---

## 🧱 Obstacle Detection (`ObstacleDetector.cs`)

A hybrid obstacle avoidance system that detects physical obstructions blocking navigation path segments using raycasts, without the performance cost of full NavMesh baking.

### How It Works
1.  When navigation starts, `ObstacleDetector` begins probing **only the active path segments** (typically 3–8 edges, not the full graph).
2.  Every `probeInterval` seconds (default: 1.0s), it casts `SphereCast` rays along each segment at multiple heights (0.3m, 0.6m, 1.0m above floor).
3.  If rays hit colliders above floor level, an internal hit counter increments. After `obstacleDebounceCount` (default: 3) consecutive detections, the edge is marked **blocked**.
4.  `NavigationManager` passes `ObstacleDetector.BlockedEdges` to `PathFinder.FindPath()`, which skips blocked edges during A* neighbor exploration.
5.  A* naturally finds an alternative route. If ALL paths are blocked, `OnRecalculationFailed` fires.
6.  When the obstacle is removed, raycasts clear. After a `clearCooldown` (default: 2s), the edge is unblocked and the system reroutes to the optimal path.

### Design Decisions
*   **Debounce counter** prevents false positives from noisy AR mesh data.
*   **Clear cooldown** prevents path flickering when an obstacle appears/disappears rapidly.
*   **Graceful degradation**: If `ARMeshManager` is not assigned or the device lacks depth sensors, `ObstacleDetector` still works — it just won't detect obstacles through mesh colliders (the system falls back to the existing off-route rerouting).
*   **Pending reroute flag**: If an obstacle event fires during the recalculation cooldown, it's queued and processed on the next `Update` tick rather than being silently dropped.

---

### Important Edge Cases Addressed
1.  **3D Math in a 2D Floorplan**: Because a user holds a phone at ~1.6m high, regular `Vector3.Distance` checks between the camera and a node on the floor heavily inflate the result. `DistanceTracker` and `WaypointGraph.FindNearestNode` explicitly project camera positions down to the floor's Y-level before doing proximity matching.
2.  **Start == End Reroutes**: If a user reroutes while physically standing *on* their destination, the path generated is a zero-length segment (`List<WaypointNode> {start, start}`). Handling this prevents the `LineRenderer.SetPositions` from crashing due to requiring `>= 2` nodes, allowing the system to instantly trigger `OnArrived` on the immediate frame tick.
3.  **Obstacle during arrival**: `HandleArrived` sets `isNavigating = false` before `HandleObstacleDetected` can fire, preventing a stale reroute.
4.  **All paths blocked**: A* returns an empty list → `RecalculationFailed` fires → UI shows "No alternative route" → last valid path stays visible so the user can manually navigate.
5.  **Double events (off-route + obstacle)**: The 1.5s recalculation cooldown absorbs the second event. Only one reroute happens per cooldown window.
