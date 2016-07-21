﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.R.Host.Client {
    partial class RHost {
        private class GetBlobResult : BlobResult {
            public IReadOnlyList<IRBlobData> Blobs;

            public GetBlobResult(List<IRBlobData> blobs) : base(BlobRequestKind.Get) {
                Blobs = blobs ?? new List<IRBlobData>();
            }
        }
    }
}
