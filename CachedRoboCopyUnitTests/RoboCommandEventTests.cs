using Microsoft.VisualStudio.TestTools.UnitTesting;
using RoboSharp.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace RFBCodeWorks.CachedRoboCopy.Tests
{
    [TestClass]
    public class RoboCommandEventTests : RoboSharp.Tests.RoboCommandEventTests
    {
        public override IRoboCommand GetRoboCommand(bool useLargerFileSet, bool ListOnlyMode)
        {
            return TestPrep.GetCachedRoboCopy(base.GetRoboCommand(useLargerFileSet, ListOnlyMode));
        }
    }
}
