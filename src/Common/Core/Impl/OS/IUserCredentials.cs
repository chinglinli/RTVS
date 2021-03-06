﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Security;

namespace Microsoft.Common.Core.OS {
    public interface IUserCredentials {
        string Username { get; set; }
        SecureString Password { get; set; }
        string Domain { get; set; }
    }
}
