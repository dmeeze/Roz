using System.Diagnostics;
using Microsoft.Diagnostics.Tracing;

namespace Roz.Core.Monitor
{
    internal static class TraceEventExtensions
    {
        internal static double ElapsedMicroseconds(this TraceEvent e) => e.TimeStampRelativeMSec * 1000.0;
        internal static double ElapsedMicroseconds(this Stopwatch sw) => sw.ElapsedMilliseconds * 1000.0;
        
    }
}