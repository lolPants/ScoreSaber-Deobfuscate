using Deobfuscator.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Deobfuscator
{
    public class Deobfuscator
    {
        private string InputPath { get; }
        private string InputDir { get; }
        private List<string> DependencyDirectories { get; }
        internal string Password { get; }

        /// <summary>
        /// Working directory to perform deobfuscation
        /// </summary>
        internal string WorkingDirectory { get; }

        internal ILogger Logger { get; }

        public Deobfuscator(ILoggerFactory loggerFactory, string inputPath, string password, List<string>? dependencyDirectories = null)
        {
            InputPath = inputPath;
            DependencyDirectories = dependencyDirectories ?? new List<string>();
            Password = password;

            var inputDir = Path.GetDirectoryName(inputPath);
            if (inputDir is null)
            {
                throw new NullReferenceException(nameof(inputDir));
            }

            InputDir = inputDir;

            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            WorkingDirectory = Path.Combine(inputDir, $"{fileName}-deobfuscation");

            Logger = loggerFactory.CreateLogger(fileName);
        }

        public class InputNotExistsException : Exception
        {
            public InputNotExistsException(string path) : base(path) { }
        }

        public class DependencyDirNotExistsException : Exception
        {
            public DependencyDirNotExistsException(string path) : base(path) { }
        }

        public async Task<bool> Deobfuscate(Toolchain toolchain, bool dryRun = false, bool decompile = false)
        {
            var run = async () =>
            {
                toolchain.Ensure();

                if (!File.Exists(InputPath))
                {
                    throw new InputNotExistsException(InputPath);
                }

                foreach (var dependencyDir in DependencyDirectories)
                {
                    if (!Directory.Exists(dependencyDir))
                    {
                        throw new DependencyDirNotExistsException(dependencyDir);
                    }
                }

                var wd = WorkingDirectory;
                if (Directory.Exists(wd)) Directory.Delete(wd, true);
                Directory.CreateDirectory(wd);

                foreach (var dependencyDir in DependencyDirectories)
                {
                    var source = new DirectoryInfo(dependencyDir);
                    source.DeepCopy(wd);
                }

                string fileName = Path.GetFileName(InputPath);
                string input = Path.Combine(wd, fileName);
                File.Copy(InputPath, input);

                try
                {
                    var (cleaned, _) = await toolchain.de4dot.Execute(this, input);
                    (string, bool) devirt;

                    try
                    {
                        devirt = await toolchain.EazDevirt.Execute(this, cleaned);
                    }
                    catch (EazDevirt.FailException)
                    {
                        devirt = await toolchain.EazDevirt_2018_1.Execute(this, cleaned);
                    }

                    var (eazfixed, _) = await toolchain.EazFixer.Execute(this, devirt.Item1);
                    var (decoded, _) = await toolchain.OsuDecoder.Execute(this, eazfixed);
                    (string, bool)? decompiledDir = decompile ? await toolchain.ILSpy.Execute(this, decoded) : null;

                    string nameWithoutExtension = Path.GetFileNameWithoutExtension(InputPath);
                    string projectName = $"{nameWithoutExtension}-deobfuscated";
                    string dllName = $"{projectName}.dll";

                    string outputDllPath = Path.Combine(wd, decoded);
                    string finalDllPath = Path.Combine(InputDir, dllName);

                    if (!dryRun)
                    {
                        if (decompiledDir is not null)
                        {
                            string dir = decompiledDir.Value.Item1;
                            string outputProjectPath = Path.Combine(wd, dir);
                            string finalProjectPath = Path.Combine(InputDir, projectName);

                            var source = new DirectoryInfo(outputProjectPath);
                            source.DeepCopy(finalProjectPath);
                        }
                        else
                        {
                            File.Copy(outputDllPath, finalDllPath, true);
                        }
                    }

                    return devirt.Item2;
                }
                catch (Tool.OutputNotExistsException)
                {
                    // Pass
                    return false;
                }
                catch (FallibleCommand.Exception ex)
                {
                    Logger.LogCritical("{ex}", ex.Message);
                    return false;
                }
                finally
                {
                    Directory.Delete(WorkingDirectory, true);
                }
            };

            var ok = await run();

            if (ok)
            {
                Logger.LogInformation("Successfully deobfuscated!");
            }
            else
            {
                Logger.LogError("Deobfuscation unsuccessful.");
            }

            return ok;
        }
    }
}
