﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.IO.Pipelines;

namespace Squidex.Web.Pipeline;

public sealed class UsagePipeWriter(PipeWriter inner) : PipeWriter
{
    private long bytesWritten;

    public long BytesWritten
    {
        get => bytesWritten;
    }

    public override void Advance(int bytes)
    {
        inner.Advance(bytes);

        bytesWritten += bytes;
    }

    public override void CancelPendingFlush()
    {
        inner.CancelPendingFlush();
    }

    public override void Complete(Exception? exception = null)
    {
        inner.Complete();
    }

    public override ValueTask<FlushResult> FlushAsync(
        CancellationToken cancellationToken = default)
    {
        return inner.FlushAsync(cancellationToken);
    }

    public override Memory<byte> GetMemory(int sizeHint = 0)
    {
        return inner.GetMemory(sizeHint);
    }

    public override Span<byte> GetSpan(int sizeHint = 0)
    {
        return inner.GetSpan(sizeHint);
    }
}
