// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Diagnostics;

public interface IWithCustomTags {
    void SetCustomTags(TagList customTags);
}
