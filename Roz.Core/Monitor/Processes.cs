using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Roz.Core.Log;

namespace Roz.Core.Monitor
{
    public class Processes : IDisposable
    {
        const int NONE = -1;

        private readonly Log.Writer writer;
        private TraceEventSession session;
        private readonly Stopwatch stopwatch;
        private readonly PseudoThreadPool processes;

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
            session.Source.Process();
        }
        
        public void StopWatching()
        {
            if (session?.IsActive ?? false)
            {
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
            var entry = new BeginEntry(NONE, NONE, data.TimeStampRelativeMSec * 1000.0, data.ProcessName);
            var parentID = getParentID(data) ?? NONE;            
            entry.ThreadID = processes.AddChild(data.ProcessID, parentID);
            entry.Args = buildArgs(data).ToList();
            writer.AddEntry(entry);
        }

        private void ProcessStopEvent(TraceEvent data)
        {
            var entry = new EndEntry(NONE, NONE, data.TimeStampRelativeMSec * 1000.0);
            var tid = processes.RemoveChild(data.ProcessID);
            if (tid.HasValue) entry.ThreadID = tid.Value;            
            entry.Args = buildArgs(data).ToList();
            writer.AddEntry(entry);
        }

        private int? getParentID(TraceEvent data)
        {
            var parentIDString = data.PayloadStringByName("ParentID");
            if (string.IsNullOrWhiteSpace(parentIDString)) return null;
            if (int.TryParse(parentIDString, out int foundID)) return foundID;
            return null;
        }

        private IEnumerable<(string,string)> buildArgs(TraceEvent data)
        {
            for (int i = 0; i < data.PayloadNames.Length; i++)
            {
                var key = data.PayloadNames[i];
                
                switch (key)
                {
                    case "CommandLine" :
                    case "PackageFullName" :
                    case "ImageFileName" :
                        yield return (key, escape(data.PayloadStringByName(key)));
                        break;
                    case "ParentID" :
                    case "ProcessID" :                    
                    case "SessionID" :
                    case "ExitStatus" :
                        yield return(key, data.PayloadStringByName(key));
                        break;
                    default:
                        //yield return(key, data.PayloadStringByName(key));
                        break;                    
                }                
            }
        }

        private string escape(string entry) => entry.Replace(@"\",@"\\").Replace(@"""",@"\""");

    }
}