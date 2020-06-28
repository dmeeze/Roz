using System.Collections.Generic;

namespace Roz.Core.Monitor
{
    internal class PseudoThreadPool
    {
        private List<int?> children = new List<int?>();

        public int AddChild(int childProcessID)
        {
            lock (children)
            {
                for (int i=0; i < children.Count; i++)
                {
                    if (children[i].HasValue) continue;

                    children[i] = childProcessID;
                    return i;
                }
                children.Add(childProcessID);
                return children.Count - 1;
            }
        }

        public int? RemoveChild(int childProcessID)
        {
            lock (children)
            {
                for (int i=0; i < children.Count; i++)
                {
                    if (!children[i].HasValue) continue;
                    if (children[i].Value == childProcessID)
                    {
                        children[i] = null;
                        return i;
                    }
                }
            }
            return null;
        }
    }
}