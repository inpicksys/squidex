﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Runtime.CompilerServices;
using Squidex.Domain.Apps.Core.Assets;
using Squidex.Domain.Apps.Core.HandleRules;
using Squidex.Domain.Apps.Core.Rules;
using Squidex.Domain.Apps.Core.Rules.EnrichedEvents;
using Squidex.Domain.Apps.Core.Rules.Triggers;
using Squidex.Domain.Apps.Core.Scripting;
using Squidex.Domain.Apps.Core.Subscriptions;
using Squidex.Domain.Apps.Entities.Assets.Repositories;
using Squidex.Domain.Apps.Events;
using Squidex.Domain.Apps.Events.Assets;
using Squidex.Infrastructure.EventSourcing;
using Squidex.Infrastructure.Reflection;

namespace Squidex.Domain.Apps.Entities.Assets;

public sealed class AssetChangedTriggerHandler(
    IScriptEngine scriptEngine,
    IAssetLoader assetLoader,
    IAssetRepository assetRepository)
    : IRuleTriggerHandler, ISubscriptionEventCreator
{
    public bool CanCreateSnapshotEvents => true;

    public Type TriggerType => typeof(AssetChangedTriggerV2);

    public bool Handles(AppEvent @event)
    {
        return @event is AssetEvent and not AssetMoved;
    }

    public async IAsyncEnumerable<EnrichedEvent> CreateSnapshotEventsAsync(RuleContext context,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var asset in assetRepository.StreamAll(context.AppId.Id, ct))
        {
            var result = new EnrichedAssetEvent
            {
                Type = EnrichedAssetEventType.Created,
            };

            SimpleMapper.Map(asset, result);

            result.Actor = asset.LastModifiedBy;
            result.PixelHeight = asset.Metadata?.GetInt32(KnownMetadataKeys.PixelHeight);
            result.PixelWidth = asset.Metadata?.GetInt32(KnownMetadataKeys.PixelWidth);
            result.Name = "AssetQueried";

            yield return result;
        }
    }

    public async IAsyncEnumerable<EnrichedEvent> CreateEnrichedEventsAsync(Envelope<AppEvent> @event, RulesContext context,
        [EnumeratorCancellation] CancellationToken ct)
    {
        yield return await CreateEnrichedEventsCoreAsync(@event, ct);
    }

    public async ValueTask<EnrichedEvent?> CreateEnrichedEventsAsync(Envelope<AppEvent> @event,
        CancellationToken ct)
    {
        return await CreateEnrichedEventsCoreAsync(@event, ct);
    }

    private async ValueTask<EnrichedEvent> CreateEnrichedEventsCoreAsync(Envelope<AppEvent> @event,
        CancellationToken ct)
    {
        var assetEvent = (AssetEvent)@event.Payload;

        var result = new EnrichedAssetEvent();

        var asset = await assetLoader.GetAsync(
            assetEvent.AppId.Id,
            assetEvent.AssetId,
            @event.Headers.EventStreamNumber(),
            ct);

        if (asset != null)
        {
            SimpleMapper.Map(asset, result);

            result.PixelHeight = asset.Metadata?.GetInt32(KnownMetadataKeys.PixelHeight);
            result.PixelWidth = asset.Metadata?.GetInt32(KnownMetadataKeys.PixelWidth);
            result.AssetType = asset.Type;
        }

        // Use the concrete event to map properties that are not part of app event.
        SimpleMapper.Map(assetEvent, result);

        switch (@event.Payload)
        {
            case AssetCreated:
                result.Type = EnrichedAssetEventType.Created;
                break;
            case AssetAnnotated:
                result.Type = EnrichedAssetEventType.Annotated;
                break;
            case AssetUpdated:
                result.Type = EnrichedAssetEventType.Updated;
                break;
            case AssetDeleted:
                result.Type = EnrichedAssetEventType.Deleted;
                break;
        }

        return result;
    }

    public bool Trigger(EnrichedEvent @event, RuleTrigger trigger)
    {
        var assetTrigger = (AssetChangedTriggerV2)trigger;

        if (string.IsNullOrWhiteSpace(assetTrigger.Condition))
        {
            return true;
        }

        // Script vars are just wrappers over dictionaries for better performance.
        var vars = new EventScriptVars
        {
            Event = @event,
        };

        return scriptEngine.Evaluate(vars, assetTrigger.Condition);
    }
}
