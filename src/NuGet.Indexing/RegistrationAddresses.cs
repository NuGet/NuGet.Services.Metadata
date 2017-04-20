// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Indexing
{
    public class RegistrationAddresses
    {
        public Uri HttpRegistrationAddress { get; set; }
        public Uri HttpsRegistrationAddress { get; set; }
        public Uri SemVer2HttpRegistrationAddress { get; set; }
        public Uri SemVer2HttpsRegistrationAddress { get; set; }
    }
}
