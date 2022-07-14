﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Squidex.Infrastructure.States;
using Squidex.Infrastructure.Tasks;

namespace Squidex.Infrastructure.EventSourcing.Consume
{
    public class EventConsumerProcessor : IEventSubscriber<ParsedEvents>
    {
        private readonly SimpleState<EventConsumerState> state;
        private readonly IEventFormatter eventFormatter;
        private readonly IEventConsumer eventConsumer;
        private readonly IEventStore eventStore;
        private readonly ILogger<EventConsumerProcessor> log;
        private readonly AsyncLock asyncLock = new AsyncLock();
        private IEventSubscription? currentSubscription;

        public EventConsumerState State
        {
            get => state.Value;
            set => state.Value = value;
        }

        public EventConsumerProcessor(
            IPersistenceFactory<EventConsumerState> persistenceFactory,
            IEventConsumer eventConsumer,
            IEventFormatter eventFormatter,
            IEventStore eventStore,
            ILogger<EventConsumerProcessor> log)
        {
            this.eventStore = eventStore;
            this.eventFormatter = eventFormatter;
            this.eventConsumer = eventConsumer;
            this.log = log;

            state = new SimpleState<EventConsumerState>(persistenceFactory, GetType(), eventConsumer.Name);
        }

        public virtual Task InitializeAsync(
            CancellationToken ct)
        {
            return state.LoadAsync(ct);
        }

        public virtual async Task CompleteAsync()
        {
            if (currentSubscription is BatchSubscription batchSubscriber)
            {
                try
                {
                    await batchSubscriber.CompleteAsync();
                }
                catch (Exception ex)
                {
                    log.LogCritical(ex, "Failed to complete consumer.");
                }
            }
        }

        public virtual async ValueTask OnNextAsync(IEventSubscription subscription, ParsedEvents @event)
        {
            await UpdateAsync(async () =>
            {
                if (!ReferenceEquals(subscription, currentSubscription))
                {
                    return;
                }

                await DispatchAsync(@event.Events);

                State = State.Handled(@event.Position, @event.Events.Count);
            }, State.Position);
        }

        public virtual async ValueTask OnErrorAsync(IEventSubscription subscription, Exception exception)
        {
            await UpdateAsync(() =>
            {
                if (!ReferenceEquals(subscription, currentSubscription))
                {
                    return;
                }

                Unsubscribe();

                State = State.Stopped(exception);
            }, State.Position);
        }

        public virtual Task ActivateAsync()
        {
            return UpdateAsync(() =>
            {
                if (State.IsFailed)
                {
                    Subscribe();

                    State = State.Started();
                }
                else if (!State.IsStopped)
                {
                    Subscribe();
                }
            }, State.Position);
        }

        public virtual Task StartAsync()
        {
            return UpdateAsync(() =>
            {
                if (!State.IsStopped)
                {
                    return;
                }

                Subscribe();

                State = State.Started();
            }, State.Position);
        }

        public virtual Task StopAsync()
        {
            return UpdateAsync(() =>
            {
                if (State.IsStopped)
                {
                    return;
                }

                Unsubscribe();

                State = State.Stopped();
            }, State.Position);
        }

        public virtual async Task ResetAsync()
        {
            await UpdateAsync(async () =>
            {
                Unsubscribe();

                await ClearAsync();

                State = EventConsumerState.Initial;

                Subscribe();
            }, State.Position);
        }

        private async Task DispatchAsync(IReadOnlyList<Envelope<IEvent>> events)
        {
            if (events.Count > 0)
            {
                await eventConsumer!.On(events);
            }
        }

        private Task UpdateAsync(Action action, string? position, [CallerMemberName] string? caller = null)
        {
            return UpdateAsync(() =>
            {
                action();

                return Task.CompletedTask;
            }, position, caller);
        }

        private async Task UpdateAsync(Func<Task> action, string? position, [CallerMemberName] string? caller = null)
        {
            // We do not want to deal with concurrency in this class, therefore we just use a lock.
            using (await asyncLock.EnterAsync())
            {
                var previousState = State;

                try
                {
                    await action();
                }
                catch (Exception ex)
                {
                    try
                    {
                        Unsubscribe();
                    }
                    catch (Exception unsubscribeException)
                    {
                        ex = new AggregateException(ex, unsubscribeException);
                    }

                    log.LogCritical(ex, "Failed to update consumer {consumer} at position {position} from {caller}.",
                        eventConsumer!.Name, position, caller);

                    State = previousState.Stopped(ex);
                }

                // The state is a record, therefore we can check for value equality.
                if (!Equals(previousState, State))
                {
                    await state.WriteAsync();
                }
            }
        }

        private async Task ClearAsync()
        {
            if (log.IsEnabled(LogLevel.Debug))
            {
                log.LogDebug("Event consumer {consumer} reset started", eventConsumer!.Name);
            }

            var watch = ValueStopwatch.StartNew();
            try
            {
                await eventConsumer!.ClearAsync();
            }
            finally
            {
                log.LogDebug("Event consumer {consumer} reset completed after {time}ms.", eventConsumer!.Name, watch.Stop());
            }
        }

        private void Unsubscribe()
        {
            if (currentSubscription != null)
            {
                currentSubscription.Dispose();
                currentSubscription = null;
            }
        }

        private void Subscribe()
        {
            if (currentSubscription == null)
            {
                currentSubscription = CreateRetrySubscription(this);
            }
            else
            {
                currentSubscription.WakeUp();
            }
        }

        protected IEventSubscription CreatePipeline(IEventSubscriber<ParsedEvents> subscriber)
        {
            return new BatchSubscription(eventConsumer, subscriber,
                x => new ParseSubscription(eventConsumer, eventFormatter, x, CreateSubscription));
        }

        protected virtual IEventSubscription CreateRetrySubscription(IEventSubscriber<ParsedEvents> subscriber)
        {
            return new RetrySubscription<ParsedEvents>(subscriber, CreatePipeline);
        }

        protected virtual IEventSubscription CreateSubscription(IEventSubscriber<StoredEvent> subscriber)
        {
            return eventStore.CreateSubscription(subscriber, eventConsumer!.EventsFilter, State.Position);
        }
    }
}