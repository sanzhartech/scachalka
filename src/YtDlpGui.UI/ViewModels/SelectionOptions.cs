using YtDlpGui.Abstractions.Enums;

namespace YtDlpGui.UI.ViewModels;

/// <summary>Combo-box item for the output format selector.</summary>
public sealed record FormatOption(MediaFormat Value, string Label)
{
    public override string ToString() => Label;

    public static IReadOnlyList<FormatOption> All { get; } =
        [.. Enum.GetValues<MediaFormat>().Select(f => new FormatOption(f, f.ToDisplay()))];
}

/// <summary>Combo-box item for the video or audio quality selector.</summary>
public sealed record QualityOption(VideoQuality Value, string Label)
{
    public override string ToString() => Label;

    public static IReadOnlyList<QualityOption> VideoQualities { get; } =
        [.. Enum.GetValues<VideoQuality>().Select(q => new QualityOption(q, q.ToDisplay()))];

    public static IReadOnlyList<QualityOption> AudioQualities { get; } =
    [
        new(VideoQuality.Best, "320 kbps"),
        new(VideoQuality.Q1440, "256 kbps"),
        new(VideoQuality.Q1080, "192 kbps"),
        new(VideoQuality.Q720, "128 kbps")
    ];

    public static IReadOnlyList<QualityOption> All => VideoQualities;
}

/// <summary>Combo-box item for the UI language selector. Labels are endonyms (never translated).</summary>
public sealed record LanguageOption(string Code, string Label)
{
    public override string ToString() => Label;

    public static IReadOnlyList<LanguageOption> All { get; } =
    [
        new("en", "English"),
        new("ru", "Русский")
    ];

    public static LanguageOption ForCode(string code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
