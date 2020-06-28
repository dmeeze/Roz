using System.Collections.Generic;
using System.Linq;

namespace Roz.Core.Monitor
{
    public class PseudoThreadPool
    {
        private List<List<int>> pseudoThreads = new List<List<int>>();

        public int AddChild(int childProcessID, int parentProcessID)
        {
            lock (pseudoThreads)
            {
                int? firstFree = null;
                for (int i=0; i < pseudoThreads.Count; i++)
                {
                    var thread = pseudoThreads[i];
                    if ((thread.Any()) && (thread.Last() == parentProcessID))
                    {
                        thread.Add(childProcessID);
                        return i;
                    }
                    if (!firstFree.HasValue && !thread.Any()) firstFree = i;
                }
                if (firstFree.HasValue)
                {
                    pseudoThreads[firstFree.Value].Add(childProcessID);
                    return firstFree.Value;
                }
                pseudoThreads.Add(new List<int>() { childProcessID });
                return pseudoThreads.Count - 1;
            }
        }

        public int? RemoveChild(int childProcessID)
        {
            lock (pseudoThreads)
            {
                for (int i=0; i < pseudoThreads.Count; i++)
                {
                    if (pseudoThreads[i].Remove(childProcessID)) return i;
                }
            }
            return null;
        }
    }
}