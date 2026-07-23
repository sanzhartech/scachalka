using YtDlpGui.Abstractions.Localization;

namespace YtDlpGui.Infrastructure.Localization;

/// <summary>Russian string catalog.</summary>
internal static partial class StringCatalog
{
    public static IReadOnlyDictionary<string, string> Russian { get; } = new Dictionary<string, string>
    {
        [LocKeys.ToolsChecking] = "Поиск yt-dlp и FFmpeg…",
        [LocKeys.ToolsRecheck] = "Проверить снова",
        [LocKeys.ToolsReadyFormat] = "yt-dlp {0} · {1}",
        [LocKeys.ToolsMissingFormat] = "Не найдено: {0}. Установите (например, \"winget install yt-dlp.yt-dlp Gyan.FFmpeg\") или положите .exe рядом со Scachalka.exe.",
        [LocKeys.ToolsMissingJoin] = " и ",
        [LocKeys.ToolsCheckFailed] = "Проверка инструментов не удалась — смотрите журнал.",

        [LocKeys.UrlHint] = "Ссылки на видео/аудио — одна или несколько, через пробел или с новой строки. Работает и перетаскивание.",
        [LocKeys.BtnAddToQueue] = "В очередь",
        [LocKeys.BtnAddTooltip] = "Ctrl+Enter",
        [LocKeys.BtnPasteAdd] = "Вставить и добавить",
        [LocKeys.BtnPasteTooltip] = "Ctrl+Shift+V — добавить ссылки прямо из буфера обмена",

        [LocKeys.LabelFormat] = "Формат",
        [LocKeys.LabelQuality] = "Качество",
        [LocKeys.LabelOutputFolder] = "Папка сохранения",
        [LocKeys.LabelLanguage] = "Язык",
        [LocKeys.BtnBrowse] = "Обзор…",
        [LocKeys.BtnOpenFolder] = "Открыть папку",
        [LocKeys.CheckDarkTheme] = "Тёмная тема",

        [LocKeys.AdvHeader] = "Расширенные параметры yt-dlp",
        [LocKeys.AdvPlaylists] = "Скачивать плейлисты",
        [LocKeys.AdvPlaylistsTip] = "Если ссылка — плейлист, скачать все элементы в подпапку плейлиста",
        [LocKeys.AdvMetadata] = "Встраивать метаданные и главы",
        [LocKeys.AdvThumbnail] = "Встраивать обложку",
        [LocKeys.AdvThumbnailTip] = "Обложка для видео и аудио (не поддерживается для WAV)",
        [LocKeys.AdvSubtitles] = "Встраивать субтитры",
        [LocKeys.AdvSubtitlesTip] = "Только для видеоформатов",
        [LocKeys.AdvSubLangsTip] = "Языки субтитров для --sub-langs, напр. en,ru или all (пусто = по умолчанию yt-dlp)",
        [LocKeys.AdvCookies] = "Cookies из браузера",
        [LocKeys.AdvCookiesTip] = "Использовать cookies браузера для приватных, возрастных или платных видео",
        [LocKeys.AdvExtraArgs] = "Доп. аргументы yt-dlp (применяются к каждой загрузке, переопределяют любые настройки)",
        [LocKeys.AdvExtraArgsTip] = "Вся мощь yt-dlp: напр. --limit-rate 2M --live-from-start --proxy socks5://127.0.0.1:1080",
        [LocKeys.CookieNone] = "Нет",

        [LocKeys.QueueTitle] = "Очередь загрузок",
        [LocKeys.QueueClearFinished] = "Очистить завершённые",
        [LocKeys.JobCancel] = "Отмена",
        [LocKeys.JobRetry] = "Повторить",
        [LocKeys.JobShowInFolder] = "Показать в папке",
        [LocKeys.JobEtaPrefix] = "Осталось",
        [LocKeys.LogHeader] = "Журнал",

        [LocKeys.StageQueued] = "В очереди",
        [LocKeys.StageQueuedRetryFormat] = "В очереди (попытка №{0})",
        [LocKeys.StageResolving] = "Получение сведений…",
        [LocKeys.StageDownloading] = "Загрузка",
        [LocKeys.StageMerging] = "Объединение",
        [LocKeys.StageConverting] = "Конвертация",
        [LocKeys.StageCompleted] = "Готово",
        [LocKeys.StageFailed] = "Ошибка",
        [LocKeys.StageCanceled] = "Отменено",

        [LocKeys.StatusReady] = "Готово.",
        [LocKeys.StatusNoValidUrls] = "Не найдено корректных ссылок — ожидаются http(s)-ссылки.",
        [LocKeys.StatusToolsMissing] = "Нельзя начать: не найдены yt-dlp/FFmpeg. Установите их и нажмите «Проверить снова».",
        [LocKeys.StatusClipboardNoText] = "В буфере обмена нет текста.",
        [LocKeys.StatusCannotRetry] = "Эту загрузку нельзя повторить (некорректная ссылка).",
        [LocKeys.StatusSummaryFormat] = "{0} активны · {1} в очереди · {2} готово · {3} с ошибкой · {4} отменено",

        [LocKeys.NoteCanceled] = "Отменено пользователем.",
        [LocKeys.NoteCanceledBeforeStart] = "Отменено до начала.",
        [LocKeys.NoteAlreadyExisted] = "Файл уже существовал — загрузка пропущена.",

        [LocKeys.ErrorToolMissing] = "Нужный инструмент не найден. Установите yt-dlp и FFmpeg, затем нажмите «Проверить снова».",
        [LocKeys.ErrorInvalidUrl] = "Эта ссылка не поддерживается или не является корректной медиа-ссылкой.",
        [LocKeys.ErrorNetwork] = "Проблема сети при загрузке. Проверьте подключение и нажмите «Повторить».",
        [LocKeys.ErrorDiskFull] = "Недостаточно свободного места в папке сохранения.",
        [LocKeys.ErrorPermission] = "Нет прав на запись в папку сохранения. Выберите другую папку.",
        [LocKeys.ErrorFileExists] = "Файл уже существует в папке сохранения.",
        [LocKeys.ErrorInterrupted] = "Загрузка была прервана до завершения.",
        [LocKeys.ErrorUnknown] = "Загрузка неожиданно завершилась ошибкой. Подробности в журнале."
    };
}
