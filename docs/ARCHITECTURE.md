# Architecture

## Why this shape

The control loop is the one thing that must never wait. Everything else
is arranged around that rule:

- `ChamberController` owns run state only. No sockets, no UI, no timers.
- `TelemetryBroadcaster` fans readings out to bounded per-client queues.
  Publishing never blocks, even if a client stops reading.
- The WCF service runs one dispatcher task per client, pumping that
  client's queue into one-way callbacks. A faulted callback ends only that
  client's dispatcher.
- The console polls alarms and the run log every 2 seconds in addition to
  callbacks, so a missed callback cannot hide an alarm.

## Threading

- Controller state is guarded by a single lock. Public methods are safe
  to call from the control loop, service operations, and tests at once.
- Subscriber queues use `ConcurrentQueue` plus a semaphore, with a
  capacity of 1000. Overflow drops the oldest sample and counts it, rather
  than blocking the producer or growing without bound.
- The 500 ms control loop is a timer, not a thread per client.

## What is simulated

`SimulatedChamber` random-walks toward the active recipe targets. It exists
so the service, console, and demos run with no hardware. Replace
`IChamberHardware` with a real driver for production use, and add
authentication, encryption, and hardware interlocks before connecting to
a real chamber.
