﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.DependencyInjection;

namespace Squidex.Domain.Apps.Entities.Backup;

public sealed class DefaultBackupHandlerFactory(IServiceProvider serviceProvider) : IBackupHandlerFactory
{
    public IEnumerable<IBackupHandler> CreateMany()
    {
        return serviceProvider.GetRequiredService<IEnumerable<IBackupHandler>>().OrderBy(x => x.Order);
    }
}
