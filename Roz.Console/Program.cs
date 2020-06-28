using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Roz.Console
{
    class Program
    {
        static string watcherName = $"Roz";
        static string fileName = $"{watcherName}_Trace_{GetTimeCode(DateTime.UtcNow)}.json";
        static bool isShutdown = false;

        static void Main(string[] args)
        {
            if (!args.Any())
            {
                System.Console.WriteLine("Usage: Roz [commandline]");
            }
            else
            {
                AlwaysWatching(args);
            }
        }

        private static void AlwaysWatching(string[] args)
        {
            System.Console.WriteLine($"{watcherName} Logging processes to {fileName}");
            
            using (var writer = new Roz.Core.Logger.Writer(fileName))
            {
                writer.AddMeta("logFileName", fileName);
                writer.AddMeta("logMachine", Environment.MachineName); 
                using (var watcher = new Roz.Core.Monitor.Processes(writer))
                {

                    System.Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) => Shutdown(writer, watcher);
                    AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs exceptArgs) => Shutdown(writer, watcher);

                    try
                    {                        
                        writer.AddMeta("logStart", DateTime.UtcNow.ToString("o"));
                        Task.Run( () => watcher.Watch(watcherName) );
                        var process = CreateProcess(args);
                        process.Exited += (object sender, EventArgs eventArgs) => Shutdown(writer, watcher);
                        process.Start();
                        process.WaitForExit();
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        System.Console.WriteLine($"{watcherName} An error occurred : {e}");
                        throw;
                    }
                    finally
                    {
                        if (!isShutdown) Shutdown(writer, watcher);
                    }
                }
            }
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
            System.Console.WriteLine($"Shutting down watcher...");            
            Task.Delay(5000).Wait();
            watcher?.StopWatching();
            System.Console.WriteLine($"Shutting down writer...");
            Task.Delay(5000).Wait();
            writer?.Close();            
            System.Console.WriteLine($"{watcherName} Logged processes to {fileName}");
            System.Console.WriteLine($"Open this file in chrome://tracing/");
        }

        static string GetTimeCode(DateTime t) => t.ToString("yyyyMMddhhmmssfff");

    }
}
