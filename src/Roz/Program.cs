using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;

namespace Roz
{
    class Program
    {
        static bool isShutdown = false;
        static string fileName;

        static int Main(string[] args)
        {
            return Parser.Default
                .ParseArguments<Options>(args)
                .MapResult(
                    options => AlwaysWatching(options),
                    _ => 1);               
        }

        private static int AlwaysWatching(Options args)
        {
            fileName = String.IsNullOrEmpty(args.Outfile) ? $"Roz_Trace_{GetTimeCode(DateTime.UtcNow)}.json" : args.Outfile;


            var command = args.Command.ToArray();
            var filters = args.Watched.ToArray();

            if (filters.Any())
            {
                Console.WriteLine($"Roz: Watching for processes '{string.Join(' ',filters)}'");
            }
            else
            {
                Console.WriteLine($"Roz: Watching all processes");
            }

            if (command.Any())
            {
                Console.WriteLine($"Roz: Executing '{string.Join(' ',command)}'");
            }
            else
            {
                Console.WriteLine($"Roz: No command specified, waiting for Ctrl-C");
            }

            Console.WriteLine($"Roz: Logging to {fileName}");

            
            using (var writer = new Roz.Core.Logger.Writer(fileName))
            {
                writer.AddMeta("logFileName", fileName);
                writer.AddMeta("logMachine", Environment.MachineName); 
                using (var watcher = new Roz.Core.Monitor.Processes(writer))
                {

                    Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) => Shutdown(writer, watcher);
                    AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs exceptArgs) => Shutdown(writer, watcher);

                    try
                    {                        
                        writer.AddMeta("logStart", DateTime.UtcNow.ToString("o"));
                        Task.Run( () => watcher.Watch("Roz", filters) );

                        if (command.Any())
                        {
                            var process = CreateProcess(command);
                            process.Exited += (object sender, EventArgs eventArgs) => Shutdown(writer, watcher);
                            process.Start();
                            process.WaitForExit();
                        }
                        else
                        {
                            Task.Delay(-1).GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        Console.WriteLine($"Roz: An error occurred : {e}");
                        throw;
                    }
                    finally
                    {
                        if (!isShutdown) Shutdown(writer, watcher);
                    }
                }
            }
            return 0;
        }

        private static System.Diagnostics.Process CreateProcess(string[] args)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo()
            {
                FileName = args[0],
                UseShellExecute = false,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal              
            };
            foreach (var arg in args.Skip(1)) startInfo.ArgumentList.Add(arg);
            foreach (var env in Environment.GetEnvironmentVariables().Keys)
            {
                var envKey = env as string;
                startInfo.Environment[envKey] = Environment.GetEnvironmentVariable(envKey);
            }
            var process = new System.Diagnostics.Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            return process;
        }

        static void Shutdown(Core.Logger.Writer writer, Core.Monitor.Processes watcher)
        {
            isShutdown = true;
            writer.AddMeta("logEnd", DateTime.UtcNow.ToString("o"));
            Console.WriteLine($"Shutting down watcher...");            
            Task.Delay(5000).Wait();
            watcher?.StopWatching();
            Console.WriteLine($"Shutting down writer...");
            Task.Delay(5000).Wait();
            writer?.Close();            
            Console.WriteLine($"Roz: Logged processes to {fileName}");
            Console.WriteLine($"Open this file in chrome://tracing/");
        }

        static string GetTimeCode(DateTime t) => t.ToString("yyyyMMddhhmmssfff");

    }
}
