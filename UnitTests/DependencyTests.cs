using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timelapse.Editor;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class DependencyTests
    {
        [TestMethod]
        public void RequiredBinaries()
        {
            Assert.IsTrue(Dependencies.AreRequiredBinariesPresent(Constant.ApplicationName, this.GetType().Assembly));
            Assert.IsTrue(Dependencies.AreRequiredBinariesPresent(EditorConstant.ApplicationName, this.GetType().Assembly));
        }
    }
}
