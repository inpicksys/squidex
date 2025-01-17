﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Options;
using Squidex.AI.Implementation.OpenAI;
using Squidex.Domain.Apps.Core;
using Squidex.Domain.Apps.Core.Assets;
using Squidex.Domain.Apps.Entities.Assets;
using Squidex.Domain.Apps.Entities.Contents;
using Squidex.Infrastructure;
using IGenericUrlGenerator = Squidex.Hosting.IUrlGenerator;

namespace Squidex.Web.Services;

public sealed class UrlGenerator(
    IGenericUrlGenerator urlGenerator,
    IAssetFileStore assetFileStore,
    IOptions<AssetOptions> assetOptions,
    IOptions<ContentsOptions> contentOptions)
    : IUrlGenerator, IHttpImageEndpoint
{
    private readonly AssetOptions assetOptions = assetOptions.Value;
    private readonly ContentsOptions contentOptions = contentOptions.Value;

    public string? AssetThumbnail(NamedId<DomainId> appId, string idOrSlug, AssetType assetType)
    {
        if (assetType != AssetType.Image)
        {
            return null;
        }

        return urlGenerator.BuildUrl($"api/assets/{appId.Name}/{idOrSlug}?width=100&mode=Max");
    }

    public string AssetContentCDNBase()
    {
        return assetOptions.CDN ?? string.Empty;
    }

    public string AssetContentBase()
    {
        return urlGenerator.BuildUrl("api/assets/");
    }

    public string AssetContentBase(string appName)
    {
        return urlGenerator.BuildUrl($"api/assets/{appName}/");
    }

    public string AssetContent(NamedId<DomainId> appId, DomainId assetId)
    {
        return urlGenerator.BuildUrl($"api/assets/{appId.Name}/{assetId}");
    }

    public string AssetContent(NamedId<DomainId> appId, string idOrSlug)
    {
        return urlGenerator.BuildUrl($"api/assets/{appId.Name}/{idOrSlug}");
    }

    public string AssetContent(NamedId<DomainId> appId, string idOrSlug, long version)
    {
        return urlGenerator.BuildUrl($"api/assets/{appId.Name}/{idOrSlug}?version={version}");
    }

    public string? AssetSource(NamedId<DomainId> appId, DomainId assetId, long fileVersion)
    {
        return assetFileStore.GeneratePublicUrl(appId.Id, assetId, fileVersion, null);
    }

    public string AssetsUI(NamedId<DomainId> appId, string? @ref = null)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/assets", false) + @ref != null ? $"?ref={@ref}" : string.Empty;
    }

    public string ClientsUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/settings/clients", false);
    }

    public string ContentCDNBase()
    {
        return contentOptions.CDN ?? string.Empty;
    }

    public string ContentBase()
    {
        return urlGenerator.BuildUrl("api/content/", false);
    }

    public string ContentsUI(NamedId<DomainId> appId, NamedId<DomainId> schemaId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/content/{schemaId.Name}", false);
    }

    public string ContentUI(NamedId<DomainId> appId, NamedId<DomainId> schemaId, DomainId contentId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/content/{schemaId.Name}/{contentId}/history", false);
    }

    public string ContributorsUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/settings/contributors", false);
    }

    public string DashboardUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}", false);
    }

    public string JobsUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/settings/jobs", false);
    }

    public string LanguagesUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/settings/languages", false);
    }

    public string PatternsUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/settings/patterns", false);
    }

    public string PlansUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/settings/plans", false);
    }

    public string RolesUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/settings/roles", false);
    }

    public string Root()
    {
        return urlGenerator.BuildUrl();
    }

    public string RulesUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/rules", false);
    }

    public string SchemasUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/schemas", false);
    }

    public string SchemaUI(NamedId<DomainId> appId, NamedId<DomainId> schemaId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/schemas/{schemaId.Name}", false);
    }

    public string WorkflowsUI(NamedId<DomainId> appId)
    {
        return urlGenerator.BuildUrl($"app/{appId.Name}/settings/workflows", false);
    }

    public string UI()
    {
        return urlGenerator.BuildUrl("app", false);
    }

    string IHttpImageEndpoint.GetUrl(string relativePath)
    {
        return urlGenerator.BuildUrl($"ai-images/{relativePath}", false);
    }
}
