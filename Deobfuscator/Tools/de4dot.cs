using CliWrap;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace Deobfuscator.Tools
{
    internal class de4dot : Tool
    {
        internal de4dot(ILogger logger) : base(
            logger: logger,
            name: "de4dot",
            relativeBuildPath: Path.Combine("Release", "de4dot.exe"),
            slnName: "de4dot",
            repoUrl: "https://github.com/lolPants/de4dot",
            targetCommit: "22bc21240115e8572c8a702288f2e26fd4a51ca8"
        )
        { }

        protected override async Task<(string, bool)> ExecuteInternal(Deobfuscator deobfuscator, string path, string fileName)
        {
            var log = deobfuscator.Logger;
            var results = await Cli.Wrap(BuildPath)
                .WithArguments($"--dont-rename --keep-types --preserve-tokens \"{path}\"")
                .WithValidation(CommandResultValidation.None)
                .ExecuteFallible();

            if (results?.StandardOutput is not null)
            {
                log.LogDebug("{stdout}", results.StandardOutput);
            }

            log.LogInformation("Cleaned assembly.");
            return ($"{fileName}-cleaned.dll", true);
        }
    }
}
