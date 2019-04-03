using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace Timelapse.Images
{
    public class BoundingBoxes
    {
        // List of Bounding Boxes associated with the image
        public List<BoundingBox> Boxes { get; private set; }
        public BoundingBoxes()
        {
            this.Boxes = new List<BoundingBox>();
        }

        public void CreatefromRecognitionData(string predictedBoxes)
        {
            // The JSON data is structured as a comma-separated list of lists, i.e.
            // [] for an empty list
            // [[x1, y1, x2, y2, probability]]
            // [[x1, y1, x2, y2, probability], [x1, y1, x2, y2, probability]]

            if (predictedBoxes == String.Empty || predictedBoxes == "[]")
            {
                // empty list, so do nothing
                return;
            }

            // Strip out the first and last '[]'
            predictedBoxes = StripBraces(predictedBoxes);

            // cycle through each list to fill in the bounding boxes
            String[] lists = predictedBoxes.Split(new string[] { "], [" }, StringSplitOptions.None);
            foreach (string str in lists)
            {
                // The split above still keeps some braces, so strip them as needed

                // Get the individual parameters
                string[] bbox_parametersAsString = StripBraces(str).Split(new string[] { ", " }, StringSplitOptions.None);
                List<float> bbox_parameters = new List<float>();
                foreach (string parameter in bbox_parametersAsString)
                {
                    if (float.TryParse(parameter, out float value))
                    {
                        bbox_parameters.Add(value);
                    }
                    else
                    {
                        System.Diagnostics.Debug.Print("Something went wrong in parsing the float from string");
                    }
                }

                if (bbox_parameters.Count == 5)
                { 
                    this.Boxes.Add(new BoundingBox(bbox_parameters[0], bbox_parameters[1], bbox_parameters[2], bbox_parameters[3], bbox_parameters[4]));
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
}
