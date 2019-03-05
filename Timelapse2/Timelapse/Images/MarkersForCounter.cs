using System;
using System.Collections.Generic;
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
                pointList.AppendFormat("{0:0.000},{1:0.000}", markerForCounter.Position.X, markerForCounter.Position.Y); // Add a point in the form x,y e.g., 0.500, 0.700
            }
            return pointList.ToString();
        }

        public void Parse(string pointList)
        {
            if (String.IsNullOrEmpty(pointList))
            {
                return;
            }

            char[] delimiterBar = { Constant.DatabaseValues.MarkerBar };
            string[] pointsAsStrings = pointList.Split(delimiterBar);
            List<Point> points = new List<Point>();
            foreach (string pointAsString in pointsAsStrings)
            {
                Point point = Point.Parse(pointAsString);
                points.Add(point);
            }

            foreach (Point point in points)
            {
                this.AddMarker(point);  // add the marker to the list
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
