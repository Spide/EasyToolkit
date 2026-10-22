# Easy Toolkit Simulation LOD

Runs only as much of each AI agent as the player can notice. Agents near an
observer (a hero, a player, a bot) run in full; farther ones think slowly;
far ones are frozen or parked (their object is deactivated); sleeping ones
stay **Dormant** until something wakes them. Volumes hold whole rooms of
sleeping agents until a door opens. Per-frame budgets keep every change and
every behaviour tree tick inside a frame budget, and a steady frame allocates
nothing.

Assemblies:

| Assembly | Contents |
| --- | --- |
| `Easy.SimulationLod` | tiers, solver, hysteresis, budget, `SimulationLodDriver`, `SimulationAgent`, `SimulationVolume`, `SimulationObserver`, `SimulationSegmentMap`, built-in listeners |
| `Easy.SimulationLod.BehaviourTree` | `SimulationTreeTicker<T,V>`, `SimulationBehaviourTreeRunner<T,V>` (depends on `Easy.BehaviourTree`) |
| `Easy.SimulationLod.Editor` | volume and agent gizmos, driver runtime inspector |

Manifest (list `behaviourtree` and `logging` too, see the root README):

```json
"com.easytoolkit.logging": "https://github.com/Spide/EasyToolkit.git?path=/Packages/com.easytoolkit.logging#vX.Y.Z",
"com.easytoolkit.behaviourtree": "https://github.com/Spide/EasyToolkit.git?path=/Packages/com.easytoolkit.behaviourtree#vX.Y.Z",
"com.easytoolkit.simulationlod": "https://github.com/Spide/EasyToolkit.git?path=/Packages/com.easytoolkit.simulationlod#vX.Y.Z"
```

## Tiers

| Tier | Meaning | Reached by |
| --- | --- | --- |
| `Full` | everything runs | near an observer, on screen, sticky, woken |
| `Minimal` | slower thinking (`MinimalThinkScale`, default 5x), culled animation | within `MinimalRadius`, or the neighbouring district |
| `Off` | nothing runs; parked (object inactive) when `ParkWhenOff` | far away |
| `Dormant` | asleep whatever the distance, camera and stickiness | a volume or a dormancy request only |

Rules:

- A promotion (towards Full) is immediate. A demotion waits
  `DemotionDelaySeconds` and is never applied while the agent is on screen
  (view camera frustum, or within `ObserverViewRadius` of any observer).
- Sticky agents (an `ISimulationStickiness` component, the policy's
  `IsSticky`, a noise or damage wake window) are Full anywhere.
- **Dormant** is forced: it ignores distance, camera, stickiness and the
  demotion delay. Only an explicit activation leaves it: a volume activation,
  `TryWakeManaged`, `SetDormant(agent, false)`, a noise routed through
  `NotifyNoise` (for agents that allow it), damage (`NotifyDamaged` or an
  `ISimulationHealth` drop), or `BeginWakeAll(includeDormant: true)`. Once
  activated the agent follows the normal rules (leaving Dormant is a
  promotion, so it is immediate). Dormant does not park the object unless
  `ParkWhenDormant` is set, so a sleeping zombie stays visible, frozen.
- A volume **cap** (`InactiveTier` = Minimal or Off) is applied without the
  demotion delay, only when it runs less than the distance rules would, and
  **never on screen**: an agent in the view camera's frustum or within
  `ObserverViewRadius` follows the normal rules, so a cap never parks or
  freezes an agent an observer can see.
- Dormant ignores the camera by design. Keep the occupants of Dormant volumes
  out of view (inside buildings, cellars, closed rooms); that is also why
  `ParkWhenDormant` is off by default, so a sleeper that is seen anyway stays
  visible, frozen, instead of vanishing.
- A volume hold (its Dormant or cap) never takes an **awake** agent that is
  sticky (an `ISimulationStickiness`, the policy, a noise or damage wake
  window) or on screen: the agent keeps running and joins the hold at its
  first evaluation where it is calm and unseen. Only the first application
  at adoption (and at readmission, which counts as a new adoption) is not
  protected, so sleepers placed in a building start asleep. An explicit
  `SetDormant(agent, true)` is never protected; `SetDormant(agent, false)` on
  an occupant of an inactive volume is (the volume's hold waits).
- Without observers the distance rules pause: tiers stay as they are, while
  dormancy and volumes still apply. Disabling the driver returns every managed
  agent to Full.

## Setup

1. Add a `SimulationLodDriver` to the level (one per scene). Tune its
   `Settings` (radii, delay, budgets) and the solve interval.
2. Mark observers: a `SimulationObserver` on each hero, or
   `driver.SetObservers(list)`, or an `ISimulationObserverSource`.
3. Add a `SimulationAgent` to every AI root. Agents register themselves and
   the driver adopts every agent of its scene (`AutoAdoptAgents`), or call
   `driver.Register(agent)`.
4. Put listeners next to the agent (it finds them on its object and
   children):
   - `SimulationAnimatorListener`: authored culling at Full,
     `CullUpdateTransforms` at Minimal, `CullCompletely` asleep.
   - `SimulationNavMeshAgentListener`: stops the NavMeshAgent while the agent
     does not think, restores it; lower avoidance quality below Full.
   - `SimulationBehaviourToggle`: enables a list of Behaviours only up to a
     tier (a sensor, an effect).
   - your own `ISimulationTierListener` components, or plain objects through
     `agent.AddListener`.
5. Tell the agent about game state with small components, or subclass
   `SimulationAgent` and override the virtual properties:
   - `ISimulationEligibility.IsSimulationEligible` false (dead, despawning)
     withdraws it.
   - `ISimulationStickiness.IsSimulationSticky` true (alerted, fighting)
     keeps it Full.
   - `ISimulationHealth.SimulationHealth` is polled; a drop counts as damage.
6. Optional: `driver.SetPolicy(ISimulationLodPolicy)` for orders that keep an
   agent Full and for forbidding parking (an online actor that is not
   replicated yet: Off becomes Minimal, Dormant freezes in place).
7. Optional: a `SimulationSegmentMap` (districts baked by a level tool):
   the observer's district is Full, its neighbours Minimal, the rest Off,
   with the radii still applying.

```csharp
driver.SetObservers(heroTransforms);          // or SimulationObserver components
hero.Footstep += n => driver.NotifyNoise(n.Origin, n.Radius);
health.Damaged += () => agent.NotifyDamaged();
```

## Parking

`Off` (and `Dormant` with `ParkWhenDormant`) deactivates the agent's whole
object, because a sleeping AI's components cost more than its tree. Rules:

- Listeners are told **before** a park (`change.Parked` true) and after the
  unpark. Keep state across a park; do not tear anything down.
- `agent.IsParking` is true only inside the agent's own `SetActive(false)`,
  `agent.IsUnparking` only inside its own `SetActive(true)`. A component may
  keep episodic runtime state across `IsParking` and restore it only under
  `IsUnparking`. Any other disable or enable (death, external `SetActive`,
  suspension, unload) never sees them and must release and rebuild as usual.
- A parked agent has no colliders and hears nothing. Code that counts or
  orders "all agents" must ask the driver or wake them (`BeginWakeAll`).
- External withdrawal: `driver.SuspendManaged(agent)` (listeners get
  `change.Suspended`, must stop without gameplay callbacks; ordinary solves
  never readmit it), `driver.ReadmitManaged(agent)` for an explicit new
  admission, `IsManagedParked(agent)`, `TryWakeManaged(agent, now)`.
- An outside deactivation (pooling, hiding: `SetActive(false)` or disabling
  the agent component) suspends the agent too. With `ReadmitOnEnable` (on by
  default) enabling it again readmits it, so its listeners and trees run
  again. An explicit `SuspendManaged` always stays out until
  `ReadmitManaged`, also across disable and enable, and a tree runner on an
  explicitly withdrawn agent never calls `Stop()`, not even on the later
  disable. Turn `ReadmitOnEnable` off for the original semantics (every
  withdrawal stays out).
- A readmission (`ReadmitManaged`, or a re-enable with `ReadmitOnEnable`) is
  a new admission: the previous run's volume state is forgotten, the agent
  joins the volume where it stands now and its hold applies at once, like at
  adoption.
- Disabling the driver (or `Unregister`) returns every managed agent to Full
  and reactivates any object it parked, also one that became ineligible or
  whose agent component was disabled while parked (explicitly suspended
  agents stay out). An interrupted `BeginWakeAll` then raises
  `WakeAllCompleted`.
- `BeginWakeAll()` wakes agents parked by distance, except those whose hold
  would park them again (Dormant with `ParkWhenDormant`, an Off cap); an agent
  parked by distance inside a Minimal-cap volume wakes. Held agents wake with
  `BeginWakeAll(includeDormant: true)`.

## Volumes

A `SimulationVolume` holds its occupants at `InactiveTier` (default
Dormant) until it is activated.

- Shape: its own box (`Center`, `Size`, rotation and scale apply) or a
  collider (`BoxCollider` and `SphereCollider` exactly, any other by closest
  point).
- Occupants: agents listed in `ExplicitOccupants`, plus (with
  `AssignByContainment`) every agent inside it when the driver adopts the
  agent or the volume appears. One volume per agent: an explicit listing
  wins, then the higher `Priority`, then the smaller (innermost) volume.
  `AddOccupant` / `RemoveOccupant` at runtime.
- Membership tracking (`TrackMembership`, on by default): the driver
  re-checks containment as it evaluates each agent on its round-robin slice
  (one point-in-box test per volume, allocation-free). Agents that walk in or
  are spawned inside later join, by the same explicit > priority > innermost
  order; containment occupants that walk out are released (explicit
  listings stay). An agent joining an inactive volume is held only once it is
  calm and out of view. An agent woken explicitly (`TryWakeManaged`,
  wake-all with dormant) is never captured again by containment; use
  `AddOccupant`. Off: membership is decided once, at adoption.
- A volume that appears or changes later (registered, enabled, added with
  `AddVolume`) claims only adopted agents that have no volume and were never
  released, and only by the tracking rules (explicit listing, or containment
  in a tracking volume). Agents that already have a volume keep it, and the
  hold protection applies, so spawning or toggling an unrelated volume never
  puts an awake sticky or visible agent to sleep.
- Activation: `Activate()` / `Activate(origin)` (a door, a script, a
  behaviour tree action node), a trigger collider entered by a collider that
  passes the filter (layers, tag, and by default it must be a
  `SimulationObserver` or a driver's observer), a noise that reaches it
  (`driver.NotifyNoise`), damage to an occupant.
- Wake: `AllAtOnce`, `Staggered` (`WakePerFrame`) or `NearestFirst` (sorted
  by distance to the activation origin). Every wake counts against the
  driver's `MaxActivationChangesPerFrame`, so a big room never spikes a frame;
  `Instant` ignores the driver budget (its own rate still applies). A woken
  agent stays Full for `KeepFullSeconds`, then follows the normal rules.
- Deactivation: `Never`, or `WhenObserversLeave` after
  `DeactivateAfterSeconds` without an observer inside. Occupants still inside
  go back to sleep (within the activation budget) as soon as they are calm and
  out of view; a fighting or visible occupant keeps running until then.
  Occupants that left are released (and may join a volume again).
- Events: `Activated`, `OccupantWoken`, `WakeCompleted`, `Deactivated` (C#),
  and UnityEvents `OnActivatedEvent`, `OnDeactivatedEvent`.
- Gizmos: bounds coloured by state (blue inactive, orange waking, green
  active), the occupant count, and lines to the occupants when selected.

### Example: the cellar wakes when the door opens

```csharp
public sealed class CellarDoor : MonoBehaviour
{
    [SerializeField] private SimulationVolume cellar;   // InactiveTier = Dormant, WakeMode = NearestFirst, WakePerFrame = 2

    public void Open()
    {
        // ... play the door animation ...
        cellar.Activate(transform.position);            // nearest zombies first, 2 per frame, within the driver budget
    }
}
```

The cellar's zombies are placed inside the volume (or listed in it). They
sleep beside the player and on screen, visible but frozen, until `Open`
runs; then they wake nearest first and chase as usual. With
`ActivateOnNoise` a gunshot next to the cellar wakes them too, and with
`ActivateOnOccupantDamage` shooting one through a window wakes the room.

A ready-made version is the **Cellar Door** sample (Package Manager >
Samples): a code-built scene with a Dormant cellar under the ground, sleepers
tinted by tier, and a door that opens on a timer or a button.

### Authority

Everything is a deterministic method call; the package does no networking.
In a host-authoritative game run the driver on the host only, activate
volumes there (disable `ActivateOnTrigger` on clients, or call `Activate`
from the host's door logic) and replicate the result (the agents' state) as
the game already does. Clients never compute tiers from their own camera.

## Behaviour trees

`SimulationTreeTicker<T,V>` ticks one tree: every `IntervalSeconds` at Full,
times `MinimalThinkScale` at Minimal, never at Off, Dormant or while
suspended. Each timed tick first asks the driver's frame think budget
(`MaxThinksPerFrame`, `MaxMinimalThinksPerFrame`, `ThinkBudgetMilliseconds`);
a refused tick keeps its due time and runs in a later frame, first in line.
Going to sleep calls `Stop()` on the tree; a park or a suspension does not.

Without glue code, subclass the runner component and put it next to the
agent:

```csharp
public sealed class ZombieBrain : SimulationBehaviourTreeRunner<ZombieBlackboard, string>
{
    protected override ZombieBlackboard CreateBlackboard() => new ZombieBlackboard(this);

    protected override INode<ZombieBlackboard, string> CreateTree(ZombieBlackboard blackboard)
        => ZombieTree.Build();
}
```

For an existing brain, create a `SimulationTreeTicker`, add it with
`agent.AddListener(ticker)` and call `ticker.Tick(Time.time)` from the
brain's Update. The behaviour tree module itself is unchanged.

## Budget tuning

| Setting | Effect | Start with |
| --- | --- | --- |
| `FullRadius` / `MinimalRadius` | distance rings | the camera's view radius plus a margin / about twice that |
| `DemotionDelaySeconds` | no flicker at a ring | 3 |
| `ObserverViewRadius` | "on some observer's screen" for co-op and bots; never demoted | the view radius, inside `FullRadius` |
| `MaxEvaluationsPerFrame` | round-robin: each agent is re-evaluated about once per solve interval; 0 = one full pass per interval | 24 |
| `MaxActivationChangesPerFrame` | parks, unparks and volume wakes that are not urgent; 0 = unlimited | 3 |
| `MaxThinksPerFrame` / `MaxMinimalThinksPerFrame` | tree ticks per frame across every managed agent | 8 / 2 |
| `ThinkBudgetMilliseconds` | stop ticking trees once the frame's ticks took this long (the first always runs) | 0 until measured on the target device |

`driver.Budget` exposes the counters (deferred thinks and activations, peak
thinks per frame) for measuring; the driver inspector shows them in Play
Mode.

## Allocations

A steady frame (round-robin evaluations with membership tracking, parked, dormant and capped agents,
active volumes with a deactivation clock, ticking trees) allocates nothing;
`SimulationLodDriverTests.SteadySteppingOfMixedTiersVolumesAndTreesAllocatesNothing`
measures it with the profiler `GC.Alloc` recorder and a positive control.
Allocations happen only on events: adopting agents, volume
activation (the wake queue is reused), registering listeners, and the first
use of each agent (component cache).

## Tests

EditMode `Easy.SimulationLod.Tests` (solver, budget, agent and listeners,
driver, volumes, behaviour tree, zero allocation) and PlayMode
`Easy.SimulationLod.PlayMode.Tests` (auto registration, Update-driven tiers,
real park callbacks, physics trigger activation, tree runner across a park,
collider containment).
