using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Images
{
    internal class ImageDataXml
    {
        // Read all the data into the imageData structure from the XML file in the filepath.
        // Note that we need to know the code controls,as we have to associate any points read in with a particular counter control
        public static void Read(string filePath, string xmlDataFileName, FileDatabase imageDatabase)
        {
            // XML Preparation
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            // Import the old log (if any)
            XmlNodeList logNodes = xmlDoc.SelectNodes(Constant.ImageXml.Images + Constant.ImageXml.Slash + Constant.DatabaseColumn.Log);
            if (logNodes.Count > 0)
            {
                XmlNode logNode = logNodes[0];
                imageDatabase.ImageSet.Log = logNode.InnerText;
                imageDatabase.SyncImageSetToDatabase();
            }

            // Create three lists, each one representing the datalabels (in order found in the template) of notes, counters and choices
            // We will use these to find the matching ones in the xml data table.
            List<string> noteControlNames = new List<string>();
            List<string> counterControlNames = new List<string>();
            List<string> choiceControlNames = new List<string>();
            foreach (ControlRow control in imageDatabase.Controls)
            {
                // Note that code should be modified to  deal with flag controls 
                switch (control.Type)
                {
                    case Constant.Control.Counter:
                        counterControlNames.Add(control.DataLabel);
                        break;
                    case Constant.Control.FixedChoice:
                        choiceControlNames.Add(control.DataLabel);
                        break;
                    case Constant.Control.Note:
                        noteControlNames.Add(control.DataLabel);
                        break;
                    default:
                        
                        break;
                }
            }

            XmlNodeList nodeList = xmlDoc.SelectNodes(Constant.ImageXml.Images + Constant.ImageXml.Slash + Constant.DatabaseColumn.Image);
            int imageID = 0;
            TimeZoneInfo imageSetTimeZone = imageDatabase.ImageSet.GetTimeZone();
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            List<ColumnTuplesWithWhere> markersToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (XmlNode node in nodeList)
            {
                imageID++;

                // We ignore:
                // - Folder and Relative path, as the new template will have the correct values
                // - ImageQuality, as the new Timelapse version probably has a better determination of it
                // - DeleteFlag, as the old-style xml templates didn't have them
                // - Flags as the old-style xml templates didn't have them

                List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>(); // Populate the data 
                // File Field - We use the file name as a key into a particular database row. We don't change the database field as it is our key.
                string imageFileName = node[Constant.ImageXml.File].InnerText;

                // If the Folder Path differs from where we had previously loaded it, 
                // warn the user that the new path will be substituted in its place
                // This gets the folderName in the Xml file, but we still ahve to get the folderName as it currently exists.
                // string folderName = node[Constant.ImageXml.Folder].InnerText;
              
                // Date - We use the original date, as the analyst may have adjusted them 
                string date = node[Constant.ImageXml.Date].InnerText;
                columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.Date, date));

                // Date - We use the original time, although its almost certainly identical
                string time = node[Constant.ImageXml.Time].InnerText;
                columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.Time, time));

                // DateTime
                DateTimeOffset dateTime;
                if (DateTimeHandler.TryParseLegacyDateTime(date, time, imageSetTimeZone, out dateTime))
                {
                    columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.DateTime, dateTime.UtcDateTime));
                    columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.UtcOffset, dateTime.Offset));
                }

                // Notes: Iterate through 
                int innerNodeIndex = 0;
                XmlNodeList innerNodeList = node.SelectNodes(Constant.Control.Note);
                foreach (XmlNode innerNode in innerNodeList)
                {
                    System.Diagnostics.Debug.Print("Note: " + noteControlNames[innerNodeIndex] + " | " + innerNode.InnerText);
                    columnsToUpdate.Add(new ColumnTuple(noteControlNames[innerNodeIndex++], innerNode.InnerText));
                }

                // Choices: Iterate through 
                innerNodeIndex = 0;
                innerNodeList = node.SelectNodes(Constant.Control.FixedChoice);
                foreach (XmlNode innerNode in innerNodeList)
                {
                    System.Diagnostics.Debug.Print("Choice: " + choiceControlNames[innerNodeIndex] + " | " + innerNode.InnerText);
                    columnsToUpdate.Add(new ColumnTuple(choiceControlNames[innerNodeIndex++], innerNode.InnerText));
                }

                // Counters: Iterate through  
                List <ColumnTuple> counterCoordinates = new List<ColumnTuple>();
                innerNodeIndex = 0;
                innerNodeList = node.SelectNodes(Constant.Control.Counter);
                string where = String.Empty;
                foreach (XmlNode innerNode in innerNodeList)
                {
                    // Add the value of each counter to the dataline 
                    XmlNodeList dataNode = innerNode.SelectNodes(Constant.DatabaseColumn.Data);
                    System.Diagnostics.Debug.Print("Counter: " + counterControlNames[innerNodeIndex] + " | " + dataNode[0].InnerText);
                    columnsToUpdate.Add(new ColumnTuple(counterControlNames[innerNodeIndex], dataNode[0].InnerText));

                    // For each counter, find the points associated with it and compose them together as x1,y1|x2,y2|...|xn,yn 
                    XmlNodeList pointNodeList = innerNode.SelectNodes(Constant.DatabaseColumn.Point);
                    string countercoord = String.Empty;
                    foreach (XmlNode pointNode in pointNodeList)
                    {
                        String x = pointNode.SelectSingleNode(Constant.DatabaseColumn.X).InnerText;
                        if (x.Length > 5)
                        {
                            x = x.Substring(0, 5);
                        }
                        String y = pointNode.SelectSingleNode(Constant.DatabaseColumn.Y).InnerText;
                        if (y.Length > 5)
                        {
                            y = y.Substring(0, 5);
                        }
                        countercoord += x + "," + y + "|";
                    }

                    // Remove the last "|" from the point list
                    if (!countercoord.Equals(String.Empty))
                    {
                        countercoord = countercoord.Remove(countercoord.Length - 1); // Remove the last "|"
                    }

                    // Countercoords will have a list of points (possibly empty) with each list entry representing a control
                    counterCoordinates.Add(new ColumnTuple(counterControlNames[innerNodeIndex], countercoord));
                    innerNodeIndex++;
                }

                // add this image's updates to the update lists
                ColumnTuplesWithWhere imageToUpdate = new ColumnTuplesWithWhere(columnsToUpdate);
                // Since TImelapse1 didn't have relative paths, we only need to set Where using the image filename 
                //imageToUpdate.SetWhere(currentFolderName, null, imageFileName); //<- replaced by the simpler SetWhere form below
                imageToUpdate.SetWhere(imageFileName);
                imagesToUpdate.Add(imageToUpdate);

                ColumnTuplesWithWhere markerToUpdate = new ColumnTuplesWithWhere(counterCoordinates, imageID);
                markersToUpdate.Add(markerToUpdate);
            }

            // batch update both tables
            imageDatabase.UpdateFiles(imagesToUpdate);
            imageDatabase.UpdateMarkers(markersToUpdate);
        }
    }
}
