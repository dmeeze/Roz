using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Roz.Core.Logger;

namespace Roz.Core.Monitor
{
    public class Processes : IDisposable
    {
        const int NONE = -1;

        private readonly Writer writer;
        private TraceEventSession session;
        private readonly PseudoThreadPool pseudoThreads;
        private readonly ConcurrentDictionary<int, CompleteEntry> pendingWriteProcesses;
        private readonly ConcurrentDictionary<int, int> watchedProcesses;
        private readonly Stopwatch timer;

        private System.IO.StreamWriter f;
        public Processes(Writer logWriter)
        {
            pseudoThreads = new PseudoThreadPool();
            pendingWriteProcesses = new ConcurrentDictionary<int, CompleteEntry>();
            watchedProcesses = new ConcurrentDictionary<int, int>();
            writer = logWriter;
            timer = new Stopwatch();
        }

        public void Watch(string sessionName, int rootParentProcess)
        {
            watchedProcesses[rootParentProcess] = NONE;

            if (!(TraceEventSession.IsElevated() ?? false)) throw new Exception("Tracing requires Administrator rights.");

            if (session != null) throw new Exception("Session already started");
            // NB : StopOnDispose is the default, but because trace sessions continue even after
            // the process which created them has ended, we want to be certain.
            session = new TraceEventSession(sessionName) { StopOnDispose = true };
            session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);
            session.Source.Kernel.ProcessStart += ProcessStartEvent;
            session.Source.Kernel.ProcessStop += ProcessStopEvent;            
            timer.Start();
            writer.AddEntry(new BeginEntry(NONE,NONE,timer.ElapsedMicroseconds(),"ROOT"));
            session.Source.Process();        
            writer.AddEntry(new EndEntry(NONE,NONE,timer.ElapsedMicroseconds()));
            Flush();
        }
        
        public void StopWatching()
        {
            if (session?.IsActive ?? false)
            {
                session.Source.Kernel.ProcessStart -= ProcessStartEvent;
                session.Source.Kernel.ProcessStop -= ProcessStopEvent;
                session?.Stop();
            }
        }

        private void Flush()
        {
            var pendingProcessIDs = pendingWriteProcesses.Keys.ToList();

            foreach (var processID in pendingProcessIDs)
            {
                if (watchedProcesses.TryGetValue(processID, out var _))
                {
                    if (pendingWriteProcesses.TryRemove(processID, out var entry))
                    {
                        var unfinishedProcessEntry = new BeginEntry(entry.ProcessID, entry.ThreadID, entry.Timestamp, entry.Name);
                        unfinishedProcessEntry.Args = entry.Args;
                        writer.AddEntry(unfinishedProcessEntry);
                    }
                }
            }
        }

        public void Dispose()
        {
            session?.Dispose();
            writer?.Dispose();
        }

        private void ProcessStartEvent(TraceEvent data)
        {
            var processID = data.ProcessID;
            var parentID = getParentID(data) ?? NONE;
            var threadID = NONE;

            if (watchedProcesses.TryGetValue(parentID, out int _))
            {
                //this is part of the tree so add to the tree.
                threadID = pseudoThreads.AddChild(data.ProcessID, parentID);
                watchedProcesses[processID] = parentID;
            }

            var entry = new CompleteEntry(NONE, threadID, data.ElapsedMicroseconds(), 0.0, data.ProcessName);
            entry.Args = buildArgs(data).ToList();
            pendingWriteProcesses.TryAdd(processID, entry);
        }

        private void ProcessStopEvent(TraceEvent data)
        {
            var processID = data.ProcessID;
            f.WriteLine($"STOP ,{processID},");

            if (pendingWriteProcesses.TryGetValue(processID, out var entry))
            {
                entry.Duration = (data.ElapsedMicroseconds()) - entry.Timestamp;

                if (watchedProcesses.TryGetValue(processID, out var parentID))
                {
                    var threadID = pseudoThreads.RemoveChild(processID);
                    pendingWriteProcesses.TryRemove(processID, out var _);
                    entry.ThreadID = threadID ?? NONE;
                    writer.AddEntry(entry);
                }
            }
        }

        private int? getParentID(TraceEvent data)
        {
            for (int i = 0; i < data.PayloadNames.Length; i++)
            {
                var key = data.PayloadNames[i];
                
                switch (key)
                {
                    case "ParentID" :
                    case "ParentProcessID" :
                        var value = data.PayloadValue(i);                        
                        if (value is int) return (int)value;
                        break;
                    default:
                        break;
                }
            }
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
                    case "ParentProcessID" :
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