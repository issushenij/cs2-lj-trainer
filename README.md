# 🎮 CS2 LongJump & Movement Trainer [v1.1.2]

**CS2 LJ Trainer** — это полноценная программа для анализа, тренировки стрейфов и отслеживания рекордов в Counter-Strike 2.  
Она работает **без инжектов в игру и без хуков** (100% безопасно, никакого бана VAC) — все данные считываются напрямую из локального лога CS2.

---

## ⚡ Быстрый старт: как подключить к CS2 (для чайников)

Чтобы программа автоматически ловила каждый ваш прыжок с любого сервера CS2 (Cybershoke, GOKZ, KZTimer и др.) и сразу рисовала графики и сохраняла рекорды:

1. Откройте **Steam** -> нажмите правой кнопкой мыши по **Counter-Strike 2** -> выберите **«Свойства...»** (Properties).
2. В первой же вкладке **«Общие»** (General) найдите поле **«Параметры запуска»** (Launch Options).
3. Вставьте туда следующую строчку:
   ```text
   -condebug +con_logfile console.log
   ```
4. **Готово!** Запустите игру и прыгайте на сервере. Приложение сразу подхватит прыжок, воспроизведет звук ранга, запишет рекорд и построит 2D-траекторию.

*(Если игра уже запущена, можно просто открыть консоль `~` в CS2 и прописать команду: `con_logfile console.log`).*

---

## 👤 Как привязать свой профиль Steam и Cybershoke

В правом верхнем углу программы нажмите на **квадратную кнопку профиля** (или клавишу **`P`**):

1. **Если вы играете на серверах Cybershoke:**
   * Нажмите на кнопку **«Авто-Синхр (Edge)»** в шапке профиля. Программа автоматически и безопасно прочитает ваш профиль из авторизованного браузера и загрузит ваши рекорды, пройденные карты и аватар.
2. **Ввод вручную:**
   * Нажмите на строку `STEAM: [НЕ ПРИВЯЗАН]` и вставьте ваш **SteamID64** (17-значный номер вашего Steam-аккаунта).
   * После этого все ваши рекорды будут привязаны к вашему профилю.

---

## 🧭 За что отвечает каждая вкладка и кнопка

### 1. Вкладка «1. Тренажёр» (Клавиша `1`)
* **Что делает:** Интерактивный тренажер ритма и техники стрейфов в реальном времени.
* **Как тренироваться:**
  1. Нажмите **`▶ СТАРТ`** (или **`Пробел`**). Курсор заблокируется, и вы сможете стрейфить мышкой влево-вправо.
  2. Программа замеряет длительность каждого стрейфа в миллисекундах, скорость мыши, плавность разворота и перекрытия клавиш A/D.
  3. Для выхода из режима тренировки нажмите **`Пробел`** или **`Escape`**.

### 2. Вкладка «2. Осциллограф» (Клавиша `3`)
* **Что делает:** График волн скорости и синхронизации стрейфов в реальном времени. Позволяет увидеть плавность движения мыши и микро-задержки при смене сторон.

### 3. Верхняя панель (Навигация и быстрые функции)
* **`▶ СТАРТ (Space)`** — запуск/пауза замера стрейфов.
* **Иконка Метронома** — включение звукового и визуального метронома с анимацией стрелки (помогает выработать мышечную память на 8, 9 или 10 стрейфов).
* **Иконка Динамика** — переключение звуков и регулировка громкости.
* **Кнопка «Гайд (F1)»** — подробное встроенное интерактивное руководство по всем элементам игры.
* **Кнопка «История»** — просмотр всех ваших недавних попыток и подробный разбор стрейфов.
* **Кнопка-шестеренка `⚙` (или `Tab`)** — меню всех настроек.
* **Кнопка Профиля (или `P`)** — ваш персональный профиль:
  * **Рекорды по направлениям:** `FWD` (прямо), `SW` (боком / sideways), `BW` (спиной / backwards).
  * **Рекорды блоков и дистанций** со всеми официальными рангами (*PERFECT, GODLIKE, OWNAGE, WRECKER*).
  * **Древо рекордов (История PB):** при клике на любую карточку прыжка открывается полная цепочка развития вашего рекорда с датами и деталями.

---

## ⚙️ Меню настроек (`⚙` или клавиша `Tab`)

* **1. Чувствительность мыши:** Настройка вашей сенсы или кнопка **«Авто-импорт sens из CS2»** (программа сама найдет вашу сенсу и m_yaw в конфигах игры).
* **2. Метроном и темп:** Выбор целевого количества стрейфов (6, 7, 8, 9, 10 или 12) и расчет времени на один стрейф.
* **3. Аудио:** Выбор звуковых пакетов, озвучка рекордов и громкость.
* **4. Цветовые темы и масштаб UI:** Темы *Cyber Neon*, *OLED Monochrome*, *Amber Sunset* и масштабирование интерфейса (100%, 125%, 150%, 175%).
* **5. Режим физики:** Выбор между физикой *CKZ / KZ* (100 AA, 276 Pre) и *Vanilla CS2* (12 AA, 250 Pre).
* **6. Поведение окна:** Сворачивание в трей при нажатии `[X]` (программа продолжает озвучивать рекорды в фоне во время игры).
* **7. Обновления программы:** Проверка новых версий и обновление в один клик без потери рекордов.

---

## 🔄 Как работает обновление приложения

* Программа **автоматически сохраняет** ваш файл рекордов `user_profile.json` и файл настроек `config.json`.
* При появлении обновления в верхнем баре загорается бейдж **`⚡ ОБНОВЛЕНИЕ`**.
* Вы можете обновиться прямо внутри приложения одной кнопкой `[ОБНОВИТЬ СЕЙЧАС]`, не скачивая архивы вручную.
* Автопроверку обновлений можно включить или выключить в Настройках `[Tab]`.

---

## 📥 Скачать приложение

1. Перейдите в раздел **[Releases](https://github.com/issushenij/cs2-lj-trainer/releases)**.
2. Скачайте файл **`LJTrainer.exe`**.
3. Запустите файл — программа готова к работе!

---

<a name="english-version"></a>
# English Version

# CS2 LongJump & Movement Trainer [v1.1.2]

**CS2 LJ Trainer** is a desktop analytics suite and cadence trainer for Counter-Strike 2 movement, strafes, and jump techniques.  
It runs **100% VAC-safe** without injecting code or reading process memory.

### Quick Setup
1. Open Steam -> Right-click **Counter-Strike 2** -> **Properties...**
2. In the **Launch Options** field under **General**, paste:
   ```text
   -condebug +con_logfile console.log
   ```
3. Launch CS2 and jump on any server. The trainer will automatically track your jumps, announce records, and visualize full 2D trajectories.

### Features
* **Direction Records:** Separate tracking for `FWD` (Forward), `SW` (Sideways), and `BW` (Backwards) jumps.
* **Block & Distance PBs:** Authentic KZ tiers (*IMPRESSIVE, PERFECT, GODLIKE, OWNAGE, WRECKER*).
* **Cadence Practice Lab:** Real-time mouse synchronization and A/D overlap analyzer with metronome pacing.
* **Cybershoke Integration:** Fast auto-sync of completed maps, tiers, and player avatar.
* **In-App Updater:** Automatic update notifications and one-click upgrades with full data preservation.

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
