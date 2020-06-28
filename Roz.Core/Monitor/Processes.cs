using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Roz.Core.Log;

namespace Roz.Core.Monitor
{
    public class Processes : IDisposable
    {
        private readonly Log.Writer writer;
        private TraceEventSession session;
        private readonly Stopwatch stopwatch;
        private readonly PseudoThreadPool processes;

        const int NONE = -1;

        public Processes(Log.Writer logWriter)
        {
            stopwatch = new Stopwatch();
            processes = new PseudoThreadPool();
            writer = logWriter;
        }

        public void Watch(string sessionName)
        {
            if (!(TraceEventSession.IsElevated() ?? false)) throw new Exception("Tracing requires Administrator rights.");

            if (session != null) throw new Exception("Session already started");
            // NB : StopOnDispose is the default, but because trace sessions continue even after
            // the process which created them has ended, we want to be certain.
            session = new TraceEventSession(sessionName) { StopOnDispose = true };
            session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);
            session.Source.Kernel.ProcessStart += ProcessStartEvent;
            session.Source.Kernel.ProcessStop += ProcessStopEvent;
            stopwatch.Start();
            writer.AddEntry(new BeginEntry(NONE,NONE,0.0,"ROOT"));
            session.Source.Process();
        }
        
        public void StopWatching()
        {
            if (session?.IsActive ?? false)
            {
                writer.AddEntry(new EndEntry(-1,-1,stopwatch.ElapsedMilliseconds * 1000.0));
                session?.Stop();
            }
        }

        public void Dispose()
        {
            session?.Dispose();
            writer?.Dispose();
        }

        private void ProcessStartEvent(TraceEvent data)
        {
            var tid = processes.AddChild(data.ProcessID);
            var entry = new BeginEntry(-1, tid, data.TimeStampRelativeMSec * 1000.0, data.ProcessName);
            addArgs(data, entry);
            writer.AddEntry(entry);
        }

        private void ProcessStopEvent(TraceEvent data)
        {
            var tid = processes.RemoveChild(data.ProcessID);
            if (!tid.HasValue) return;
            var entry = new EndEntry(-1, tid.Value, data.TimeStampRelativeMSec * 1000.0);
            addArgs(data, entry);
            writer.AddEntry(entry);
        }

        private int? addArgs(TraceEvent data, Entry entry)
        {
            int? result = null;
            for (int i = 0; i < data.PayloadNames.Length; i++)
            {
                var key = data.PayloadNames[i];
                
                switch (key)
                {
                    case "ParentProcessID" :
                        var value = data.PayloadStringByName(key);
                        if (int.TryParse(value, out int parentID)) result = parentID;
                        entry.Args.Add((key, value));
                        break;
                    case "CommandLine" :
                    case "PackageFullName" :
                    case "ImageFileName" :
                        entry.Args.Add((key, escape(data.PayloadStringByName(key))));
                        break;
                    case "ProcessID" :                    
                    case "SessionID" :
                    case "ExitStatus" :
                        entry.Args.Add((key, data.PayloadStringByName(key)));
                        break;
                    default:
                        break;                    
                }                
            }
            return result;
        }

        private string escape(string entry) => entry.Replace(@"\",@"\\").Replace(@"""",@"\""");

    }
}