using CommandLine;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Deobfuscator.Bulk
{
    internal class Program
    {
        internal class Options
        {
            [Option('f', "versions", Required = true, HelpText = "Path to versions.tsv file")]
            public string VersionsFile { get; set; } = null!;

            [Option('V', "version", Required = false, HelpText = "Only run on a single version")]
            public string? Version { get; set; }

            [Option('p', "password", Required = true, HelpText = "Symbol password")]
            public string Password { get; set; } = null!;

            [Option('D', "decompile", Required = false, HelpText = "Output a decompiled directory instead of a DLL")]
            public bool Decompile { get; set; } = false;

            [Option('d', "dry-run", Required = false, HelpText = "Don't output a deobfuscated DLL")]
            public bool DryRun { get; set; } = false;

            [Option('P', "parallelism", Required = false, HelpText = "Max number of versions to deobfuscate concurrently")]
            public int Parallelism { get; set; } = 1;

            [Option('r', "report", Required = false, HelpText = "Optional report file path")]
            public string? ReportFile { get; set; }

            [Option('v', "verbose", Required = false, HelpText = "Verbose logging")]
            public bool Verbose { get; set; } = false;
        }

        static async Task Main(string[] args)
        {
            var options = Parser.Default.ParseArguments<Options>(args).Value;
            if (options is null) return;

            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddFilter(null, options.Verbose ? LogLevel.Trace : LogLevel.Information)
                .AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "HH:mm:ss ";
                })
            );

            var log = loggerFactory.CreateLogger("Program");

            if (!File.Exists(options.VersionsFile))
            {
                log.LogCritical("Versions file does not exist!");
                Environment.Exit(1);
            }

            string? root = Path.GetDirectoryName(options.VersionsFile);
            if (root is null)
            {
                throw new NullReferenceException(nameof(root));
            }

            string versionsString = await File.ReadAllTextAsync(options.VersionsFile);
            List<VersionInfo> versions = versionsString.Split('\n')
                .Skip(1)
                .Where(line => line != string.Empty)
                .Select(line => new VersionInfo(root, line))
                .ToList();

            var toolchain = new Toolchain(loggerFactory);
            await toolchain.Setup();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = options.Parallelism,
            };

            ConcurrentDictionary<string, string> report = new();
            await Parallel.ForEachAsync(versions, parallelOptions, async (version, _) =>
            {
                if (options.Version is not null && version.Version != options.Version)
                {
                    return;
                }

                var path = version.Filepath;
                if (path is null)
                {
                    log.LogWarning("{version} does not exist!", version);
                    return;
                }

                List<string?> dependencies = new()
                {
                    version.GameAssembliesDep,
                    version.LibsDep,
                    version.PluginsDep,
                };

                var deps = dependencies.Where(x => x is not null).Cast<string>().ToList();
                var deobfuscator = new Deobfuscator(loggerFactory, path, options.Password, deps);

                var success = await deobfuscator.Deobfuscate(toolchain, dryRun: options.DryRun, decompile: options.Decompile);
                report.TryAdd(version.Filename, success ? "success" : "failure");
            });

            if (options.ReportFile is not null)
            {
                using var writer = new StreamWriter(options.ReportFile);
                await writer.WriteLineAsync("version\tstatus");

                foreach (var (key, value) in report)
                {
                    await writer.WriteLineAsync($"{key}\t{value}");
                }

                await writer.FlushAsync();
            }
        }
    }
}
