using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using Timelapse.Util;

namespace Timelapse.Images
{
    // A class containing a list of Markers associated with a counter's data label 
    // Each marker represents the coordinates of an entity on the screen being counted
    public class MarkersForCounter
    {
        // The counter's data label
        public string DataLabel { get; private set; }

        // the list of markers associated with the counter
        public List<Marker> Markers { get; private set; }
        
        public MarkersForCounter(string dataLabel)
        {
            this.DataLabel = dataLabel;
            this.Markers = new List<Marker>();
        }

        // Add a Marker to the Marker's list
        public void AddMarker(Marker marker)
        {
            this.Markers.Add(marker);
        }

        // Create a marker with the given point and add it to the marker list
        public void AddMarker(Point point)
        {
            this.AddMarker(new Marker(this.DataLabel, point));
        } 

        public string GetPointList()
        {
            StringBuilder pointList = new StringBuilder();
            foreach (Marker markerForCounter in this.Markers)
            {
                if (pointList.Length > 0)
                {
                    pointList.Append(Constant.DatabaseValues.MarkerBar); // don't put a separator at the beginning of the point list
                }

                // Different cultures specify doubles differently, e.g., 0.100 vs 0,100 which will break things if we try to parse them later.
                // To get around this, format coordinates (which are only used internally and which the user never sees) as an Invariant culture. 
                string x = String.Format(CultureInfo.InvariantCulture, "{0:0.000}", markerForCounter.Position.X);
                string y = String.Format(CultureInfo.InvariantCulture, "{0:0.000}", markerForCounter.Position.Y);
                pointList.AppendFormat("{0},{1}", x,y); // Add a point in the form x,y e.g., 0.500, 0.700
            }
            return pointList.ToString();
        }

        public void Parse(string pointList)
        {
            if (String.IsNullOrEmpty(pointList))
            {
                return;
            }
            try
            {
                char[] delimiterBar = { '|' };
                string[] pointsAsStrings = pointList.Split(delimiterBar);
                List<Point> points = new List<Point>();
                string invariantCulturePoint = String.Empty;
                foreach (string pointAsString in pointsAsStrings)
                {
                    if (pointAsString.Count(f => f == ',') == 3)
                    {
                        // The point was not stored in an invariant culture, i.e., 0,345,0,400 rather than 0.345,0.400
                        // So we have to fix it.
                        // Remove the first comma
                        int index = pointAsString.IndexOf(",");
                        invariantCulturePoint = pointAsString.Remove(index, 1).Insert(index, ".");

                        // Remove the last comma
                        index = invariantCulturePoint.LastIndexOf(",");
                        invariantCulturePoint = invariantCulturePoint.Remove(index, 1).Insert(index, ".");
                    }
                    else
                    {
                        invariantCulturePoint = pointAsString;
                    }
                    Point point = Point.Parse(invariantCulturePoint);
                    points.Add(point);
                }

                foreach (Point point in points)
                {
                    this.AddMarker(point);  // add the marker to the list;
                }
            }
            catch
            {
                // Just in case there is a weird format in the point list.
                // essentially it will add points to the list until the first parsing failure
                return;
            }
        }

        public void RemoveMarker(Marker marker)
        {
            int index = this.Markers.IndexOf(marker);
            if (index == -1)
            { 
                TraceDebug.PrintMessage("RemoveMarker: Expected marker to be present in list, but its not there.");
                return;
            }
            this.Markers.RemoveAt(index);
        }
    }
}
