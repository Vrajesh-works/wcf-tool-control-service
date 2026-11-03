using System;
using System.Threading.Tasks;
using ToolControl.Core;
using Xunit;

namespace ToolControl.Core.Tests
{
    public class ChamberControllerTests
    {
        private static Recipe StandardRecipe() => new Recipe("Standard Etch", 120, 150, TimeSpan.FromMinutes(30));

        [Fact]
        public void StartPauseResumeAbort_FollowsStateMachine()
        {
            var controller = new ChamberController();
            Assert.Equal(RunState.Idle, controller.State);
            controller.StartRecipe(StandardRecipe());
            Assert.Equal(RunState.Running, controller.State);
            controller.Pause();
            Assert.Equal(RunState.Paused, controller.State);
            controller.Resume();
            Assert.Equal(RunState.Running, controller.State);
            controller.Abort();
            Assert.Equal(RunState.Aborted, controller.State);
        }

        [Fact]
        public void StartRecipe_RejectsOutOfRangeTargets()
        {
            var controller = new ChamberController();
            Assert.Throws<ArgumentException>(() => controller.StartRecipe(new Recipe("Bad", 9999, 150, TimeSpan.FromMinutes(10))));
        }

        [Fact]
        public void StartRecipe_RejectsSecondActiveRun()
        {
            var controller = new ChamberController();
            controller.StartRecipe(StandardRecipe());
            Assert.Throws<InvalidOperationException>(() => controller.StartRecipe(StandardRecipe()));
        }

        [Fact]
        public void OutOfRangeReading_RaisesAlarmOnlyWhileRunning()
        {
            var controller = new ChamberController();
            controller.OnReading(new ChamberReading(DateTime.UtcNow, 9999, 150));
            Assert.Empty(controller.GetActiveAlarms());
            controller.StartRecipe(StandardRecipe());
            controller.OnReading(new ChamberReading(DateTime.UtcNow, 9999, 150));
            Assert.Single(controller.GetActiveAlarms());
        }

        [Fact]
        public void AcknowledgeAlarm_RemovesItFromActiveList()
        {
            var controller = new ChamberController();
            controller.StartRecipe(StandardRecipe());
            controller.OnReading(new ChamberReading(DateTime.UtcNow, 120, 999));
            var alarm = Assert.Single(controller.GetActiveAlarms());
            Assert.True(controller.AcknowledgeAlarm(alarm.Id));
            Assert.Empty(controller.GetActiveAlarms());
            Assert.False(controller.AcknowledgeAlarm(alarm.Id));
        }

        [Fact]
        public async Task SubscriberQueue_DropsOldestInsteadOfBlockingProducer()
        {
            var queue = new SubscriberQueue<int>(capacity: 3);
            for (var i = 0; i < 10; i++) queue.Enqueue(i);
            Assert.Equal(3, queue.Count);
            Assert.True(queue.DroppedCount > 0);
            var first = await queue.TakeAsync(default);
            Assert.Equal(7, first);
        }

        [Fact]
        public void Broadcaster_FansOutToEverySubscriberWithoutBlocking()
        {
            var controller = new ChamberController();
            var a = controller.Broadcaster.Subscribe("client-a");
            var b = controller.Broadcaster.Subscribe("client-b");
            controller.OnReading(new ChamberReading(DateTime.UtcNow, 120, 150));
            Assert.Equal(1, a.Count);
            Assert.Equal(1, b.Count);
            Assert.Equal(2, controller.Broadcaster.SubscriberCount);
            Assert.True(controller.Broadcaster.Unsubscribe("client-a"));
            Assert.Equal(1, controller.Broadcaster.SubscriberCount);
        }
    }
}
