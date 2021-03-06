﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AutoTest.Core.BuildRunners;
using AutoTest.Core.TestRunners;

namespace AutoTest.Core.Caching.RunResultCache
{
    interface IMergeRunResults
    {
        void Merge(BuildRunResults results);
        void Merge(TestRunResults results);
    }
}
