using System.Collections.Specialized;
using Japlayer.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Japlayer.UnitTests.Collections
{
    [TestClass]
    public class BatchObservableCollectionTests
    {
        [TestMethod]
        public void ReplaceAll_WhenItemsChange_EmitsSingleResetNotification()
        {
            var collection = new BatchObservableCollection<int>([1, 2, 3]);
            var events = new List<NotifyCollectionChangedAction>();
            collection.CollectionChanged += (_, eventArgs) => events.Add(eventArgs.Action);

            var changed = collection.ReplaceAll([4, 5]);

            Assert.IsTrue(changed);
            CollectionAssert.AreEqual(new[] { 4, 5 }, collection.ToList());
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, events[0]);
        }

        [TestMethod]
        public void ReplaceAll_WhenSequenceIsIdentical_ReturnsFalseAndEmitsNoNotifications()
        {
            var collection = new BatchObservableCollection<string>(["A", "B"]);
            var eventCount = 0;
            collection.CollectionChanged += (_, _) => eventCount++;

            var changed = collection.ReplaceAll(["A", "B"]);

            Assert.IsFalse(changed);
            Assert.AreEqual(0, eventCount);
        }
    }
}
