# CS2 LongJump & Movement Trainer

CS2 LJ Trainer — программа для анализа техники стрейфов, тренировки ритма и отслеживания рекордов в Counter-Strike 2.  
Приложение работает без внедрения в память игры и без хуков (полностью безопасно для VAC) — все данные считываются из стандартного консольного лога CS2.

---

## Быстрый старт: подключение к CS2

Чтобы программа автоматически считывала каждый ваш прыжок с серверов CS2 (Cybershoke, GOKZ, KZTimer и др.), строила траектории и сохраняла статистику:

1. Откройте **Steam** -> нажмите правой кнопкой мыши по **Counter-Strike 2** -> выберите **«Свойства...»** (Properties).
2. Во вкладке **«Общие»** (General) найдите поле **«Параметры запуска»** (Launch Options).
3. Вставьте следующую строку:
   ```text
   -condebug +con_logfile console.log
   ```
4. Запустите игру и прыгайте на сервере. Приложение автоматически обработает прыжок, воспроизведет звук ранга, обновит рекорд и отобразит траекторию.

*(Также команду можно включить прямо в игре через консоль: `con_logfile console.log`).*

---

## Привязка профиля Steam и Cybershoke

В правом верхнем углу интерфейса нажмите на кнопку профиля (или клавишу **P**):

1. **Автоматическая синхронизация с Cybershoke:**
   * Нажмите кнопку **«Авто-Синхр (Edge)»** в шапке профиля. Программа загрузит ваши рекорды, пройденные карты и аватар.
2. **Ввод вручную:**
   * Нажмите на поле Steam ID и укажите ваш **SteamID64** (17-значный номер аккаунта).

---

## Основные разделы интерфейса

### 1. Тренажёр (Клавиша 1)
* Интерактивная тренировка ритма стрейфов в реальном времени.
* Нажмите **СТАРТ** (или **Пробел**), чтобы заблокировать курсор и начать стрейфить мышью влево и вправо.
* Программа измеряет длительность каждого стрейфа, скорость движения мыши, синхронизацию и перекрытие клавиш A/D.
* Пауза / выход из тренировки — клавиша **Пробел** или **Escape**.

### 2. Осциллограф (Клавиша 3)
* График скорости и синхронизации стрейфов в динамике. Помогает оценить плавность движений и задержки при смене направления.

### 3. Верхняя панель управления
* **СТАРТ / Пауза (Space)** — запуск и остановка тренировочного замера.
* **Метроном** — переключение звукового и визуального метронома для выработки стабильного тайминга стрейфов.
* **Динамик** — настройка громкости и включение/выключение звуковых оповещений.
* **Гайд (F1)** — встроенная справочная информация.
* **История** — журнал недавних попыток с подробной раскладкой по стрейфам.
* **Настройки (Шестеренка / Tab)** — параметры программы.
* **Профиль игрока (P)**:
  * Рекорды по направлениям: прямо (FWD), боком (SW), спиной (BW).
  * Рекорды блоков и дистанций с градацией рангов (Impressive, Perfect, Godlike, Ownage, Wrecker).
  * История личных рекордов (PB): при нажатии на карточку открывается хронология прогресса.

---

## Настройки (Клавиша Tab)

* **Чувствительность мыши:** ручная настройка или авто-импорт значений `sensitivity` и `m_yaw` из файлов конфигурации CS2.
* **Метроном и темп:** выбор целевого количества стрейфов (от 6 до 12) и расчет времени на стрейф.
* **Аудио:** выбор звуковых пресетов, озвучка рекордов и громкость.
* **Темы оформления и масштаб UI:** выбор цветовой схемы и масштабирование интерфейса (100%, 125%, 150%, 175%).
* **Режим физики:** переключение между физикой CKZ / KZ (100 AA, 276 Pre) и Vanilla CS2 (12 AA, 250 Pre).
* **Поведение окна:** сворачивание в трей при закрытии (программа продолжает озвучивать рекорды в фоне).
* **Обновления:** ручная проверка и возможность отключения автоматической проверки при старте.

---

## Обновление приложения

* Приложение сохраняет ваши рекорды (`user_profile.json`) и настройки (`config.json`) при обновлениях.
* При выходе новой версии в верхней панели появляется уведомление о доступном обновлении.
* Обновление устанавливается автоматически прямо через интерфейс программы.

---

<a name="english-version"></a>
# English Version

# CS2 LongJump & Movement Trainer

CS2 LJ Trainer is a desktop analytics tool and cadence trainer for Counter-Strike 2 movement, strafes, and jumping mechanics.  
It operates 100% VAC-safe without injecting into game memory.

### Quick Setup
1. Open Steam -> Right-click **Counter-Strike 2** -> **Properties...**
2. In **Launch Options** (General tab), paste:
   ```text
   -condebug +con_logfile console.log
   ```
3. Start the game and jump on any server. The application will track your jumps, announce records, and plot flight curves.

### Key Features
* **Directional Records:** Separate tracking for Forward (FWD), Sideways (SW), and Backwards (BW) jumps.
* **Block & Distance Records:** Standard KZ tier classifications (Impressive, Perfect, Godlike, Ownage, Wrecker).
* **Cadence Practice Lab:** Real-time mouse synchronization and key overlap analysis with metronome pacing.
* **Cybershoke Integration:** Map completion stats, tier tracking, and profile sync.
* **In-App Updater:** Automatic update notifications and one-click upgrades with full profile preservation.

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
