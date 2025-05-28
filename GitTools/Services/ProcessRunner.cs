using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace GitTools.Services;

/// <inheritdoc/>
[ExcludeFromCodeCoverage]
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc/>
    public int Run
    (
        ProcessStartInfo startInfo,
        DataReceivedEventHandler? outputDataReceived,
        DataReceivedEventHandler? errorDataReceived
    )
    {
        using var process = new Process();
        process.StartInfo = startInfo;

        if (outputDataReceived != null)
            process.OutputDataReceived += outputDataReceived;

        if (errorDataReceived != null)
            process.ErrorDataReceived += errorDataReceived;

        process.Start();

        if (outputDataReceived != null)
            process.BeginOutputReadLine();

        if (errorDataReceived != null)
            process.BeginErrorReadLine();

        process.WaitForExit();

        return process.ExitCode;
    }    /// <inheritdoc/>
    public async Task<int> RunAsync
    (
        ProcessStartInfo startInfo,
        DataReceivedEventHandler? outputDataReceived = null,
        DataReceivedEventHandler? errorDataReceived = null
    )
    {
        using var process = new Process();
        process.StartInfo = startInfo;

        if (outputDataReceived != null)
            process.OutputDataReceived += outputDataReceived;

        if (errorDataReceived != null)
            process.ErrorDataReceived += errorDataReceived;

        process.Start();

        if (outputDataReceived != null)
            process.BeginOutputReadLine();

        if (errorDataReceived != null)
            process.BeginErrorReadLine();

        await process.WaitForExitAsync().ConfigureAwait(false);

        return process.ExitCode;
    }
}
