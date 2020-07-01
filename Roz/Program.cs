using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Roz
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
                Console.WriteLine("Usage: Roz [commandline arg1 arg2...]");
            }
            else
            {
                AlwaysWatching(args);
            }
        }

        private static void AlwaysWatching(string[] args)
        {
            int processID = System.Diagnostics.Process.GetCurrentProcess().Id;
            Console.WriteLine($"{watcherName} Logging processes to {fileName}");
            
            using (var writer = new Roz.Core.Logger.Writer(fileName))
            {            
                writer.AddMeta("Roz.fileName", fileName);
                writer.AddMeta("Roz.machine", Environment.MachineName);
                writer.AddMeta("Roz.command", escape(string.Join(" ",args)) ); 
                writer.AddMeta("Roz.startTime", DateTime.UtcNow.ToString("o"));
                writer.AddMeta("Roz.processID", processID.ToString());

                using (var watcher = new Roz.Core.Monitor.Processes(writer))
                {
                    Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) => Shutdown(writer, watcher);
                    AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs exceptArgs) => Shutdown(writer, watcher);
                    Wait($"Waiting to allow watcher to start...");
                    try
                    {
                        var watcherTask = Task.Run( () => watcher.Watch(watcherName, processID) );
                        var process = CreateProcess(args);
                        process.Exited += (object sender, EventArgs eventArgs) => Shutdown(writer, watcher);
                        process.Start();
                        process.WaitForExit();
                        if (watcherTask.IsFaulted) throw watcherTask.Exception ?? new Exception("Error in watcher");
                    }
                    catch (Exception e) when (!(e is OperationCanceledException))
                    {
                        Console.WriteLine($"{watcherName} An error occurred : {e}");
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
                UseShellExecute = false             
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
            writer.AddMeta("Roz.logEnd", DateTime.UtcNow.ToString("o"));
            Wait($"Shutting down watcher...");            
            watcher?.StopWatching();
            Wait($"Shutting down writer...");
            writer?.Close();            
            Console.WriteLine($"{watcherName} Logged processes to {fileName}");
            Console.WriteLine($"Open this file in chrome://tracing/");
        }

        static void Wait(string message, int milliseconds = 5000)
        {
            Console.WriteLine(message);            
            Task.Delay(milliseconds).Wait();
        }

        private static string GetTimeCode(DateTime t) => t.ToString("yyyyMMddhhmmssfff");        
        private static string escape(string entry) => entry.Replace(@"\",@"\\").Replace(@"""",@"\""");

    }
}
