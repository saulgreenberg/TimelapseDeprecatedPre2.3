using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timelapse.Editor;
using Timelapse.Util;

namespace Timelapse.UnitTests
{
    [TestClass]
    public class VersionTests
    {
        [TestMethod]
        public void CheckForUpdates()
        {
            VersionClient timelapseUpdates = new VersionClient(null, Constant.ApplicationName, Constant.LatestVersionFilenameXML);
            Assert.IsTrue(timelapseUpdates.TryGetAndParseVersion(false));

            VersionClient editorUpdates = new VersionClient(null, EditorConstant.ApplicationName, EditorConstant.LatestVersionAddress);
            Assert.IsTrue(editorUpdates.TryGetAndParseVersion(false));
        }
    }
}
