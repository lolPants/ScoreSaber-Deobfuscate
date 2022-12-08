using CliWrap;
using CliWrap.Buffered;
using System.Text;
using System.Threading.Tasks;

namespace Deobfuscator
{
    internal static class FallibleCommand
    {
        public class Exception : System.Exception
        {
            public string StdOutput { get; private set; }
            public string StdError { get; private set; }

            public Exception(string command, string stdOutput, string stdError) : base(Output(command, stdOutput, stdError))
            {
                StdOutput = stdOutput;
                StdError = stdError;
            }

            private static string Output(string command, string output, string error)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"Error while running command: {command}").Append('\n');

                if (output != string.Empty)
                {
                    builder.Append($"\nstdout:\n{output}\n");
                }

                if (error != string.Empty)
                {
                    builder.Append($"\nstderr:\n{error}\n");
                }

                return builder.ToString();
            }
        }

        public static async Task<BufferedCommandResult> ExecuteFallible(this Command command)
        {
            var result = await command.ExecuteBufferedAsync();
            if (result.ExitCode != 0)
            {
                throw new FallibleCommand.Exception(command.ToString(), result.StandardOutput, result.StandardError);
            }

            return result;
        }
    }
}
