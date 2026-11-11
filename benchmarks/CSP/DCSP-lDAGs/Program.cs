// Benchmark runs for the Delay Constrained Shortest Path (DCSP) on layered acyclic digraph (lDAG) (DCSPonlDAGs) problem.
// The program generates random lDAGs and measures the performance of various DCSP algorithms

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using CSP.DCSP_lDAGs;

var cli = CliOptions.Parse(args);
var configPath = cli.ConfigPath ?? "runsettings.json";

var config = JsonSerializer.Deserialize<RunParameters>(
    File.ReadAllText(configPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("Invalid run config.");

#if TEST
var verbose = cli.Verbose;
const bool check = true;
const bool writeOutput = false;
const bool measure = false;
#else
if (cli.Verbose)
{
    throw new ArgumentException("--verbose is only supported in TEST builds.");
}
const bool writeOutput = true;
const bool measure = true;
#endif

var seed = config.Seed ?? Random.Shared.Next();
config.Seed = seed;
if (writeOutput)
{
    var runId = Guid.NewGuid().ToString("N");
    var runDir = Path.Combine(config.OutputPath, runId);
    Directory.CreateDirectory(runDir);

    config.OutputPath = Path.EndsInDirectorySeparator(runDir) ? runDir : runDir + Path.DirectorySeparatorChar;

    var build = BuildInfo.Read();
    var metadata = new RunMetadata
    {
        RunId = runId,
        UtcTimestamp = DateTimeOffset.UtcNow,
        CommitSha = build.SourceRevisionId ?? "UNKNOWN",
        BuildConfiguration = build.Configuration ?? "UNKNOWN",
        TargetFramework = build.TargetFramework ?? "UNKNOWN",
        Runtime = RuntimeInformation.FrameworkDescription,
        OsDescription = RuntimeInformation.OSDescription,
        ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
        CpuLogicalCores = Environment.ProcessorCount,
        RamBytesAvailableToProcess = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes
    };

    File.WriteAllText(
        Path.Combine(runDir, "metadata.json"),
        JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
    
    File.WriteAllText(
        Path.Combine(runDir, "runparameters.json"),
        JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
}

Benchmark.Run(
    parameters: config, measure: measure, writeOutput: writeOutput
#if TEST
    , verbose: verbose, check: check
#endif
);

internal sealed class CliOptions
{
    public string? ConfigPath { get; private set; }
    public bool Verbose { get; private set; }

    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        foreach (var arg in args)
        {
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                o.ConfigPath ??= arg;
                continue;
            }

            if (arg == "--verbose") o.Verbose = true;
            else throw new ArgumentException($"Unknown argument: {arg}");
        }
        return o;
    }
}

internal sealed class RunMetadata
{
    public string RunId { get; set; } = "";
    public DateTimeOffset UtcTimestamp { get; set; }
    public string CommitSha { get; set; } = "";
    public string BuildConfiguration { get; set; } = "";
    public string TargetFramework { get; set; } = "";
    public string Runtime { get; set; } = "";
    public string OsDescription { get; set; } = "";
    public string ProcessArchitecture { get; set; } = "";
    public int CpuLogicalCores { get; set; }
    public long RamBytesAvailableToProcess { get; set; }
}

internal sealed class BuildInfo
{
    public string? Configuration { get; init; }
    public string? TargetFramework { get; init; }
    public string? SourceRevisionId { get; init; }

    public static BuildInfo Read()
    {
        var asm = Assembly.GetExecutingAssembly();
        var map = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase);

        map.TryGetValue("BuildConfiguration", out var cfg);
        map.TryGetValue("TargetFramework", out var tfm);
        map.TryGetValue("SourceRevisionId", out var sha);

        return new BuildInfo
        {
            Configuration = cfg,
            TargetFramework = tfm,
            SourceRevisionId = sha
        };
    }
}