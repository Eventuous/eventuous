// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// Backstop, not the budget, so a hung wait fails the test instead of hanging CI forever. Tests with a
// tighter bound still carry their own [Timeout]; this just has to clear the slowest legitimate test (the
// 100-cycle resubscribe ones, at a 60s budget).
[assembly: Timeout(120_000)]
