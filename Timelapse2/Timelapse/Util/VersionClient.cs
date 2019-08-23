using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Xml;
using Timelapse.Dialog;
using MessageBox = Timelapse.Dialog.MessageBox;

namespace Timelapse.Util
{
    // Check if the version currently being run is the latest version
    public class VersionClient
    {
        private Uri latestVersionAddress; // The url of the timelapse_template_version timelapse_version xml file containing the versioo information
        private string applicationName;  // Either Timelapse or TimelapseEditor
        private Window window;

        public VersionClient(Window window, string applicationName, Uri latestVersionAddress)
        {
            this.applicationName = applicationName;
            this.latestVersionAddress = latestVersionAddress;
            this.window = window;
        }

        // Checks for updates by comparing the current version number of Timelapse or the Editor with a version stored on the Timelapse website in an xml file in either
        // timelapse_version.xml or timelapse_template_version.xml (as specified in the latestVersionAddress). 
        public bool TryGetAndParseVersion(bool showNoUpdatesMessage)
        {
            string url = String.Empty; // THE URL where the new version is located
            Version latestVersionNumber = null;  // if a new version is available, store the new version number here  

            XmlTextReader reader = null;
            try
            {
                // provide the XmlTextReader with the URL of our xml document  
                reader = new XmlTextReader(this.latestVersionAddress.AbsoluteUri);
                reader.MoveToContent(); // skip the junk at the beginning  

                // As the XmlTextReader moves only forward, we save current xml element name in elementName variable. 
                // When we parse a  text node, we refer to elementName to check what was the node name  
                string elementName = String.Empty;
                // Check if the xml starts with a <timelapse> Element  
                if ((reader.NodeType == XmlNodeType.Element) && (reader.Name == Constant.VersionXml.Timelapse))
                {
                    // Read the various elements and their associated contents
                    while (reader.Read())
                    {
                        // when we find an element node, we remember its name  
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            elementName = reader.Name;
                        }
                        else
                        {
                            // for text nodes...  
                            if ((reader.NodeType == XmlNodeType.Text) && reader.HasValue)
                            {
                                // we check what the name of the node was  
                                switch (elementName)
                                {
                                    case Constant.VersionXml.Version:
                                        // we keep the version info in xxx.xxx.xxx.xxx format as the Version class does the  parsing for us  
                                        latestVersionNumber = new Version(reader.Value);
                                        break;
                                    case Constant.VersionXml.Url:
                                        url = reader.Value;
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }

            // get the running version  
            Version currentVersionNumber = VersionClient.GetTimelapseCurrentVersionNumber();

            // compare the versions  
            if (currentVersionNumber < latestVersionNumber)
            {
                NewVersionNotification newVersionNotification = new NewVersionNotification(this.window, this.applicationName, currentVersionNumber, latestVersionNumber);

                bool? result = newVersionNotification.ShowDialog();
                if (result == true)
                {
                    // navigate the default web browser to our app homepage (the url comes from the xml content)  
                    Process.Start(url);
                }
            }
            else if (showNoUpdatesMessage)
            {
                MessageBox messageBox = new MessageBox(String.Format("No updates to {0} are available.", this.applicationName), Application.Current.MainWindow);
                messageBox.Message.Reason = String.Format("You a running the latest version of {0}, version: {1}", this.applicationName, currentVersionNumber);
                messageBox.Message.Icon = MessageBoxImage.Information;
                if (this.window != null)
                {
                    messageBox.Owner = this.window;
                }
                messageBox.ShowDialog();
            }
            return true;
        }

        // Return the current timelapse version number
        public static Version GetTimelapseCurrentVersionNumber()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        // returns true if version1 is greater than version2
        public static bool IsVersion1GreaterThanVersion2(string versionNumber1, string versionNumber2)
        {
            Version version1 = new Version(versionNumber1);
            Version version2 = new Version(versionNumber2);
            return version1 > version2;
        }

        public static bool IsVersion1GreaterOrEqualToVersion2(string versionNumber1, string versionNumber2)
        {
            Version version1 = new Version(versionNumber1);
            Version version2 = new Version(versionNumber2);
            return version1 >= version2;
        }
    }
}
