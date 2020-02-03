using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Editor.Util
{
    // This clase reads in the code_template.xml file (the old way that we used to specify the template) 
    // and converts it into a data template database.
    public static class CodeTemplateImporter
    {
        public static void Import(string filePath, TemplateDatabase templateDatabase, out List<string> conversionErrors)
        {
            ThrowIf.IsNullArgument(templateDatabase, nameof(templateDatabase));
            conversionErrors = new List<string>();

            // Collect all the data labels as we come across them, as we have to ensure that a new data label doesn't have the same name as an existing one
            List<string> dataLabels = new List<string>();

            // Load the XML document (the code template file)
            // Follows CA3075  pattern for loading
            XmlDocument xmlDoc = new XmlDocument() { XmlResolver = null };
            System.IO.StringReader sreader = new System.IO.StringReader(File.ReadAllText(filePath));
            XmlReader reader = XmlReader.Create(sreader, new XmlReaderSettings() { XmlResolver = null });
            xmlDoc.Load(reader);

            // merge standard controls which existed in code templates
            // MarkForDeletion and Relative path weren't available in code templates
            // NOTE THAT WE NEED TO UPDATE THIS TO NEWER DELETEFLAG
            XmlNodeList selectedNodes = xmlDoc.SelectNodes(Constant.ImageXml.FilePath); // Convert the File type 
            CodeTemplateImporter.UpdateStandardControl(selectedNodes, templateDatabase, Constant.DatabaseColumn.File, ref conversionErrors, ref dataLabels);

            selectedNodes = xmlDoc.SelectNodes(Constant.ImageXml.FolderPath); // Convert the Folder type
            CodeTemplateImporter.UpdateStandardControl(selectedNodes, templateDatabase, Constant.DatabaseColumn.Folder, ref conversionErrors, ref dataLabels);

            selectedNodes = xmlDoc.SelectNodes(Constant.ImageXml.DatePath); // Convert the Date type
            CodeTemplateImporter.UpdateStandardControl(selectedNodes, templateDatabase, Constant.DatabaseColumn.Date, ref conversionErrors, ref dataLabels);

            selectedNodes = xmlDoc.SelectNodes(Constant.ImageXml.TimePath); // Convert the Time type
            CodeTemplateImporter.UpdateStandardControl(selectedNodes, templateDatabase, Constant.DatabaseColumn.Time, ref conversionErrors, ref dataLabels);

            selectedNodes = xmlDoc.SelectNodes(Constant.ImageXml.ImageQualityPath); // Convert the Image Quality type
            CodeTemplateImporter.UpdateStandardControl(selectedNodes, templateDatabase, Constant.DatabaseColumn.ImageQuality, ref conversionErrors, ref dataLabels);

            // no flag controls to import
            // import notes
            selectedNodes = xmlDoc.SelectNodes(Constant.ImageXml.NotePath);
            for (int index = 0; index < selectedNodes.Count; index++)
            {
                ControlRow note = templateDatabase.AddUserDefinedControl(Constant.Control.Note);
                CodeTemplateImporter.UpdateControl(selectedNodes[index], templateDatabase, Constant.Control.Note, note, ref conversionErrors, ref dataLabels);
            }

            // import choices
            selectedNodes = xmlDoc.SelectNodes(Constant.ImageXml.FixedChoicePath);
            for (int index = 0; index < selectedNodes.Count; index++)
            {
                ControlRow choice = templateDatabase.AddUserDefinedControl(Constant.Control.FixedChoice);
                CodeTemplateImporter.UpdateControl(selectedNodes[index], templateDatabase, Constant.Control.FixedChoice, choice, ref conversionErrors, ref dataLabels);
            }

            // import counters
            selectedNodes = xmlDoc.SelectNodes(Constant.ImageXml.CounterPath);
            for (int index = 0; index < selectedNodes.Count; index++)
            {
                ControlRow counter = templateDatabase.AddUserDefinedControl(Constant.Control.Counter);
                CodeTemplateImporter.UpdateControl(selectedNodes[index], templateDatabase, Constant.Control.Counter, counter, ref conversionErrors, ref dataLabels);
            }
            if (reader != null)
            {
                reader.Dispose();
            }
        }

        private static void UpdateControl(XmlNode selectedNode, TemplateDatabase templateDatabase, string typeWanted, ControlRow control, ref List<string> errorMessages, ref List<string> dataLabels)
        {
            XmlNodeList selectedData = selectedNode.SelectNodes(Constant.ImageXml.Data);
            control.DefaultValue = GetColumn(selectedData, Constant.Control.DefaultValue); // Default
            control.Width = Int32.Parse(GetColumn(selectedData, Constant.Control.TextBoxWidth)); // Width

            // The tempTable should have defaults filled in at this point for labels, datalabels, and tooltips
            // Thus if we just get empty values, we should use those defaults rather than clearing them
            string label = GetColumn(selectedData, Constant.Control.Label);
            if (!String.IsNullOrEmpty(label))
            {
                control.Label = label;
            }

            string controlType = typeWanted;
            if (EditorControls.IsStandardControlType(typeWanted) == false)
            {
                controlType = GetColumn(selectedData, Constant.Control.DataLabel);
                if (String.IsNullOrWhiteSpace(controlType))
                {
                    controlType = label; // If there is no data label, use the label's value into it. 
                }

                // string dataLabel = Regex.Replace(controlType, @"\s+", String.Empty);    // remove any white space that may be there
                string dataLabel = Regex.Replace(controlType, "[^a-zA-Z0-9_]", String.Empty);  // only allow alphanumeric and '_'. 
                if (!dataLabel.Equals(controlType))
                {
                    errorMessages.Add("illicit characters: '" + controlType + "' changed to '" + dataLabel + "'");
                    controlType = dataLabel;
                }

                foreach (string sqlKeyword in EditorConstant.ReservedSqlKeywords)
                {
                    if (String.Equals(sqlKeyword, dataLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        errorMessages.Add("reserved word:    '" + controlType + "' changed to '" + controlType + "_'");
                        controlType += "_";
                        break;
                    }
                }
            }

            // Now set the actual data label

            // First, check to see if the datalabel already exsists in the list, i.e., its not a unique key
            // If it doesn't, keep trying to add an integer to its end to make it unique.
            int j = 0;
            string temp_datalabel = controlType;
            while (dataLabels.Contains(temp_datalabel))
            {
                temp_datalabel = controlType + j.ToString();
            }
            if (!controlType.Equals(temp_datalabel))
            {
                errorMessages.Add("duplicate data label:" + Environment.NewLine + "   '" + controlType + "' changed to '" + temp_datalabel + "'");
                controlType = temp_datalabel;
            }

            if (!String.IsNullOrEmpty(controlType))
            {
                if (controlType.Equals("Delete"))
                {
                    controlType = Constant.ControlDefault.DeleteFlagLabel; // Delete is a reserved word!
                }
                control.DataLabel = controlType;
            }
            else
            {
                // If the data label was empty, the priority is to use the non-empty label contents
                // otherwise we stay with the default contents of the data label filled in previously 
                label = Regex.Replace(label, @"\s+", String.Empty);
                if (!string.IsNullOrEmpty(label))
                {
                    control.DataLabel = label;
                }
            }
            dataLabels.Add(controlType); // and add it to the list of data labels seen

            string tooltip = GetColumn(selectedData, Constant.Control.Tooltip);
            if (!String.IsNullOrEmpty(tooltip))
            {
                control.Tooltip = tooltip;
            }

            // If there is no value supplied for Copyable, default is false for these data types (as they are already filled in by the system). 
            // Counters are also not copyable be default, as we expect counts to change image by image. But there are cases where they user may want to alter this.
            bool defaultCopyable = true;
            if (EditorControls.IsStandardControlType(typeWanted))
            {
                defaultCopyable = false;
            }
            control.Copyable = ConvertToBool(TextFromNode(selectedData, 0, Constant.Control.Copyable), defaultCopyable);

            // If there is no value supplied for Visibility, default is true (i.e., the control will be visible in the interface)
            control.Visible = ConvertToBool(TextFromNode(selectedData, 0, Constant.Control.Visible), true);

            // if the type has a list, we have to do more work.
            if (typeWanted == Constant.DatabaseColumn.ImageQuality)
            {
                // For Image Quality, use the new list (longer than the one in old templates)
                control.List = Constant.ImageQuality.ListOfValues;
            }
            else if (typeWanted == Constant.DatabaseColumn.ImageQuality || typeWanted == Constant.Control.FixedChoice)
            {
                // Load up the menu items
                control.List = String.Empty; // For others, generate the list from what is stored

                XmlNodeList nodes = selectedData[0].SelectNodes(Constant.Control.List + Constant.ImageXml.Slash + Constant.ImageXml.Item);
                bool firstTime = true;
                foreach (XmlNode node in nodes)
                {
                    if (firstTime)
                    {
                        control.List = node.InnerText; // also clears the list's default values
                    }
                    else
                    {
                        control.List += "|" + node.InnerText;
                    }
                    firstTime = false;
                }
            }

            templateDatabase.SyncControlToDatabase(control);
        }

        private static void UpdateStandardControl(XmlNodeList selectedNodes, TemplateDatabase templateDatabase, string typeWanted, ref List<string> errorMessages, ref List<string> dataLabels)
        {
            Debug.Assert(selectedNodes != null && selectedNodes.Count == 1, "Row update is supported for only a single XML element.");

            // assume the database is well formed and contains only a single row of the given standard type
            foreach (ControlRow control in templateDatabase.Controls)
            {
                if (control.Type == typeWanted)
                {
                    CodeTemplateImporter.UpdateControl(selectedNodes[0], templateDatabase, typeWanted, control, ref errorMessages, ref dataLabels);
                    return;
                }
            }

            throw new ArgumentOutOfRangeException(String.Format("Control of type {0} could not be found in database.", typeWanted));
        }

        // A helper routine to make sure that no values are ever null
        private static string GetColumn(XmlNodeList nodeData, string what)
        {
            string s = TextFromNode(nodeData, 0, what);
            if (s == null)
            {
                s = String.Empty;
            }
            return s;
        }

        // Convert a string to a boolean, where its set to defaultValue if it cannot be converted by its value
        private static bool ConvertToBool(string value, bool defaultValue)
        {
            string s = value.ToLowerInvariant();
            if (s == Constant.BooleanValue.True)
            {
                return true;
            }
            if (s == Constant.BooleanValue.False)
            {
                return false;
            }
            return defaultValue;
        }

        // Given a nodelist, get the text associated with it 
        private static string TextFromNode(XmlNodeList node, int nodeIndex, string nodeToFind)
        {
            XmlNodeList n = node[nodeIndex].SelectNodes(nodeToFind);
            if (n.Count == 0)
            {
                return String.Empty; // The node doesn't exist
            }
            return n[0].InnerText;
        }
    }
}
