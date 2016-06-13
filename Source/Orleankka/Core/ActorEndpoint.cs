using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;

using Orleans;
using Orleans.Runtime;

namespace Orleankka.Core
{
    using Cluster;

    /// <summary> 
    /// FOR INTERNAL USE ONLY!
    /// </summary>
    public abstract class ActorEndpoint : Grain, IRemindable
    {
        readonly ActorType type;

        ActorContext context;
        Func<ActorContext, object, Task<object>> receiver;

        protected ActorEndpoint(string code)
        {
            type = ActorType.Registered(code);
        }

        public async Task<object> Receive(object message)
        {
            KeepAlive();

            return await receiver(context, message);
        }

        public Task<object> ReceiveReentrant(object message)
        {
            #if DEBUG
                CallContext.LogicalSetData("LastMessageReceivedReentrant", message);
            #endif

            return Receive(message);
        }

        public Task ReceiveVoid(object message)
        {
            KeepAlive();

            return receiver(context, message);
        }

        public Task ReceiveReentrantVoid(object message)
        {
            #if DEBUG
                CallContext.LogicalSetData("LastMessageReceivedReentrantVoid", message);
            #endif

            return ReceiveVoid(message);
        }

        async Task IRemindable.ReceiveReminder(string name, TickStatus status)
        {
            KeepAlive();

            await receiver(context, new Reminder(name));
        }

        public override Task OnActivateAsync()
        {
            return Activate(ActorPath.Deserialize(IdentityOf(this)));
        }

        public override Task OnDeactivateAsync()
        {
            return context != null
                    ? receiver(context, new Deactivate())
                    : base.OnDeactivateAsync();
        }

        Task Activate(ActorPath path)
        {
            context = new ActorContext(path, ClusterActorSystem.Current, this);
            receiver = type.Receiver(path.Id, context);
            return receiver(context, new Activate());
        }

        void KeepAlive() => type.KeepAlive(this);

        #region Internals

        internal new void DeactivateOnIdle()
        {
            base.DeactivateOnIdle();
        }

        internal new void DelayDeactivation(TimeSpan timeSpan)
        {
            base.DelayDeactivation(timeSpan);
        }

        internal new Task<IGrainReminder> GetReminder(string reminderName)
        {
            return base.GetReminder(reminderName);
        }

        internal new Task<List<IGrainReminder>> GetReminders()
        {
            return base.GetReminders();
        }

        internal new Task<IGrainReminder> RegisterOrUpdateReminder(string reminderName, TimeSpan dueTime, TimeSpan period)
        {
            return base.RegisterOrUpdateReminder(reminderName, dueTime, period);
        }

        internal new Task UnregisterReminder(IGrainReminder reminder)
        {
            return base.UnregisterReminder(reminder);
        }

        internal new IDisposable RegisterTimer(Func<object, Task> asyncCallback, object state, TimeSpan dueTime, TimeSpan period)
        {
            return base.RegisterTimer(asyncCallback, state, dueTime, period);
        }

        #endregion

        static string IdentityOf(IGrain grain)
        {
            return (grain as IGrainWithStringKey).GetPrimaryKeyString();
        }
    }
}