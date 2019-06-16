using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Timelapse.Detection
{
    // DETECTOR: all in this file only used to read from CSV files, and can be removed if we just import using JSON
    // Routines for importing detection data from CSV
    public static class DetectionDataFromCSV
    {
        public static bool TryImportRecognitionDataFromCsv(string filePath, out Detector detector)
        {
            detector = null;

            int expectedCSVColumns = 3;
            try
            {
                // Check if the data labels contain the required data fields to hold the recognition data, i.e., the MaxConfidence and the Bounding boxes
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (StreamReader csvReader = new StreamReader(stream))
                    {
                        string message = String.Empty;
                        // validate the presence of the required headers in the .csv file 
                        List<string> csvLabelsFromHeader = ReadAndParseLine(csvReader);
                        if (csvLabelsFromHeader.Count != expectedCSVColumns)
                        {
                            message = String.Format("CSV file: should contain only {0} columns.", expectedCSVColumns);
                        }
                        else if (csvLabelsFromHeader.Contains(Constant.Recognition.CSVLabelImagePath) == false)
                        {
                            message = String.Format("CSV file: missing a column named '{0}'.", Constant.Recognition.CSVLabelImagePath);
                        }
                        else if (csvLabelsFromHeader.Contains(Constant.Recognition.CSVLabelMaxConfidence) == false)
                        {
                            message = String.Format("CSV file: missing a column named '{0}'.", Constant.Recognition.CSVLabelMaxConfidence);
                        }
                        if (csvLabelsFromHeader.Contains(Constant.Recognition.CSVLabelBoundingBoxes) == false)
                        {
                            message = String.Format("CSV file: missing a column named '{0}'.", Constant.Recognition.CSVLabelBoundingBoxes);
                        }
                        if (message != string.Empty)
                        {
                            Debug.Print(message);
                            return false;
                        }

                        // At this point, we know that we have matching required datalabels and column headers in the template and csv file
                        detector = new Detector();

                        // CSV detector data files do not (currently have the category or classification defaults set, so just add some arbitrary defaults for now
                        detector.SetDetectionCategoryDefaults();
                        detector.SetDetectionClassificationDefaults();
                        detector.info.SetInfoDefaults();

                        // Get each row from the csv file
                        for (List<string> row = ReadAndParseLine(csvReader); row != null; row = ReadAndParseLine(csvReader))
                        {
                            float maxDetectionConfidence = 0;
                            string file = String.Empty;
                            image image = new image();
                            string boundingBoxes = String.Empty;
                            BoundingBoxes bb = new BoundingBoxes();

                            if (row.Count == expectedCSVColumns - 1)
                            {
                                // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
                                // if the final field has a value this case isn't a concern, but if the final field has no value then there's
                                // no way for the parser to know the exact number of fields in the line
                                row.Add(String.Empty);
                            }
                            else if (row.Count != expectedCSVColumns)
                            {
                                Debug.Print(String.Format("CSV file: expected {0} fields in line {1} but found {2}.", expectedCSVColumns, String.Join(",", row), row.Count));
                                break;
                            }

                            // for each column in each row, assemble the column values to update
                            for (int field = 0; field < row.Count; ++field)
                            {
                                string dataLabel = csvLabelsFromHeader[field];
                                string value = row[field];

                                if (dataLabel == Constant.Recognition.CSVLabelImagePath)
                                {
                                    string folder = String.Empty;
                                    string relativePath = String.Empty;
                                    int index = value.IndexOf("/");
                                    if (index > 0)
                                    {
                                        folder = value.Substring(0, index);
                                        relativePath = System.IO.Path.GetDirectoryName(value.Substring(index + 1));
                                    }
                                    string filename = System.IO.Path.GetFileName(value);
                                    file = Path.Combine(relativePath, filename);
                                }
                                else if (dataLabel == Constant.Recognition.CSVLabelMaxConfidence)
                                {
                                    // Convert the number into a string of the form "#.##"
                                    if (float.TryParse(value, out float maxConfidenceAsFloat))
                                    {
                                        maxDetectionConfidence = maxConfidenceAsFloat;
                                    }
                                }
                                else if (dataLabel == Constant.Recognition.CSVLabelBoundingBoxes)
                                {
                                    // For some reason, the bounding box list can include a trailing quote ("), so we remove it if its there. 
                                    // Not sure why its read in as part of the CSV row...
                                    boundingBoxes = value.Replace("\"", String.Empty);
                                    bb = new BoundingBoxes();
                                    bb.CreatefromRecognitionData(maxDetectionConfidence, boundingBoxes);
                                }
                                else
                                {
                                    Debug.Print("Something went wrong... The CSV file is likely open.");
                                    return false;
                                }
                            } // end foreach column

                            // We should now have all the info
                            image.file = file;
                            image.max_detection_conf = maxDetectionConfidence;

                            foreach (BoundingBox box in bb.Boxes)
                            {
                                detection detection = new detection
                                {
                                    category = "1",
                                    conf = box.Confidence,
                                    bbox = box.Box // new double[]{ box.Box.Left, box.Box.Top, box.Box.Width, box.Box.Height },
                                };
                                detection.classifications.Add(new Object[] { 1, 0.0 });
                                image.detections.Add(detection);
                            }
                            detector.images.Add(image);
                        } // end foreach row
                    }
                }
            }
            catch
            {
                Debug.Print(String.Format("Error in TryImportRecognitionDataFromCsv. Check if the CSV file is opened in another application?"));
                return false;
            }
            return true;
        }

        #region Parsing methods
        private static List<Rect> ParseBoundingBoxes(string sBoundingBoxes)
        {
            List<Rect> boundingBoxes = new List<Rect>();
            return boundingBoxes;
        }
        private static List<string> ReadAndParseLine(StreamReader csvReader)
        {
            string unparsedLine = csvReader.ReadLine();
            if (unparsedLine == null)
            {
                return null;
            }

            List<string> parsedLine = new List<string>();
            bool isFieldEscaped = false;
            int fieldStart = 0;
            bool inField = false;
            for (int index = 0; index < unparsedLine.Length; ++index)
            {
                char currentCharacter = unparsedLine[index];
                if (inField == false)
                {
                    if (currentCharacter == '\"')
                    {
                        // start of escaped field
                        isFieldEscaped = true;
                        fieldStart = index + 1;
                    }
                    else if (currentCharacter == ',')
                    {
                        // empty field
                        // promote null values to empty values to prevent the presence of SQNull objects in data tables
                        // much Timelapse code assumes data table fields can be blindly cast to string and breaks once the data table has been
                        // refreshed after null values are inserted
                        parsedLine.Add(String.Empty);
                        continue;
                    }
                    else
                    {
                        // start of unescaped field
                        fieldStart = index;
                    }

                    inField = true;
                }
                else
                {
                    if (currentCharacter == ',' && isFieldEscaped == false)
                    {
                        // end of unescaped field
                        inField = false;
                        string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                        parsedLine.Add(field);
                    }
                    else if (currentCharacter == '\"' && isFieldEscaped)
                    {
                        // escaped character encountered; check for end of escaped field
                        int nextIndex = index + 1;
                        if (nextIndex < unparsedLine.Length)
                        {
                            if (unparsedLine[nextIndex] == ',')
                            {
                                // end of escaped field
                                // note: Whilst this implementation supports escaping of carriage returns and line feeds on export it does not support them on
                                // import.  This is common in .csv parsers and can be addressed if needed by appending the next line to unparsedLine and 
                                // continuing parsing rather than terminating the field.
                                inField = false;
                                isFieldEscaped = false;
                                string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                                field = field.Replace("\"\"", "\"");
                                parsedLine.Add(field);
                                ++index;
                            }
                            else if (unparsedLine[nextIndex] == '"')
                            {
                                // escaped double quotation mark
                                // just move next to skip over the second quotation mark as replacement back to one quotation mark is done in field extraction
                                ++index;
                            }
                        }
                    }
                }
            }

            // if the last character is a non-comma add the final (non-empty) field
            // final empty fields are ambiguous at this level and therefore handled by the caller
            if (inField)
            {
                string field = unparsedLine.Substring(fieldStart, unparsedLine.Length - fieldStart);
                if (isFieldEscaped)
                {
                    field = field.Replace("\"\"", "\"");
                }
                parsedLine.Add(field);
            }

            return parsedLine;
        }
    }
    #endregion

    #region BoundingBoxes class
    // Bounding Box class maintains a list of Bounding Boxes and confidence values associated with a single image 
    // This is only used by the CSV reader, so can be removed if we just import using JSON
    internal class BoundingBoxes
    {
        // List of Bounding Boxes
        public List<BoundingBox> Boxes { get; private set; }
        public float MaxConfidenceAcrossAllBoxes { get; set; }
        public BoundingBoxes()
        {
            this.Boxes = new List<BoundingBox>();
        }

        public void CreatefromRecognitionData(float maxConfidence, string allDetectedBoxes)
        {
            // The JSON data is structured as a comma-separated list of lists, i.e.
            // [] for an empty list
            // [[x1, y1, width, height, probability]]
            // [[x1, y1,width, height, probability], [x1, y1, width, height, probability]]

            // Check for an empty list. If so, do nothing
            if (allDetectedBoxes == String.Empty || allDetectedBoxes == "[]")
            {
                this.MaxConfidenceAcrossAllBoxes = 0;
                return;
            }

            this.MaxConfidenceAcrossAllBoxes = maxConfidence;

            // Strip out the first and last '[]'
            allDetectedBoxes = this.StripBraces(allDetectedBoxes);

            // cycle through each list to fill in the bounding boxes
            String[] arrayOfDetectedBoxes = allDetectedBoxes.Split(new string[] { "], [" }, StringSplitOptions.None);
            foreach (string str in arrayOfDetectedBoxes)
            {
                // The split above still keeps some braces, so strip them as needed

                // Get the individual parameters
                string[] bbox_parametersAsStringArray = this.StripBraces(str).Split(new string[] { ", " }, StringSplitOptions.None);
                List<float> bbox_parameters = new List<float>();
                foreach (string parameter in bbox_parametersAsStringArray)
                {
                    if (float.TryParse(parameter, out float floatValue))
                    {
                        bbox_parameters.Add(floatValue);
                    }
                    else
                    {
                        System.Diagnostics.Debug.Print("CreateFromRecognitionData: Error - parsing  float from string");
                    }
                }

                if (bbox_parameters.Count == 5)
                {
                    this.Boxes.Add(new BoundingBox(bbox_parameters[0], bbox_parameters[1], bbox_parameters[2], bbox_parameters[3], bbox_parameters[4]));
                }
                else
                {
                    System.Diagnostics.Debug.Print("CreateFromRecognitionData: Error - parameter count is not 5");
                }
            }
        }

        private string StripBraces(string str)
        {
            if (str.IndexOf("[") == 0)
            {
                str = str.Remove(0, 1);
            }
            if (str.LastIndexOf("]") == str.Length - 1)
            {
                str = str.Remove(str.Length - 1, 1);
            }
            return str;
        }
    }
    #endregion

    #region BoundingBox class
    // A BoundingBox instance contains data describing a bounding box's appearance and the data associated with that bounding box.
    // This is only used by the CSV reader, so can be removed if we just import using JSON
    internal class BoundingBox
    {
        // Gets or sets the bounding box's normalized location in the canvas, as a relative rectangle .
        public double[] Box { get; set; }

        /// <summary>
        /// Gets or sets the confidence of this bounding box.
        /// </summary>
        public float Confidence { get; set; }

        public BoundingBox()
        {
            this.Box = new double[4];
            this.Confidence = 0;
        }

        public BoundingBox(float x1, float y1, float width, float height, float confidence)
        {
            this.Box = new double[] { x1, y1, width, height };
            this.Confidence = confidence;
        }
    }
    #endregion
}

