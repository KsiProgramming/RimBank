using System.Diagnostics;

namespace RimBank.AppHost.Tests;

/// <summary>
/// Container tests always run in CI. On a developer machine without a reachable Docker engine they are skipped with
/// a clear reason instead of failing after a long timeout.
/// </summary>
internal static class DockerAvailability
{
    private static readonly Lazy<bool> _available = new(Probe);

    public static void SkipLocallyWhenUnavailable()
    {
        var inCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

        Assert.SkipUnless(inCi || _available.Value, "Docker is not reachable on this machine; container tests always run in CI.");
    }

    private static bool Probe()
    {
        try
        {
            using var docker = Process.Start(new ProcessStartInfo("docker", "info")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (docker is null)
            {
                return false;
            }

            if (!docker.WaitForExit(TimeSpan.FromSeconds(15)))
            {
                docker.Kill();
                return false;
            }

            return docker.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
