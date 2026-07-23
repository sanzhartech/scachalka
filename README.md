<div align="center">

<img src="assets/logo.png" width="140" alt="Scachalka logo" />

# Scachalka

**Скачивай легко** — a fast, native Windows GUI that puts the full power of **yt-dlp** and **FFmpeg** behind a modern interface. Paste links, drop a whole Apple Music library or a 10 000-row CSV — Scachalka handles the rest.

[![CI](https://github.com/sanzhartech/scachalka/actions/workflows/ci.yml/badge.svg)](../../actions/workflows/ci.yml)
![.NET 8](https://img.shields.io/badge/.NET-8.0-blueviolet)
![Platform](https://img.shields.io/badge/Windows-10%2F11-0078D6)
![License](https://img.shields.io/badge/license-MIT-green)

English · [Русский ниже](#по-русски)

</div>

---

Scachalka never reimplements downloader logic — it orchestrates your locally installed
yt-dlp and FFmpeg through process execution, with a clean, crash-resistant UI.

## Features

### Downloading
- Paste one or many URLs (space/newline separated), drag & drop, `Ctrl+Shift+V` paste-and-add
- URL validation up front — garbage never reaches the process layer
- **Playlists**: one toggle downloads whole playlists into `<playlist>/NN - title` subfolders
- Download queue with bounded parallelism (2 by default)
- Per-item progress bar, speed, ETA, size and stage (metadata → downloading → merging → converting)
- Cancel (kills the whole yt-dlp/ffmpeg process tree, cleans up `*.part` files) and retry

### 🎵 Import Apple Music Library

Export your library from the Apple Music app as plain text (`songs.txt`), click
**Import Apple Music Library** — and every song is found and downloaded fully automatically.
No manual searching, no pasting URLs.

- Parses Apple Music / iTunes plain-text exports (tab-separated, UTF-16/UTF-8, EN + RU
  headers) and simple `Artist - Title` lists
- Searches **both YouTube and YouTube Music** (`ytsearch10:` + music.youtube.com) through
  yt-dlp — metadata only, no wasted downloads
- **Intelligent matching** with a 0–100 confidence score: title similarity, artist evidence
  (official artist channel, `- Topic` auto-channels), track duration proximity, and
  official-source signals (Official Audio / Official Music Video / VEVO / verified channel)
- **Never downloads altered versions**: nightcore, slowed + reverb, sped up, bass boosted,
  remix, mashup, cover, karaoke, instrumental, live, AI versions and lyric videos are
  heavily penalized — accuracy over speed
- Skipped songs go to `failed_songs.txt` with the reason (`No match found` /
  `Confidence too low`); the run never stops on a single failure
- Live progress (`Searching 12 / 508`, `Matched: Official Audio · Confidence: 98%`) and a
  final summary with totals and elapsed time
- Scales to libraries with thousands of songs — searching and downloading run in parallel

### 📄 Bulk Import Links

Import hundreds or thousands of YouTube / YouTube Music links from a file — every valid
link goes straight into the existing download queue.

- **CSV** — URL column found by header (`youtube_music_url` / `youtube_url` / `url` /
  `link`), other columns ignored; quoted fields and `,` / `;` / tab delimiters handled
- **JSON** — arrays of URL strings, arrays of objects (same keys, case-insensitive), or a
  root object with arrays
- **TXT** — one URL per line
- Format detected automatically; the importer is pluggable, so new formats (M3U, Spotify
  exports, …) are one class away
- Validation against supported domains (`youtube.com`, `music.youtube.com`, `youtu.be`,
  `m.youtube.com`) with per-link reasons
- **Smart duplicate detection** — the same video via `youtu.be/…`, `watch?v=…` and
  YouTube Music counts as one; import order is preserved
- Rejected links saved to `failed_links.txt` (`Invalid URL` / `Unsupported domain` /
  `Missing URL` / `Duplicate`); summary shows imported / queued / duplicates / invalid
- Built for 10 000+ links: reading, parsing and validation run off the UI thread

### Formats
- Video: **MP4**, **MKV** with quality cap: Best / 4K / 1440p / 1080p / 720p / 480p / 360p
- Audio: **MP3, M4A, OPUS, FLAC, WAV** — best source audio, converted by FFmpeg

### Full yt-dlp power (Advanced panel)
- Embed metadata & chapters, thumbnail (cover art), subtitles with language selection
- **Cookies from your browser** (Chrome/Edge/Firefox/Brave/Opera/Vivaldi) for private,
  age-restricted or member-only videos — and if the browser is open and locks its cookie
  database, Scachalka automatically retries the download without cookies instead of failing
- **Extra yt-dlp arguments** field — anything yt-dlp can do (`--limit-rate`, `--proxy`,
  `--live-from-start`, SponsorBlock, …); appended last, so it overrides any default

### Comfort
- **Bilingual UI (English / Русский)** with instant runtime switch, auto-detected from the OS
- Dark and light theme (dark by default, runtime switch, dark title bar on Win11)
- Log window + session log files in `%APPDATA%\Scachalka\logs`
- Open destination folder / reveal downloaded file in Explorer
- Unicode-safe file names; friendly localized error messages
- Settings persisted as JSON (folder, format, quality, theme, language, all advanced options)

## Requirements

| Requirement | Notes |
|---|---|
| Windows 10/11 x64 | |
| [yt-dlp](https://github.com/yt-dlp/yt-dlp) | `winget install yt-dlp.yt-dlp` |
| [FFmpeg](https://ffmpeg.org) | `winget install Gyan.FFmpeg` (or bundled with the yt-dlp winget package) |
| .NET 8 SDK | **Build only** — the published exe is self-contained |

Scachalka finds the tools automatically: next to `Scachalka.exe` → `tools\` subfolder →
every `PATH` entry → WinGet links. If they are missing, a banner explains what to install;
press **Re-check tools** afterwards.

## Download & run (users)

1. Grab `Scachalka.exe` from the [latest release](../../releases/latest).
2. Install yt-dlp and FFmpeg (see Requirements).
3. Run `Scachalka.exe` — no installer, no admin rights. Settings live in `%APPDATA%\Scachalka\`.

### Importing an Apple Music library

1. In the Apple Music app (or iTunes): select your songs / playlist →
   **File → Library → Export Playlist…** → save as plain text (`songs.txt`).
2. In Scachalka: pick an audio format (MP3 by default), click **Import Apple Music Library**
   and choose the file.
3. Watch the progress. Anything that could not be matched confidently lands in
   `failed_songs.txt` next to your downloads.

### Bulk importing links

Prepare a CSV, TXT or JSON file with links (see Features for the recognized shapes),
click **Bulk Import Links**, choose the file — every valid, non-duplicate link is queued
and downloaded automatically. Rejected links land in `failed_links.txt` with reasons.

## Build from source (developers)

```powershell
git clone https://github.com/sanzhartech/scachalka.git
cd scachalka
dotnet publish -c Release -r win-x64 --self-contained true
```

Portable single-file executable:

```
src\YtDlpGui.App\bin\Release\net8.0-windows\win-x64\publish\Scachalka.exe
```

Run tests:

```powershell
dotnet test
```

## Releases (maintainers)

Tag a version and CI builds and publishes the portable exe automatically:

```bash
git tag v1.2.0
git push origin v1.2.0
```

The [release workflow](.github/workflows/release.yml) runs tests, publishes the
self-contained exe, and attaches `Scachalka.exe` + a zip to a GitHub Release.

## Logo / icon

The app icon lives at `assets/scachalka.ico` and is generated from `assets/logo.png` by
[`tools/generate-icon.ps1`](tools/generate-icon.ps1). To use your own artwork, replace
`assets/logo.png` with a square PNG and re-run:

```powershell
pwsh tools/generate-icon.ps1
```

Both the executable icon and the window icon pick it up on the next build.

## Architecture

Clean layered architecture with strictly one-directional dependencies:

```
YtDlpGui.App            composition root: DI wiring, app lifecycle
  ├─ YtDlpGui.UI            WPF views, view models (MVVM), themes, localization, converters
  │    └─ YtDlpGui.Application   queue coordinator, download executor, job model,
  │         │                    library import + bulk import orchestration
  │         └─ YtDlpGui.Core          URL validation, argument-builder strategies,
  │              │                    progress parser, error classifier, retry policy,
  │              │                    Apple Music parser, song match scorer,
  │              │                    link sources (CSV/JSON/TXT) + bulk link analyzer
  │              └─ YtDlpGui.Abstractions   interfaces, models, localization contract
  └─ YtDlpGui.Infrastructure  process runner (kill-tree), tool locator,
                              JSON settings, logging, string catalogs
```

- **Strategy pattern for formats** (`IArgumentBuilder`) — new formats add a class, not edits.
- **Strategy pattern for import formats** (`ILinkSource`) — CSV/JSON/TXT today; M3U or
  Spotify exports later without touching the import engine.
- **Pure matching logic** (`ISongMatchScorer`) — the Apple Music matcher is deterministic,
  I/O-free and covered by unit tests.
- **Machine-readable progress** via `--progress-template` with a resilient fallback parser.
- **Injection-proof process execution**: `ArgumentList` + `--` terminator, no shell.
- **Cancellation kills the entire process tree** and removes `*.part` leftovers.
- **UI never blocks**: async IO, throttled progress, collections mutated only on the UI thread.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the full module guide.

## Legal

Scachalka is a general-purpose GUI wrapper for yt-dlp. Use it only for content you are
allowed to download. It intentionally does **not** include features whose sole purpose is
to bypass site access controls (anti-bot, DRM) or to enable copyright infringement.

## License

[MIT](LICENSE)

---

<a name="по-русски"></a>

## По-русски

**Scachalka** — быстрый нативный GUI для Windows поверх **yt-dlp** и **FFmpeg**. Логику
скачивания не переписывает — запускает уже установленные инструменты через процессы.

### Возможности

**Загрузки:** много ссылок сразу, drag & drop, плейлисты, форматы MP4/MKV и
MP3/M4A/OPUS/FLAC/WAV, выбор качества, очередь с параллельностью, прогресс/скорость/ETA/этап,
отмена и повтор.

**🎵 Импорт библиотеки Apple Music:** экспортируйте библиотеку из Apple Music как текст
(`songs.txt`), нажмите одну кнопку — каждая песня будет найдена и скачана автоматически.
Поиск идёт и в YouTube, и в YouTube Music; умный алгоритм сопоставления сравнивает
название, исполнителя, длительность и официальность источника (трек YouTube Music,
Official Audio, клип, VEVO) и считает уверенность 0–100%. Изменённые версии — nightcore,
slowed + reverb, sped up, bass boosted, ремиксы, каверы, караоке, инструменталы, live,
AI-версии — не скачиваются никогда. Пропущенные песни попадают в `failed_songs.txt`
с причиной; в конце — итоговый отчёт.

**📄 Массовый импорт ссылок:** импортируйте сотни и тысячи ссылок из CSV (колонка ищется
по заголовку `youtube_music_url` / `youtube_url` / `url` / `link`), TXT (ссылка на строку)
или JSON (массивы строк или объектов). Формат определяется автоматически, ссылки
проверяются (только домены YouTube), дубликаты одного видео в разных формах ссылок
пропускаются, порядок сохраняется. Отклонённые — в `failed_links.txt` с причинами.
Рассчитано на 10 000+ ссылок без подвисания интерфейса.

**Продвинутое:** встраивание метаданных/обложки/субтитров, cookies из браузера (если
браузер открыт и блокирует базу cookies — загрузка автоматически повторяется без них,
а не падает), поле произвольных аргументов yt-dlp.

**Комфорт:** тёмная/светлая тема, **двуязычный интерфейс (RU/EN)** с мгновенным
переключением, журнал, Unicode-имена файлов, понятные сообщения об ошибках.

### Требования и запуск

Windows 10/11 x64; установленные yt-dlp и FFmpeg
(`winget install yt-dlp.yt-dlp Gyan.FFmpeg`). Для запуска готового exe .NET не нужен.

Скачайте `Scachalka.exe` из [релизов](../../releases/latest), установите yt-dlp и FFmpeg,
запустите. Установщик не требуется, настройки — в `%APPDATA%\Scachalka\`.

**Импорт Apple Music:** в приложении Apple Music — **Файл → Медиатека → Экспортировать
плейлист…** → сохранить как текст. В Scachalka — кнопка «Импорт библиотеки Apple Music».

### Сборка

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

**Свой логотип:** замените `assets/logo.png` своим квадратным PNG и выполните
`pwsh tools/generate-icon.ps1`.

**Важно:** программа предназначена только для контента, который вам разрешено скачивать.
Функций для обхода защит сайтов (anti-bot, DRM) в ней нет и не будет.
