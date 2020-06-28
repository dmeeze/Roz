using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Roz.Core.Logger
{
    public class Writer : IDisposable
    {
        private readonly TimeSpan shutdownTime = TimeSpan.FromSeconds(30);
        private readonly TimeSpan sleepTime = TimeSpan.FromMilliseconds(100);
        private readonly Task writerTask;
        private readonly CancellationTokenSource cancellation;
        private readonly string fileName;
        private StreamWriter file;
        private readonly ConcurrentQueue<Entry> entries;
        private readonly ConcurrentDictionary<string,string> meta;
        
        public Writer(string fileName)
        {
            entries = new ConcurrentQueue<Entry>();
            meta = new ConcurrentDictionary<string, string>();
            this.fileName = fileName;
            cancellation = new CancellationTokenSource();
            writerTask = Task.Run(writerAsync);
        }

        public void AddEntry(Entry e) => entries.Enqueue(e);
        public void AddMeta(string tag, string value) => meta.AddOrUpdate(tag, value, (t,v) => $"{v},{value}" );

        private async Task writerAsync()
        {
            try
            {
                bool needsSeparator = false;    
                file = File.CreateText(fileName);
                await file.WriteLineAsync("{");
                await file.WriteLineAsync(@"""traceEvents"": [");
                while (!cancellation.IsCancellationRequested)
                {
                    needsSeparator = await writeAll(needsSeparator);
                    Task.Delay(sleepTime).Wait();
                }
                needsSeparator = await writeAll(needsSeparator);
            }
            finally
            {
                List<string> metaStrings = meta.Select(m => $@"""{m.Key}"": ""{m.Value}""").ToList();

                await file.WriteLineAsync("");
                await file.WriteLineAsync((metaStrings.Count > 0) ? "]," : "]");
                for (int i = 0; i < metaStrings.Count; i++)
                {
                    await file.WriteLineAsync( (i==metaStrings.Count-1) ? metaStrings[i] : metaStrings[i]+"," );
                }
                await file.WriteLineAsync("}");
                await file.FlushAsync();
                file.Close();
                file = null;
            }
        }

        private async Task<bool> writeAll(bool needsSeparator)
        {
            while (entries.TryDequeue(out var entry))
            {
                if (needsSeparator) await file.WriteLineAsync(",");
                needsSeparator = true;
                await file.WriteAsync(entry.Serialize());
            }

            return needsSeparator;
        }

        public void Close()
        {
            cancellation.Cancel();
            writerTask?.Wait(shutdownTime);
            file?.Close();
        }

        public void Dispose()
        {
            Close();
        }
    }
}