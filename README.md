# WCF Tool Control Service

Production-style WCF duplex service that pushes live chamber pressure and
temperature readings to connected operator clients, with a Windows Forms
operator console for recipes, pauses, alarms, and run logs.

## What it does

- A WCF duplex service (`net.tcp://localhost:9000/ToolControl`) streams live
  chamber readings to every subscribed operator client.
- The operator console starts recipes, pauses and resumes runs, acknowledges
  alarms, and views the run log.
- Each client gets its own bounded, thread-safe queue, so a slow or dead
  client can never block the control loop or other clients.
- The run state machine, limits, alarms, and log live in `ToolControl.Core`,
  which has no WCF or UI dependency and is fully unit tested.

## Architecture

- `src/ToolControl.Core` (.NET Standard 2.0): chamber state machine,
  limits, alarms, run log, per-client queue, telemetry broadcaster.
- `src/ToolControl.Contracts` (.NET Framework 4.8): WCF service and
  callback contracts, data contracts.
- `src/ToolControl.Service` (.NET Framework 4.8): duplex service, control
  loop (500 ms), per-client dispatcher tasks, simulated chamber hardware.
- `src/ToolControl.ServiceHost` (.NET Framework 4.8): self-hosted console host.
- `src/ToolControl.OperatorConsole` (.NET Framework 4.8, Windows Forms):
  operator console. Callbacks deliver live readings; alarms and the run log
  are also polled every 2 seconds so a missed callback cannot hide an alarm.
- `tests/ToolControl.Core.Tests` (.NET 8, xUnit): core state machine,
  alarm, and queue tests.

The simulated chamber is a development stand-in. In production, replace
`IChamberHardware` with a real hardware driver; no service or console code
changes are needed.

## Run it (Windows)

1. Build the solution in Visual Studio 2022 (or MSBuild) on Windows.
2. Start `ToolControl.ServiceHost`. It listens on
   `net.tcp://localhost:9000/ToolControl`.
3. Start `ToolControl.OperatorConsole`, pick a recipe, and click Start recipe.

## Tests

```powershell
dotnet test tests/ToolControl.Core.Tests/ToolControl.Core.Tests.csproj
```

The .NET Framework service, host, and console build on Windows CI
(`windows-latest`). The core tests also run on Linux CI.

## Safety note

This is a control-plane demo with simulated hardware. Do not connect it to
a real chamber without a hardware safety review, interlocks, and an
authenticated, encrypted transport (the demo binding uses no security so it
runs locally out of the box).
