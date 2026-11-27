# Engine notes: picking, hover order, agent separation, MultiMesh highlight (Godot 4.7, C#)

Scope: documentation lookups only. Nothing in the game was changed, built or run.

**Source caveat.** `docs.godotengine.org` could not be resolved from this session (`getaddrinfo ENOTFOUND`), so no page was fetched directly. The Godot 4 stable class and tutorial text below was taken from the Context7 mirror of the official docs (library `/websites/godotengine_en_stable`). Each quoted item gives the docs.godotengine.org URL that Context7 reports as its source. Those URLs were not opened here. Items marked **not confirmed** were not found in any source reached. Godot 4.7-specific changes were not found (see section 6).

Abbreviations used below: Camera3D = `Camera3D`, MultiMesh = `MultiMesh`, NavigationAgent3D = `NavigationAgent3D`, NavigationServer3D = `NavigationServer3D`.

---

## 1. Picking one MultiMesh instance under the mouse, without physics

**Mouse to ray (orthographic Camera3D).** `Camera3D.ProjectRayOrigin(Vector2)` returns the world-space origin of the ray through a screen point. `Camera3D.ProjectRayNormal(Vector2)` returns its direction. The docs say the origin is "projecting a point on the Viewport rectangle by the inverse camera projection. Useful for casting rays for object intersection or picking." With `PROJECTION_ORTHOGONAL` the ray origin moves across the view plane and the normal is the same for every pixel. This is the view direction. The ray is therefore a parallel "tube" instead of a cone from one eye point.

```csharp
var cam = GetNode<Camera3D>("Camera3D");
Vector2 mouse = GetViewport().GetMousePosition();
Vector3 origin = cam.ProjectRayOrigin(mouse);
Vector3 dir = cam.ProjectRayNormal(mouse);   // normalised, world space
```

- Sources: https://docs.godotengine.org/en/stable/classes/class_camera3d.html (methods `project_ray_origin`, `project_ray_normal`); https://docs.godotengine.org/en/stable/tutorials/physics/ray-casting.html (C# example).
- Orthographic sizing: `Camera3D.SetOrthogonal(size, zNear, zFar)` and `Camera3D.Size` ("in meters measured as the diameter of the width or height ... Only applicable in orthogonal and frustum modes"). Source: https://docs.godotengine.org/en/stable/classes/class_camera3d.html.

**Option A: ray against a sphere or capsule per villager, in code.** Per click: one ray, then for each villager a ray-sphere test (or a ray-capsule test) on the villager's position. Cost: O(N) per click, N ≈ 40 to a few hundred. This is a few hundred dot products per click and no per-frame work. Use the sim's positions (not the render buffer), so the test is deterministic and independent of rendering. Expected result: exact 3D hit, plus the nearest hit by distance along the ray.
- Note: this is ordinary geometry in code. It is not an engine API, and no Godot page was needed for it.

**Option B: project each villager to the screen and test a rectangle or circle.** `Camera3D.UnprojectPosition(Vector3)` gives a `Vector2` screen point ("Unprojects a world position to a screen point"). Test the click against a screen-space circle of fixed pixel radius around it. Cost: one projection per villager per click, or cache the projections once per frame if the mouse is moving. Source: https://docs.godotengine.org/en/stable/classes/class_camera3d.html (`unproject_position`).

**Which is simpler here.** With an orthographic camera tilted about 53° down, a screen-space circle is the simplest and it matches what the player sees. A villager's on-screen footprint is almost the same size at any depth, so a fixed pixel radius is sensible. The 3D ray (Option A) is more exact when villagers overlap in depth, because the nearest hit wins by distance along the ray. For "click a villager", either is fine. Option B is cheaper to tune. Option A gives a single, exact pick per click. Both can be done without any physics engine.

**Built-in MultiMesh picking.** None found. The MultiMesh class page (https://docs.godotengine.org/en/stable/classes/class_multimesh.html) documents the buffer, instance count, custom data, transforms and visible instance count. It documents no picking or collision API. This matches the owner's statement. Source for the absence: the class page above, reached through Context7. (Mark: absence of picking confirmed by reading the class method list through Context7, not by a full page fetch.)

**Determinism.** Picking reads positions and never writes to the simulation, so it is safe for the "same seed, same result" rule as long as it only reads sim state.

## 2. Picking with physics

**Area3D or StaticBody3D per villager, plus `PhysicsDirectSpaceState3D.IntersectRay`.** Ray queries use `PhysicsRayQueryParameters3D.Create(from, to)` and `space.IntersectRay(query)`. The query takes `CollideWithAreas = true` if the shapes are `Area3D`. Source: https://docs.godotengine.org/en/stable/tutorials/physics/ray-casting.html (C# example: `var query = PhysicsRayQueryParameters3D.Create(origin, end); query.CollideWithAreas = true; var result = spaceState.IntersectRay(query);`). The result is a Dictionary with `collider`, `position`, `normal`, and so on. An empty dictionary means no hit. Source: https://docs.godotengine.org/en/stable/classes/class_physicsdirectspacestate3d.html.

**Input on the node.** `CollisionObject3D.InputRayPickable` (default `true`) lets an object "detect the mouse pointer entering/leaving, and if the mouse is inside it, report input events. Requires at least one collision_layer bit to be set." The signal `InputEvent` is emitted as `input_event(camera, event, event_position, normal, shape_idx)`. The signal `MouseEntered` is emitted when the pointer enters a shape. Source: https://docs.godotengine.org/en/stable/classes/class_collisionobject3d.html. The docs add a caveat: `mouse_entered` may not be emitted in the expected order if the mouse moves fast and the shape is small.

**Cost of moving 40 to 200 shapes every frame.** Not measured here, and no official number was found. **Not confirmed.** Known points from the docs: the physics server must be told the new transform each physics step, and the space state can be locked during `_Input` (the ray-casting tutorial advises running the query in `_PhysicsProcess`). Rebuilding or moving shapes for hundreds of bodies every frame is a real per-frame cost. Measure it in the game before choosing it.

**Determinism.** Drawing-side physics is separate from the sim. It does not change the sim's result, provided the sim never reads from the physics space. The owner's rule (no physics engine in the simulation) holds if the physics shapes are only written from sim positions and only read for input.

## 3. Hover and click order in Godot 4 when Control nodes overlay a 3D view

**Event order.** Godot 4 calls, in order: `_Input` (all nodes), then GUI `_GuiInput` on Controls (through the mouse filter), then `_ShortcutInput`, then `_UnhandledKeyInput`, then `_UnhandledInput`. The docs for `_unhandled_input`: "Called when an InputEvent hasn't been consumed by _input() or any GUI Control item. ... For gameplay input, this method is usually a better fit than _input(), as GUI events need a higher priority." It is "only called if unhandled input processing is enabled, which is done automatically if this method is overridden." Source: https://docs.godotengine.org/en/stable/classes/class_node.html. To stop an event reaching later nodes, call `GetViewport().SetInputAsHandled()`. Source: https://docs.godotengine.org/en/stable/classes/class_viewport.html.

**Mouse filter.** `Control.MouseFilter`:
- `MOUSE_FILTER_STOP` (0): the control receives the event and "the event will be marked as handled ... not propagate further to other controls."
- `MOUSE_FILTER_PASS` (1): the control receives the event, but the event bubbles up if not handled.
- `MOUSE_FILTER_IGNORE` (2): the control does not receive the event.
Source: https://docs.godotengine.org/en/stable/classes/class_control.html.

**Is the pointer over a Control.** `Viewport.GuiGetHoveredControl()` returns the Control under the mouse, or null ("Typically the leaf Control node ... This is very useful when used together with Node.is_ancestor_of() to find if the mouse is within a control tree."). Source: https://docs.godotengine.org/en/stable/classes/class_viewport.html.

**Recommended pattern (no mouse-to-Control overlap, so the shop click never picks a villager):**
- Put the shop and panels on Controls with `MouseFilter = STOP` on the root of the panel.
- In `_UnhandledInput`, pick villagers only when the click has not been consumed. The GUI consumes the click if it is over a STOP control.
- Add an explicit guard: if `GetViewport().GuiGetHoveredControl() != null`, do not pick. This covers controls with `PASS` or `IGNORE` filters that do not consume the click.
- Hover highlights should read the same guard.

Sources: docs above. The guard pattern is a recommendation, not a documented Godot idiom.

## 4. Keeping agents from overlapping

**What Godot offers.** NavigationAgent3D avoidance ("RVO avoidance callback on the NavigationServer3D") with these properties (source: https://docs.godotengine.org/en/stable/classes/class_navigationagent3d.html):
- `avoidance_enabled` (default `false`): "Avoidance processing with many registered agents has a significant performance cost and should only be enabled on agents that currently require it."
- `radius` (default `0.5`): "the body of the avoidance agent ... Does not affect normal pathfinding."
- `neighbor_distance` (default `50.0`): "The distance to search for other agents."
- `max_neighbors` (default `10`): "The maximum number of neighbors for the agent to consider."
- `time_horizon` agents (NavigationServer3D: `agent_set_time_horizon_agents`): "The minimal amount of time for which the agent's velocities ... are safe with respect to other agents ... Must be positive." Source: https://docs.godotengine.org/en/stable/classes/class_navigationserver3d.html.
- Velocity callback: the `velocity_computed` signal delivers `safe_velocity` after the avoidance step. Source: https://docs.godotengine.org/en/stable/classes/class_navigationagent3d.html.

**Determinism.** **Not confirmed.** The docs do not say whether the avoidance step is deterministic, and the avoidance runs on the navigation server's own schedule. Its results are returned by callback and signal, so it cannot be relied on for "same seed, same result" without a test. Treat it as drawing-side or a non-authoritative helper.

**Without a navigation mesh, for fixed road paths.** The NavigationServer avoidance is tied to agents in a navigation map. The docs describe it as an agent avoidance callback, and the docs do not say it needs a navigation mesh to run. **Not confirmed** whether it works for agents that only follow a fixed polyline with no mesh. The safer path for the game is to do lane and queue logic in the simulation (see below).

**Limits.** Performance cost is stated in the docs (above). Agents on a navigation map have a radius and a neighbour search, not per-instance shape tests. The docs do not state a max count.

**Standard techniques outside the engine.**
- Boids separation (Reynolds): steer away from neighbours inside a radius, with force growing as distance shrinks. Source: C. Reynolds, "Flocks, Herds, and Schools: A Distributed Behavioral Model" (SIGGRAPH 1987), and "Steering Behaviors for Autonomous Characters" (GDC 1999): https://ics-websites.science.uu.nl/docs/vakken/mcrws/papers_new/Reynolds%20-%201999%20-%20Steering%20behaviors%20for%20autonomous%20characters.pdf (found via search; not opened in full).
- RVO and ORCA (van den Berg, Guy, Lin, Manocha): "Reciprocal n-Body Collision Avoidance" (ORCA), and "Reciprocal Velocity Obstacles for Real-Time Multi-Agent Navigation" (RVO, ICRA 2008). Source: https://www.cs.unc.edu/~geom/RVO/icra2008.pdf and https://ics-websites.science.uu.nl/docs/vakken/mcrws/papers_new/van%20den%20Berg%20et%20al%20-%202011%20-%20Reciprocal%20n-body%20collision%20avoidance.pdf (found via search; venue details not confirmed against DBLP).
- Social-force model (Helbing and Molnar, 1995): agents feel attraction to a goal and repulsion from others. Source: **not confirmed**, not searched in this session.
- Lane assignment and queueing along a path: keep each villager on a lane index and a position along a fixed polyline, and block a villager only when the one ahead is closer than a set gap. This is a common game technique. No external source was searched; it needs no engine feature and is deterministic if done in the sim.

## 5. Outlining or highlighting one selected MultiMesh instance

**Per-instance custom data.** `MultiMesh.UseCustomData` (`set_use_custom_data`) must be set before instance count is set, or with instance count at `0`: "Can only be set when instance_count is 0 or less." Then `MultiMesh.SetInstanceCustomData(int instance, Color data)` stores four floats (a `Color`). "This custom instance data has to be manually accessed in your custom shader using `INSTANCE_CUSTOM`." Source: https://docs.godotengine.org/en/stable/classes/class_multimesh.html.

```csharp
mm.UseCustomData = true;             // must be set while InstanceCount is 0 or less
mm.InstanceCount = 200;              // then set the count
mm.SetInstanceCustomData(i, new Color(selected ? 1f : 0f, 0f, 0f, 0f));
```

Correct order: set `UseCustomData = true` first, then `InstanceCount`. (The docs say `use_custom_data` can only change when `instance_count` is 0 or less.)

Shader side: read `INSTANCE_CUSTOM` (a `vec4`) in a `vertex()` or `fragment()` function. Source: https://docs.godotengine.org/en/stable/classes/class_multimesh.html.

Data format: "Each number is stored in 32 bits in the Forward+ and Mobile rendering methods, but is packed into 16 bits in the Compatibility rendering method." Source: same page.

**Ring decal or second mesh under the selected villager.** Use a second `MultiMeshInstance3D` with a single instance, positioned at the selected villager's sim position each frame, or set one instance in a second MultiMesh. Cost: one transform write per frame. Not confirmed against a Godot page for this exact arrangement; it uses the documented `SetInstanceTransform` and the node transform.

**Card next to a 3D position.** `Camera3D.UnprojectPosition(worldPoint)` returns the screen point. Place a `Control` at that position each frame. Source: https://docs.godotengine.org/en/stable/classes/class_camera3d.html.

## 6. Limits and changes

- `MultiMesh.VisibleInstanceCount` (default `-1`, "draws all instances"): "Limits the number of instances drawn ... Changing this does not change the sizes of the buffers." Source: https://docs.godotengine.org/en/stable/classes/class_multimesh.html. The RenderingServer equivalent is `multimesh_set_visible_instances`. Source: https://docs.godotengine.org/en/stable/classes/class_renderingserver.html.
- `TransformFormat` must be set before the instance count (the official tutorial sets transform format first, then `instance_count`). Source: https://docs.godotengine.org/en/stable/tutorials/performance/using_multimesh.html.
- Custom data: 4 floats per instance, 32-bit in Forward+/Mobile and 16-bit in Compatibility. Source: MultiMesh class page above.
- Changes in 4.7: **not confirmed**. Context7 returned only the 4.3 and 4.4 upgrade pages (navigation changes: `NavigationRegion2D` avoidance properties removed in 4.3, `query_path` callback in 4.4). No 4.5 to 4.7 MultiMesh or avoidance note was found. The 4.7 release notes and the "upgrading" pages for 4.5 to 4.7 were not reached.

## Not confirmed (list)
- Godot avoidance determinism, and use without a navigation mesh.
- Cost of moving 40 to 200 physics shapes per frame.
- Any Godot 4.5 to 4.7 change to MultiMesh custom data, visible count or navigation avoidance.
- Social-force model source.
- Venue details of the ORCA and Reynolds sources, which were taken from search snippets and not opened.
