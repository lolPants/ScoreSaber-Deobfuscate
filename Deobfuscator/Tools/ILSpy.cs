using CliWrap;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Deobfuscator.Tools
{
    internal class ILSpy : Tool
    {
        internal ILSpy(ILogger logger) : base(
            logger: logger,
            path: Path.Combine(Environment.CurrentDirectory, "ILSpy"),
            buildPath: Path.Combine(Environment.CurrentDirectory, "ILSpy", "ICSharpCode.Decompiler.Console", "bin", "Release", "net6.0", "ilspycmd.exe"),
            slnName: "ILSpy",
            repoUrl: "https://github.com/icsharpcode/ILSpy",
            targetCommit: "7685d15fadbb2c126d47f19209336bbbeb72a792",
            restoreNugetPackages: true
        )
        { }

        protected override async Task<string> ExecuteInternal(Deobfuscator deobfuscator, string path, string fileName)
        {
            var log = deobfuscator.Logger;

            var outputDir = Path.Combine(deobfuscator.WorkingDirectory, "decomp");
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
            Directory.CreateDirectory(outputDir);

            var results = await Cli.Wrap(BuildPath)
                .WithArguments($"--project --outputdir \"{outputDir}\" \"{path}\"")
                .ExecuteFallible();

            if (results?.StandardOutput is not null)
            {
                log.LogDebug("{stdout}", results.StandardOutput);
            }

            log.LogInformation("Decompiled assembly.");
            return "decomp";
        }
    }
}
