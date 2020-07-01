using Microsoft.VisualStudio.TestTools.UnitTesting;
using Roz.Core.Monitor;

namespace Roz.Core.Test
{
    [TestClass]
    public class PseudoThreadPoolTest
    {
        [TestMethod]
        public void Nested()
        {
            var pool = new PseudoThreadPool();
        
            Assert.AreEqual(0,pool.AddChild(42, 0));
            Assert.AreEqual(0,pool.AddChild(420, 42));
            Assert.AreEqual(0,pool.RemoveChild(420));            
            Assert.AreEqual(0,pool.RemoveChild(42));
        }

        [TestMethod]
        public void Forked()
        {
            var pool = new PseudoThreadPool();
        
            Assert.AreEqual(0,pool.AddChild(42, 0));
            Assert.AreEqual(0,pool.AddChild(420, 42));
            Assert.AreEqual(1,pool.AddChild(421, 42));
            Assert.AreEqual(0,pool.RemoveChild(420));            
            Assert.AreEqual(0,pool.AddChild(422, 42));
            Assert.AreEqual(1,pool.RemoveChild(421));
            Assert.AreEqual(0,pool.RemoveChild(422));
            Assert.AreEqual(0,pool.RemoveChild(42));
        }
        
    }
}