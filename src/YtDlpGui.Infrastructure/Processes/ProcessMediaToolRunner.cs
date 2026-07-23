using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using YtDlpGui.Abstractions.Interfaces;
using YtDlpGui.Abstractions.Models;

namespace YtDlpGui.Infrastructure.Processes;

/// <summary>
/// Runs external tools fully asynchronously:
/// - arguments go through ArgumentList (no shell, no string concatenation, no injection),
/// - stdout/stderr are streamed line-by-line off the UI thread,
/// - cancellation kills the entire process tree (yt-dlp spawns ffmpeg children),
/// - output is forced to UTF-8 so Unicode titles survive on any system locale.
/// </summary>
public sealed class ProcessMediaToolRunner : IMediaToolRunner
{
    private const int StdErrTailCapacity = 60;
    private static readonly TimeSpan KillGracePeriod = TimeSpan.FromSeconds(10);

    public async Task<ToolResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        Action<string>? onOutputLine,
        Action<string>? onErrorLine,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppContext.BaseDirectory
        };

        // yt-dlp is a Python app: force its stdio to UTF-8 regardless of console code page.
        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stderrTail = new ConcurrentQueue<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                InvokeSafely(onOutputLine, e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            stderrTail.Enqueue(e.Data);
            while (stderrTail.Count > StdErrTailCapacity)
            {
                stderrTail.TryDequeue(out string? _);
            }

            InvokeSafely(onErrorLine, e.Data);
        };

        try
        {
            if (!process.Start())
            {
                return new ToolResult(-1, $"Failed to start process: {executablePath}", false);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return new ToolResult(-1, $"Failed to start process \"{executablePath}\": {ex.Message}", false);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var wasCanceled = false;
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            wasCanceled = true;
            KillProcessTree(process);
            await WaitForExitAfterKillAsync(process).ConfigureAwait(false);
        }

        // Synchronous WaitForExit after the async wait flushes pending async output events.
        try
        {
            process.WaitForExit();
        }
        catch (SystemException)
        {
            // Process already reaped — nothing left to flush.
        }

        int exitCode;
        try
        {
            exitCode = process.ExitCode;
        }
        catch (InvalidOperationException)
        {
            exitCode = -1;
        }

        return new ToolResult(exitCode, string.Join(Environment.NewLine, stderrTail), wasCanceled);
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException or AggregateException)
        {
            // Already exited or access lost — the OS will reap it.
        }
    }

    private static async Task WaitForExitAfterKillAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None)
                .WaitAsync(KillGracePeriod)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException)
        {
            // Give up waiting; the process object is disposed by the caller.
        }
    }

    private static void InvokeSafely(Action<string>? callback, string line)
    {
        if (callback is null)
        {
            return;
        }

        try
        {
            callback(line);
        }
        catch
        {
            // A faulty subscriber must never take down the output pump thread.
        }
    }
}
