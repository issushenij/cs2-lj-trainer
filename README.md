# 🚀 CS2 LongJump & Movement Trainer (LJ Trainer)

> **Next-Gen Movement Analysis & Cadence Laboratory for Counter-Strike 2 (CS2) KZ / LongJump athletes.**  
> Built with **C# (.NET 9)**, **Raylib-cs**, and integrated with real-time CS2 console parsing and **Cybershoke KZ** profile telemetry.

---

## ⚡ Features & Capabilities

* **🎮 Real-Time CS2 Console Telemetry Watcher:**
  * Instant automatic detection and parsing of all in-game jumps (`Long Jump`, `Bunnyhop`, `Multi Bunnyhop`, `Weird Jump`, `Ladder Jump`, `Countjump`, `Drop Jump`, etc.).
  * Real-time calculation of Distance, Pre-Speed, Max Speed, Overlap (A+D key conflicts), Dead Air, Bad Angles, and Sync Efficiency.
  * Real-time PB (Personal Best) detection and celebratory audio notifications with custom fanfare.

* **🛰️ 2D Airpath & Strafe Trajectory Reconstruction:**
  * 100% physically accurate 2D top-down flight path simulation utilizing actual Source 2 movement physics.
  * Color-coded strafe arcs with gain/loss efficiency, overlap markers, and turn rate deviation angles.
  * Compare current jumps with Personal Best (PB), Previous Jumps, or Lifetime Averages.

* **📊 Deep Biomechanics & Analytics Dashboard:**
  * **A vs D Hand Balance Analysis:** Real-time diagnostics of left vs. right strafe sync %, angle speed, and key overlap latency.
  * **Interactive Metric Timeline Graphs:** Distance, Sync %, Pre-Speed, and Overlap progression curves with point inspection.
  * **Cybershoke KZ Leaderboard & Completed Maps Browser:** Search, sort (by points, top rank, best time, attempts), and inspect completed map records.

* **🎯 Cadence & Metronome Rhythm Lab:**
  * Train muscle memory and strafe rhythm with dynamic visual metronomes, auditory cadence ticks, and customizable Strafe-per-Jump rhythms.

* **🔔 System Tray & Background Audio Engine:**
  * Minimize to Windows System Tray upon window close.
  * Continue watching CS2 console and playing real-time sound cues while CS2 is full-screened.

---

## 🛠️ Technology Stack

* **Language:** C# 13 / .NET 9.0 Windows
* **Graphics Engine:** Raylib-cs (OpenGL accelerated, 144Hz+ ultra-low latency rendering)
* **Audio Engine:** Custom procedural synth sound generator + Raylib Audio
* **Telemetry Parser:** CS2 `conlog` stream reader with asynchronous regex parsing
* **Web Telemetry:** WebView2 integration for automated Cybershoke KZ profile synchronization

---

## 🚀 Getting Started

### Prerequisites
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (or later)
* Windows 10/11 x64

### Build & Run
```powershell
# Clone the repository
git clone https://github.com/issushenij/cs2-lj-trainer.git
cd cs2-lj-trainer

# Build and run the project
dotnet run -c Release
```

---

## ⚙️ CS2 Setup & Auto-Telemetry

To enable automatic telemetry capturing in CS2, add the following launch options to CS2 in Steam:
```text
-condebug +con_logfile console.log
```
Or in-game console:
```text
con_logfile console.log
```
*LJ Trainer automatically monitors your `console.log` in real time and updates your statistics on every jump.*

---

## 📜 License
MIT License. Created by [issushenij](https://github.com/issushenij).
