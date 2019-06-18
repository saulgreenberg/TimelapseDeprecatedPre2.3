using System;
using System.Data;
using System.Windows;
using Timelapse.Database;
using Timelapse.Detection;
using BoundingBoxes = Timelapse.Images.BoundingBoxes;
using BoundingBox = Timelapse.Images.BoundingBox;
using Timelapse.Controls;

namespace Timelapse
{
    public partial class TimelapseWindow : Window, IDisposable
    {
        // for each image, get a list of detections and fill in the bounding box information for it. 
        // ADD TEST FOR MULTIPLE CLICKABLE IMAGE GRID AND FOR VIDEO
        public BoundingBoxes GetBoundingBoxesForCurrentFile(long fileID)
        {
            BoundingBoxes bboxes = new BoundingBoxes();
            if (this.dataHandler.FileDatabase.TableExists(Constant.DBTableNames.Detections))
            {
                DataTable dataTable = this.dataHandler.FileDatabase.GetDetectionsFromFileID(fileID);
                foreach (DataRow row in dataTable.Rows)
                {
                    string coords = (string)row[3];
                    if (coords == String.Empty)
                    {
                        // This shouldn't happen, but...
                        continue;
                    }
                    float confidence = float.Parse(row[2].ToString());
                    string category = (string)row[1];
                    // Determine the maximum confidence of these detections
                    if (bboxes.MaxConfidence < confidence)
                    {
                        bboxes.MaxConfidence = confidence;
                    }
                    BoundingBox box = new BoundingBox((string)row[3], confidence, category);
                    bboxes.Boxes.Add(box);
                }
            }
            return bboxes;
        }
        // END BOundingBoxes
    }
    }
