# CS2 LongJump & Movement Trainer [DEMO v1.0.1]

Инструмент для анализа и тренировки мувмента, стрейфов и прыжков в Counter-Strike 2. Помогает разбирать ошибки в технике, следить за динамикой рекордов и тренировать ритм стрейфов.

> **Версия:** DEMO v1.0.1. Интерфейс оптимизирован под масштаб 150% (доступно переключение масштаба в настройках [Tab]).

[English version below](#english-version)

---

## Как это работает и откуда берутся данные

Приложение работает без внедрения в память игры и без использования читерских хуков, поэтому полностью безопасно (не влияет на VAC):

1. **Где берутся данные:** CS2 умеет записывать вывод игровой консоли в локальный текстовый файл `console.log` в папке игры (например, `steamapps/common/Counter-Strike Global Offensive/game/csgo/console.log`).
2. **Как программа их считывает:** Приложение в реальном времени мониторит обновления этого файла и парсит строки результатов прыжков (сообщения LJ-плагинов серверов Cybershoke, GOKZ, KZTimer и др.).
3. **Что делает с данными:** На лету рассчитывает траекторию полета по формулам физики Source 2, обновляет личные рекорды (PB), сохраняет историю прыжков в локальный профиль и строит графики баланса рук.
4. **Масштабирование:** По умолчанию установлен масштаб интерфейса **150%** для комфортного отображения на современных мониторах. Изменить масштаб (100%, 125%, 150%) можно в любой момент в меню настроек (`[Tab]` -> `Section 5: UI & Font Scaling`).

---

## Быстрая инструкция по подключению к CS2

Чтобы статистика и траектории прыжков начали отображаться в программе автоматически:

1. Откройте **Steam** -> правой кнопкой мыши по **Counter-Strike 2** -> **Свойства...** (Properties).
2. Во вкладке **Общие** (General) найдите строку **Параметры запуска** (Launch Options).
3. Вставьте туда команду:
```text
-condebug +con_logfile console.log
```
4. Запустите игру и прыгайте на сервере. Приложение сразу подхватит каждый ваш прыжок, нарисует траекторию и обновит статистику в профиле!

*(Альтернативный способ: можно прописать команду `con_logfile console.log` в консоли CS2 прямо во время игры).*

---

## Основные возможности

### Автоматический трекинг прыжков
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

### Работа в фоновом режиме (Трей)
* Приложение можно свернуть в системный трей Windows. Мониторинг консоли и звуки рекордов продолжают работать во время игры в полноэкранном режиме.

---

## Как запустить готовый файл

1. Перейдите на страницу [Releases](https://github.com/issushenij/cs2-lj-trainer/releases) и скачайте `LJTrainer.exe` (или zip-архив).
2. Запустите `LJTrainer.exe` — установка не требуется.

### Сборка из исходников (для разработчиков)
```powershell
git clone https://github.com/issushenij/cs2-lj-trainer.git
cd cs2-lj-trainer
dotnet run -c Release
```

---

<a name="english-version"></a>
# English Version

# CS2 LongJump & Movement Trainer [DEMO]

A dedicated desktop utility for analyzing and practicing movement, strafes, and jumps in Counter-Strike 2. Designed to help players diagnose mechanical mistakes, track progression, and build muscle memory for consistent strafe rhythm.

> **Release Status:** DEMO / Release Candidate. UI is scaled to 150% by default for crisp High-DPI display. You can change the scale at any time in the settings (`[Tab]` key).

---

## How It Works & Data Privacy

The application operates without injecting into game memory or hooking game processes, making it 100% VAC-safe:

1. **Data Source:** CS2 natively writes console logs to a local file named `console.log` inside your game directory (e.g., `steamapps/common/Counter-Strike Global Offensive/game/csgo/console.log`).
2. **How Data is Processed:** LJ Trainer monitors this local log file in real time and parses jump stats broadcasted by KZ and LJ server plugins (Cybershoke, GOKZ, KZTimer, etc.).
3. **What It Does:** The app calculates exact airpath flight curves using Source 2 physics formulas, logs your personal bests (PB), stores progression locally on your PC, and generates hand-balance diagnostics.
4. **UI Scaling:** Default interface scale is set to **150%**. You can customize scaling (100%, 125%, 150%) anytime via `[Tab]` -> `Section 5: UI & Font Scaling`.

---

## Quick CS2 Setup Guide

To enable automatic telemetry capturing:

1. Open **Steam** -> Right-click **Counter-Strike 2** -> **Properties...**
2. In the **General** tab, find **Launch Options**.
3. Add the following command:
```text
-condebug +con_logfile console.log
```
4. Start CS2 and jump on any KZ or LJ server. LJ Trainer will instantly capture your jumps, plot 2D flight paths, and update your PB stats!

*(Alternatively, you can run `con_logfile console.log` directly in the CS2 in-game console).*

---

## Key Features

* **Real-Time Jump Tracking:** Captures Distance, Pre-Speed, Max Speed, Sync %, Key Overlap (A+D conflict), Dead Air, and Bad Angles. Instant PB sound fanfare.
* **2D Trajectory Visualizer:** Top-down airpath recreation with Source 2 physics formulas. Side-by-side comparison between fresh jump, personal best, and session averages.
* **Hand Balance Diagnostics:** Detailed metrics for Left (A) vs. Right (D) strafes to eliminate sync drops and key-holding overlap latency.
* **Cybershoke KZ Integration:** Completed KZ maps browser, tier ratings, completion times, and leaderboard standings.
* **Cadence Metronome Lab:** Auditory and visual cues for muscle memory and rhythmic strafe switching.
* **System Tray Mode:** Runs quietly in the background with real-time sound cues while CS2 is full-screened.

---

## Download & Run

1. Go to [Releases](https://github.com/issushenij/cs2-lj-trainer/releases) and download `LJTrainer.exe` (or the zip package).
2. Launch `LJTrainer.exe` — no installation required.

### Build from Source
```powershell
git clone https://github.com/issushenij/cs2-lj-trainer.git
cd cs2-lj-trainer
dotnet run -c Release
```

---

## License
MIT License. Created by [issushenij](https://github.com/issushenij).
