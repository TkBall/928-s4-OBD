# Porsche 928 K-Line Diagnostic Tool

A Windows desktop application for reading fault codes, live sensor data, and performing actuator tests on the six ECUs fitted to a Porsche 928 S4 via the factory K-Line diagnostic bus.

There's a single release in the release folder marked DANGEROUS, because this is completely untested. https://github.com/TkBall/928-s4-OBD/releases/tag/dangerous

---

## Contents

1. [Hardware requirements](#1-hardware-requirements)
2. [Running the application](#2-running-the-application)
3. [Connecting to the car](#3-connecting-to-the-car)
4. [Application overview](#4-application-overview)
5. [LH Jetronic 2.3 — fuel injection (0x11)](#5-lh-jetronic-23--fuel-injection-0x11)
6. [EZK ignition — mapped ignition (0x12)](#6-ezk-ignition--mapped-ignition-0x12)
7. [PSD slip differential (0x28)](#7-psd-slip-differential-0x28)
8. [RDK tyre pressure monitor (0x30)](#8-rdk-tyre-pressure-monitor-0x30)
9. [Airbag ECU (0x40)](#9-airbag-ecu-0x40)
10. [Alarm system (0x45)](#10-alarm-system-0x45)
11. [Digital instrument cluster self-test](#11-digital-instrument-cluster-self-test)
12. [Fault code reference](#12-fault-code-reference)
13. [Protocol notes](#13-protocol-notes)
14. [Troubleshooting](#14-troubleshooting)

---

## 1. Hardware requirements

### Adapter

An **FTDI FT232R** USB-to-serial adapter wired as a K-Line interface. The FT232R TX line drives the bus through a 510 Ω current-limiting resistor; a BC547 (or equivalent) NPN transistor performs the necessary voltage translation and signal inversion so the bus idles high at 12 V.

The driver must use **RTS = inactive (high)** and **DTR = inactive (high)**. The application sets both automatically — do not override them with FTDI utilities.

### Car-side connector

The 928 OBD socket is a **19-pin Porsche diagnostic connector** located in the **passenger side footwell**, mounted on the inner sill. Pin numbering:

| Pin | Signal |
|-----|--------|
| 1   | Chassis ground |
| 3   | K-Line (bidirectional single-wire bus) |
| 6   | Digital dash test trigger (ground to activate) |
| 19  | Ignition-switched +12 V |

Connect the adapter TX/RX (combined through the interface circuit) to pin 3, and the adapter GND to pin 1.

### Computer

Windows 10 or Windows 11. No .NET runtime installation is required — the runtime is bundled in the exe. The FTDI CDM driver (v2.12 or later) must be installed; Windows Update usually provides it automatically when the adapter is first plugged in.

---

## 2. Running the application

The compiled application lives in the `publish` folder alongside this README. No installation and no .NET SDK are required — the runtime is bundled.

```
publish\Porsche928Diagnostics.exe
```

Double-click the exe, or run it from a terminal. Keep all files in the `publish` folder together — the WPF graphics DLLs alongside the exe are required and cannot be moved away from it.

The application requires no installation and writes no registry entries. To uninstall, delete the folder.

---

## 3. Connecting to the car

1. Plug the FTDI adapter into a USB port. Note the COM port number assigned by Windows (visible in Device Manager under *Ports (COM & LPT)*).
2. Connect the adapter to the car's 19-pin diagnostic connector.
3. Turn the ignition **ON** (position II — dashboard illuminated, engine not running unless a test requires it).
4. Launch the application.
5. In the connection toolbar at the top of the window, select the correct COM port from the drop-down list. Click **Refresh** if the adapter was plugged in after the application started.
6. Click **Connect**. The status bar will confirm the port has opened at 10,400 baud.
7. The module tabs become active. Each tab communicates with one ECU — clicking **Initialize** on a tab performs the ISO 9141-2 slow-initialisation handshake for that ECU individually.

### Initialisation sequence (what happens behind Connect)

The application opens the serial port but does not yet talk to any ECU. Each module tab has its own **Initialize** button that triggers the full ISO 9141-2 slow-init for that ECU:

1. The TX line is held low for 200 ms per bit to send the ECU address at 5 baud (bit-banged via the serial break signal).
2. The ECU responds with a 0x55 sync byte, then two keyword bytes.
3. The application acknowledges by sending the complement of the second keyword byte.
4. The ECU confirms with the complement of its own address.
5. Communication switches to 10,400 baud 8N1 for the rest of the session.

If any step fails the status bar shows the reason (sync timeout, wrong sync byte, bad confirm byte). Check the wiring and try again.

---

## 4. Application overview

The window is divided into a **connection toolbar** at the top and a **tabbed module area** below.

### Connection toolbar

| Control | Purpose |
|---------|---------|
| Port drop-down | Select the COM port for the FTDI adapter |
| Refresh | Re-scan available COM ports |
| Connect | Open the selected port |
| Disconnect | Close the port and end all sessions |
| Status bar | Shows the last operation result; red text indicates an error |
| Progress bar | Visible while an operation is in progress |

The module tabs are disabled until the port is connected. Each module maintains its own session state independently — you can initialise LH, read some values, then initialise EZK without affecting the LH session.

### Colour coding

Throughout the application, indicator tiles use a consistent colour scheme:

- **Green** — value is healthy / switch is closed / signal is active
- **Red** — value is out of range / switch is open / fault is present

---

## 5. LH Jetronic 2.3 — fuel injection (0x11)

### Initialise

Click **Initialize LH Session**. The ECU ID (chip code and Bosch part number) is shown in the header on success.

### Fault codes

| Button | Action |
|--------|--------|
| Read Fault Codes | Retrieves all stored DTCs and displays them in the list |
| Clear Fault Codes | Erases all stored fault codes from the ECU's non-volatile RAM |

Fault codes are displayed with their description where known. See [Fault code reference](#12-fault-code-reference).

### Actual values

**Read Static Values** — engine not required to be running. Returns:

| Value | Notes |
|-------|-------|
| Battery voltage | Raw byte × 0.065 V. Normal range: 13.8–14.4 V with engine running |
| Engine temperature | NTC thermistor lookup. Operating temperature: 80–95 °C |
| EZK On signal | Whether the ignition ECU is sending its synchronisation signal to LH |

**Read Active Values** — engine must be running. Returns:

| Value | Notes |
|-------|-------|
| MAF voltage | 0–5 V from the Bosch hot-wire air mass meter |
| Lambda voltage | 0–1000 mV from the heated oxygen sensor. Oscillates 200–800 mV at idle in closed loop |

### Input signals

**Read Input Signals** — returns live switch states:

| Signal | Description |
|--------|-------------|
| Throttle Idle | Idle contact on throttle potentiometer — closed at rest, opens above ~3% throttle |
| WOT | Wide open throttle contact — closed at full load only |
| A/C Demand | Air conditioning compressor clutch demand signal from the climate control unit |

### Drive link tests

These tests command the ECU to activate an output actuator directly, bypassing normal control strategy. Engine should be at idle or off.

| Actuator | Normal operation | Test use |
|----------|-----------------|----------|
| Tank Vent Valve | Opens during deceleration to purge evaporative canister | Verify audible click and correct vacuum routing |
| Resonance Flap | Opens above ~4500 RPM to connect both intake plenums | Verify mechanical operation; listen for flap movement |
| Fuel Injectors | Normally pulsed by injection calculation | Activates all injectors simultaneously — use briefly; flooding risk |
| Idle Stabilizer Valve | Bypasses air past the throttle plate to maintain idle | Click **Activate** and listen for valve opening; idle speed should rise |

Click **Activate** to start, then **Stop** to end. Do not leave actuators running unattended.

### System Adaptation Program (SAP)

The SAP recalibrates the base injection pulse width to correct for long-term fuel trim drift. It monitors closed-loop lambda correction and writes the adapted value to non-volatile RAM.

**Prerequisites before running SAP:**
- Engine fully warm (coolant temperature 80 °C or above)
- Idle smooth with no misfires
- Lambda sensor at operating temperature (at least 2 minutes after start)
- Air conditioning **off**
- All electrical loads **off** (rear demister, headlights, fan blower)

Click **Run SAP**. The adaptation runs for up to 90 seconds, polling every 5 seconds. The status bar reports progress. Click **Stop** to abort early. On completion the adapted value is stored and the engine should idle more smoothly.

---

## 6. EZK ignition — mapped ignition (0x12)

### Sensor data

Click **Read Sensor Data**. The following live values are returned:

| Value | Notes |
|-------|-------|
| Engine RPM | Derived from crankshaft position sensor |
| Load % | Throttle/MAF-derived load signal, 0–100 % |
| Engine temperature | Should match LH reading within ±5 °C |
| Transmission coding | EZK reads the transmission type from its coding plug: Automatic or Manual |

### Knock registration

Click **Read Knock Counts**. Eight values are returned — one per cylinder (the 928 S4 is a V8, cylinders 1–8). Each value represents the number of knock events registered by the piezoelectric sensor for that cylinder since the last clear or ignition cycle.

Non-zero values indicate retarded timing on that cylinder. Values above ~10 per cylinder warrant investigation (fuel quality, sensor wiring, mechanical condition).

---

## 7. PSD slip differential (0x28)

### Overview

The PSD (Porsche Slip Differential) is an electronically controlled limited-slip differential. A hydraulic pump maintains clutch pack pressure. The ECU monitors wheel speeds and modulates a solenoid valve.

> **Model year note:** The electronic PSD was standard from MY1990 only. Pre-1990 928 S4s (most 1989 cars, unless factory-optioned) have the mechanical ZF 40% limited-slip differential instead, which has no electronic module to query. If this ECU doesn't respond on a pre-1990 car, that's expected — not a fault.

### Hydraulic bleed procedure

The bleed procedure is required after any hydraulic work on the PSD circuit (pump replacement, hose work, fluid change).

**Safety critical: read completely before starting.**

**Required equipment:**
- Vehicle on a lift or stands with all four wheels off the ground
- Clear plastic bleed tube fitted to the PSD slave cylinder bleeder screw (located on the differential casing, accessible from underneath)
- A container for used fluid
- Porsche ATF (Dexron II or equivalent) to top up

**Procedure:**

1. Ensure the PSD bleeder screw is accessible and the tube and container are in place.
2. Top up the fluid reservoir.
3. Initialise the PSD session (click **Initialize PSD Session**).
4. Click **Start 60s Bleed Sequence**.
   - The application activates the hydraulic pump and solenoid valve via drive link command.
   - The status bar counts down from 60 seconds.
5. Crack the bleeder screw **after** the status bar confirms the pump is active.
6. Allow fluid to flow until the stream is clear with no air bubbles.
7. Tighten the bleeder screw **before** the 60-second timer expires, or click **Emergency Stop**.
   - The application sends a stop command in its `finally` block regardless of whether the timer completes normally or is cancelled. The pump will stop. If communication is lost the ECU will time out and stop the actuator independently.
8. Check and top up the fluid level.
9. Repeat if air bubbles were observed.

**Do not click Emergency Stop mid-bleed unless the screw is already tight.** Air will be drawn back into the circuit.

---

## 8. RDK tyre pressure monitor (0x30)

The RDK system (German: *Reifendruckkontrolle*) uses pressure switches inside each wheel that send an RF signal to a receiver under the car. The ECU decodes the signals.

> **Model year note:** RDK was an optional extra on the 1989 928 S4 and only became standard from MY1990. If your car wasn't ordered with the option, this ECU will not respond — that's expected, not a fault.

### Pressure switch states

Click **Read Pressure Sensors**. Four tiles show the state of each wheel:

| Tile colour | Meaning |
|-------------|---------|
| Green | Pressure switch closed — tyre pressure above threshold |
| Red | Pressure switch open — pressure below threshold, or wheel removed |

The pressure switches operate at approximately 1.8 bar. They indicate loss, not exact pressure; use a gauge for accurate readings.

The **HF Receiver** indicator shows whether the RF receiver module is active and responding.

### Interpreting results

All four green at rest with the car on its wheels is the normal state. Red on a wheel that is fitted and inflated suggests:

- Pressure genuinely low — check with a gauge
- Wheel valve not seating correctly in the RDK transmitter cap
- RDK transmitter battery depleted (the transmitters are sealed units)
- RF receiver fault (check fuse and receiver mounting under the car)

---

## 9. Airbag ECU (0x40)

**Warning: do not perform electrical work on the airbag circuit while the ECU has power or the capacitor has charge. The downtime clock on this tab tells you how much charge remains.**

> **Model year note:** A driver airbag was optional on 1989 928 S4s and did not become standard until MY1990 — if your car wasn't ordered with the option, this ECU will not respond. From MY1990, UK/RHD cars received a driver-side airbag only; no passenger airbag was ever fitted to RHD cars due to dashboard packaging, so the passenger field will read empty/false by design.

### Airbag data

Click **Read Airbag Data**.

**Downtime clock** — The ECU contains an internal capacitor that retains enough energy to fire the airbags for several minutes after the main battery is disconnected. The downtime clock shows how long the ECU has been powered from its capacitor alone (i.e. how long since battery power was removed). Wait until this reads more than the capacitor's rated holdover time (typically 15–30 minutes for a 30-year-old unit) before touching the wiring.

**Crash event recorded** — If this reads `True`, the ECU's non-volatile memory contains deployment data. This does **not** necessarily mean the airbags fired (the ECU records hard decelerations even if the threshold to fire was not met), but it must be investigated before clearing.

**Deployment status** — Three indicators show whether the driver airbag, passenger airbag, or seatbelt pretensioner has been fired. Green = not fired. Red = fired. A fired unit requires replacement before the circuit can function again; clearing fault codes does not re-arm a physically deployed device.

### Fault codes

Read and clear as with other modules. Common codes include squib resistance out of range, low battery voltage, and sensor faults. Persistent codes after clearing indicate a wiring fault or failed component.

---

## 10. Alarm system (0x45)

> **Feature note:** The factory alarm (option code M533) was optional equipment across the whole 928 production run, not tied to a specific model year. If your car wasn't ordered with it, this ECU will not respond — that's expected, not a fault.

### Input switch states

Click **Read Input Signals**. Four tiles show the status of perimeter switches:

| Switch | Location |
|--------|---------|
| Engine lid | Bonnet (front) microswitch |
| Luggage lid | Boot (rear) microswitch |
| Glove box | Interior glove compartment microswitch |
| Interior motion | Ultrasonic interior motion sensor |

Green = switch closed (lid shut / motion sensor idle). Red = switch open (lid ajar or sensor triggered). Useful for diagnosing false alarm triggers — leave the ignition on with the alarm ECU session active and open each panel in turn to identify a faulty switch.

### Country coding

The alarm ECU contains a country-specific coding byte that determines the alarm sequence behaviour (duration of siren, indicator flash pattern, etc.) according to local regulations.

The current code read from the ECU is shown next to **Current code**. To change it:

1. Select the desired country code from the drop-down (DE, GB, US, FR, IT, JP).
2. Click **Write Country Code**.
3. The new code is confirmed in the status bar.

### Drive link tests

| Test | Duration | Purpose |
|------|---------|---------|
| Test Horn | 2 seconds | Verify horn relay and horn |
| Flash Indicators | 3 seconds | Verify indicator relay wiring |

The application activates the output for the stated duration and stops automatically.

---

## 11. Digital instrument cluster self-test

The 928 digital instrument cluster (the "Board Computer" LCD panel) does not communicate over the K-Line. It has its own internal self-test mode triggered by a hardware input. This tab guides you through the procedure step by step.

### Equipment needed

- A short jumper wire (a paperclip works)
- Access to the 19-pin diagnostic connector

### Procedure

Click **Start Guided Sequence**. The application walks through 10 timed steps:

| Step | Action | Duration |
|------|--------|---------|
| 1 | Turn ignition ON (do not start engine) | 5 s |
| 2 | Ground Pin 6 of the 19-pin connector to Pin 1 (chassis ground) using the jumper wire. Hold for 3 seconds. | 3 s |
| 3 | Release the jumper. The cluster enters segment-check mode — all LCD segments illuminate simultaneously. | 4 s |
| 4–9 | The cluster cycles through six sensor readings (see below) | 5–8 s each |
| 10 | Self-test complete. The cluster returns to normal display. Turn ignition off. | 3 s |

### Readings available in self-test mode

| Reading | Normal range | Notes |
|---------|-------------|-------|
| Oil pressure | 2.0–4.5 bar at idle | Low oil pressure is a critical fault — do not start engine |
| Oil level | Min line ≈ 4.0 L low | Top up if below minimum |
| Brake fluid level | OK | LOW = check reservoir and inspect for leaks |
| Engine temperature | 80–95 °C when warm | Should agree with LH ECU reading |
| Coolant level | OK | LOW = check expansion tank |
| **Toothed belt tension** | **OK** | **FAULT = stop using vehicle immediately. Inspect tensioner roller.** |

The toothed belt tension reading is the most safety-critical item. The 928 S4's 32-valve DOHC V8 drives four camshafts from a single toothed rubber belt; a failed tensioner allows belt slip or breakage with catastrophic engine damage. If this reads FAULT, the car must not be driven until the tensioner and belt are inspected and replaced as necessary.

### Notes on the checklist panel

The right side of the tab shows a static checklist of all six readings. Use it to record what the cluster displays during the sequence — the self-test mode does not transmit data digitally, so you must read the LCD directly and note down the values.

---

## 12. Fault code reference

The following codes are pre-loaded with descriptions. Any code not in this list will display its raw hex bytes.

| Code bytes | Description |
|-----------|-------------|
| 0x11 0x11 | Air mass meter signal missing or out of range |
| 0x11 0x12 | Air mass meter signal implausible |
| 0x12 0x11 | Throttle position sensor — idle contact fault |
| 0x12 0x12 | Throttle position sensor — WOT contact fault |
| 0x13 0x11 | Engine temperature sensor (NTC) — signal missing |
| 0x13 0x12 | Engine temperature sensor — signal implausible |
| 0x14 0x11 | Lambda sensor — signal missing |
| 0x14 0x12 | Lambda sensor — no switching activity (control loop open) |
| 0x21 0x11 | Ignition timing — knock detected bank 1 |
| 0x21 0x12 | Ignition timing — knock detected bank 2 |
| 0x22 0x11 | Crankshaft position sensor — signal missing |
| 0x23 0x11 | Camshaft sensor — signal missing |
| 0x31 0x11 | Idle stabilizer valve — electrical fault |
| 0x32 0x11 | Tank vent valve — electrical fault |
| 0x33 0x11 | Resonance flap — electrical fault |
| 0x41 0x11 | EZK/LH synchronisation signal fault |
| 0x44 0x11 | Vehicle speed signal missing |

---

## 13. Protocol notes

This section is for reference and troubleshooting; it is not required for normal use.

### Physical layer

- Single-wire K-Line bus, idle high at approximately 12 V (battery voltage)
- Logic levels: high = 12 V (mark), low = 0 V (space)
- The FTDI FT232R TX line is active-low at 3.3 V/5 V TTL — the hardware interface circuit inverts and level-shifts this to the K-Line convention

### Initialisation (ISO 9141-2 slow init)

1. The tester sends the ECU address byte at **5 baud** using BreakState toggling (200 ms per bit, LSB first, active-low convention)
2. Wait W1 = 60 ms
3. ECU responds with sync byte **0x55** at 10,400 baud
4. ECU sends keyword byte 1, then keyword byte 2
5. Wait W4 = 25 ms
6. Tester sends **~keyword2** (bitwise NOT of keyword byte 2)
7. ECU sends **~ecuAddress** as final confirmation
8. Normal 10,400 baud communication begins

### Frame format

```
[Format] [Target] [Source=0xF1] [SID] [Data...] [Checksum]
```

- **Format** = `0x80 | (SID + data length)`. Maximum payload: 63 bytes.
- **Target** = ECU K-Line address
- **Source** = `0xF1` (standardised tester address)
- **Checksum** = sum of all preceding bytes, modulo 256

### ECU addresses

| ECU | Address |
|-----|---------|
| LH Jetronic 2.3 | 0x11 |
| EZK ignition | 0x12 |
| PSD differential | 0x28 |
| RDK tyre pressure | 0x30 |
| Airbag | 0x40 |
| Alarm | 0x45 |

---

## 14. Troubleshooting

### "Timeout: No sync byte received from ECU"

The ECU did not respond to the 5-baud address. Check:

- Ignition is ON (position II)
- Adapter wiring is correct — TX to K-Line (pin 3) through the interface resistor, GND to chassis (pin 1)
- The correct COM port is selected
- The FTDI CDM driver is installed (check Device Manager)
- The ECU fuse is intact (see wiring diagram for fuse box location)
- There is no short to ground on the K-Line (measure resistance from pin 3 to pin 1 with ignition off — should be several kilohms)

### "ECU error: Negative response (SID 0x7F)"

The ECU rejected the request. This usually means the session was lost (timeout between frames) or the ECU does not support the requested service ID. Re-initialise the session and try again. If it persists on every request, the ECU may have a hardware fault or the K-Line bus has interference.

### "Unexpected error: Access to port COM3 is denied"

Another application (or a previous crashed instance) holds the COM port open. Check Task Manager for other instances of the application. Some FTDI utilities also hold the port open — close them.

### Drive link tests do not produce any response

Ensure the relevant fuse and relay for the actuator circuit are intact. The ECU confirms receipt of the command but cannot confirm the actuator physically moved — a failed relay or broken wire will produce no visible result at the actuator even though the command was sent successfully.

### PSD bleed pump does not appear to run

Check PSD hydraulic pump fuse (see car wiring diagram). Verify the PSD ECU session initialised successfully before attempting the bleed. The pump is only activated after the drive link command is accepted — if the session was not established the button press will fail with an ECU error.

### Digital dash self-test does not start

Verify the ignition is ON before grounding pin 6. The ground must be held for at least 2.5 seconds — a brief touch is not enough. The cluster microcontroller is the trigger point, not the diagnostic ECU, so K-Line connection state has no bearing on this test.
