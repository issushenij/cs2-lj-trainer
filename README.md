# CS2 LongJump & Movement Trainer

Инструмент для анализа и тренировки мувмента, стрейфов и прыжков в Counter-Strike 2. Помогает разбирать ошибки в технике, следить за динамикой рекордов и тренировать ритм стрейфов.

[English version below](#english-version)

---

## Возможности

### Автоматический трекинг прыжков из CS2
Программа в реальном времени читает консоль игры и выводит подробную статистику по каждому прыжку:
* Поддержка всех основных типов: Long Jump, Bunnyhop, Multi Bunnyhop, Weird Jump, Ladder Jump, Countjump, Drop Jump и др.
* Расчет дистанции, скорости на отрыве (Pre-Speed), максимальной скорости в воздухе, процента синхронизации, зажатий клавиш A+D (Overlap), мертвых зон (Dead Air) и потерь углов (Bad Angles).
* Определение личных рекордов (PB) и звуковые оповещения при их обновлении.

### 2D-визуализация траектории полета
* Построение реального пути движения игрока сверху с учетом физики Source 2.
* Разделение стрейфов по цветам, отображение эффективности набора скорости и зон зажатия двух клавиш одновременно.
* Возможность наглядно сравнить свежий прыжок со своим рекордом (PB) или со средней траекторией.

### Аналитика техники и баланс стрейфов
* Отдельный анализ левых (A) и правых (D) стрейфов: помогает понять, в какую сторону стрейфы получаются слабее или где чаще зажимаются обе клавиши.
* Интерактивные графики прогресса по дистанции, синхронизации и скорости.
* История изменения рекордов и позиций в рейтинге.

### Интеграция с профилем Cybershoke KZ
* Просмотр пройденных карт KZ, времени прохождения, набранных очков и позиции в общем топе.
* Удобный поиск и сортировка по сложности карт (Tier), очкам и времени.

### Метроном и тренировка ритма (Cadence Lab)
* Звуковой и визуальный метроном для выработки стабильного темпа и тайминга переключения клавиш A и D.

### Работа в фоновом режиме
* Приложение можно свернуть в системный трей Windows. Мониторинг консоли и звуки рекордов продолжают работать во время игры в полноэкранном режиме.

---

## Как запустить

### Требования
* Windows 10 или 11 (64-bit)
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) или новее

### Сборка из исходников
```powershell
git clone https://github.com/issushenij/cs2-lj-trainer.git
cd cs2-lj-trainer
dotnet run -c Release
```

---

## Настройка CS2 для авто-телеметрии

Чтобы программа могла автоматически считывать прыжки, включите логирование консоли в CS2.

Добавьте в параметры запуска игры в Steam:
```text
-condebug +con_logfile console.log
```
Или пропишите команду прямо в консоли игры:
```text
con_logfile console.log
```

---

<a name="english-version"></a>
# English Version

# CS2 LongJump & Movement Trainer

A dedicated desktop utility for analyzing and practicing movement, strafes, and jumps in Counter-Strike 2. Designed to help players diagnose mechanical mistakes, track progression, and build muscle memory for consistent strafe rhythm.

---

## Key Features

### Real-Time In-Game Jump Tracking
Automatically monitors your CS2 console log and breaks down every jump:
* Supports all jump modes: Long Jump, Bunnyhop, Multi Bunnyhop, Weird Jump, Ladder Jump, Countjump, Drop Jump, and more.
* Captures Distance, Pre-Speed, Max Speed, Sync %, Key Overlap (A+D conflict), Dead Air, and Bad Angles.
* Instant PB detection with custom audio fanfare.

### 2D Flight Trajectory Visualizer
* Reconstructs the exact top-down airpath based on Source 2 movement physics.
* Color-coded strafe segments highlighting speed gains, losses, and overlap zones.
* Direct side-by-side comparison between your latest jump, personal best, and session averages.

### Biomechanics & Hand Balance Diagnostics
* Compares left (A) vs. right (D) strafe metrics to spot hand imbalance, sync drops, or key hold latency.
* Interactive progression timeline graphs for Distance, Sync, Pre-Speed, and Overlap.
* PB milestone timeline tracking your personal improvement history.

### Cybershoke KZ Integration
* Displays completed KZ maps, completion times, earned points, and leaderboard standings.
* Built-in search and filtering by difficulty tiers, rank, and score.

### Cadence Rhythm Lab
* Visual and audio metronome tailored for training muscle memory and steady strafe switching intervals.

### Background System Tray Mode
* Minimizes directly to the Windows System Tray on close. Continues parsing CS2 jumps and playing audio cues while the game is running.

---

## Getting Started

### Requirements
* Windows 10 or 11 (64-bit)
* [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or newer

### Building from Source
```powershell
git clone https://github.com/issushenij/cs2-lj-trainer.git
cd cs2-lj-trainer
dotnet run -c Release
```

---

## CS2 Launch Setup

To enable real-time jump detection, enable console logging in CS2.

Add this to your Steam launch options for CS2:
```text
-condebug +con_logfile console.log
```
Or type directly into the CS2 console:
```text
con_logfile console.log
```

---

## License
MIT License. Created by [issushenij](https://github.com/issushenij).
