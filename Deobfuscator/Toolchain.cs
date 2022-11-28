using Deobfuscator.Tools;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Deobfuscator
{
    public class Toolchain
    {
        public readonly Tool EazDevirt;
        public readonly Tool de4dot;
        public readonly Tool OsuDecoder;
        public readonly Tool EazFixer;
        public readonly Tool ILSpy;

        private bool IsSetup = false;
        private readonly List<Tool> Tools;

        public Toolchain(ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger("Toolchain");

            EazDevirt = new EazDevirt(logger);
            de4dot = new de4dot(logger);
            OsuDecoder = new OsuDecoder(logger);
            EazFixer = new Eazfixer(logger);
            ILSpy = new ILSpy(logger);

            Tools = new()
            {
                EazDevirt,
                de4dot,
                OsuDecoder,
                EazFixer,
                ILSpy,
            };
        }

        public async Task Setup()
        {
            if (IsSetup) return;

            foreach (var tool in Tools)
            {
                using (tool.Logger.BeginScope(tool.SlnName))
                {
                    await tool.Clone();
                    await tool.Build();
                }
            }

            IsSetup = true;
        }
    }
}