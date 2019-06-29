using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Timelapse.Database;

namespace Timelapse.Detection
{
    public static class DetectionDatabases
    {
        // Create all Detection Database Tables
        public static void CreateOrRecreateTablesAndColumns(SQLiteWrapper database)
        {
            List<ColumnDefinition> columnDefinitions;
            // Create the various tables used to hold detection data

           // Info: Create or clear table 
            if (database.TableExists(Constant.DBTableNames.Info))
            {
                // all the data tables were previously created. 
                // So just clear their contents as there is no need to create them
                List<string> tableList = new List<string>
                {
                   Constant.DBTableNames.Info,
                   Constant.DBTableNames.DetectionCategories,
                   Constant.DBTableNames.ClassificationCategories,
                   Constant.DBTableNames.Detections,
                   Constant.DBTableNames.Classifications
                };
                database.ClearRowsInTables(tableList);
                return;
            }
            columnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition(Constant.InfoColumns.InfoID, Timelapse.Constant.Sqlite.Integer + Timelapse.Constant.Sqlite.PrimaryKey), // Primary Key
                new ColumnDefinition(Constant.InfoColumns.Detector,  Constant.Sqlite.String),
                new ColumnDefinition(Constant.InfoColumns.DetectionCompletionTime,  Constant.Sqlite.String),
                new ColumnDefinition(Constant.InfoColumns.Classifier,  Constant.Sqlite.String),
                new ColumnDefinition(Constant.InfoColumns.ClassificationCompletionTime,  Constant.Sqlite.String)
            };
            database.CreateTable(Constant.DBTableNames.Info, columnDefinitions);

            // DetectionCategories: create or clear table 
            columnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition(Constant.DetectionCategoriesColumns.Category,  Constant.Sqlite.String + Timelapse.Constant.Sqlite.PrimaryKey), // Primary Key
                new ColumnDefinition(Constant.DetectionCategoriesColumns.Label,  Constant.Sqlite.String),
            };
            database.CreateTable(Constant.DBTableNames.DetectionCategories, columnDefinitions);

            // ClassificationCategories: create or clear table 
            columnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition(Constant.ClassificationCategoriesColumns.Category,  Constant.Sqlite.String + Timelapse.Constant.Sqlite.PrimaryKey), // Primary Key
                new ColumnDefinition(Constant.ClassificationCategoriesColumns.Label,  Constant.Sqlite.String),
            };
            database.CreateTable(Constant.DBTableNames.ClassificationCategories, columnDefinitions);

            // Detections: create or clear table 
            columnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition(Constant.DetectionColumns.DetectionID, Timelapse.Constant.Sqlite.Integer + Timelapse.Constant.Sqlite.PrimaryKey),
                new ColumnDefinition(Constant.DetectionColumns.Category,  Constant.Sqlite.String),
                new ColumnDefinition(Constant.DetectionColumns.Conf,  Constant.Sqlite.Real),
                new ColumnDefinition(Constant.DetectionColumns.BBox,  Constant.Sqlite.String), // Will need to parse it into new new double[4]
                new ColumnDefinition(Constant.DetectionColumns.ImageID, Timelapse.Constant.Sqlite.Integer), // Foreign key: ImageID
                new ColumnDefinition("FOREIGN KEY ( " + Constant.DetectionColumns.ImageID + " )", "REFERENCES " + Constant.DatabaseTable.FileData + " ( " + Constant.DetectionColumns.ImageID + " ) " + " ON DELETE CASCADE "),
            };
            database.CreateTable(Constant.DBTableNames.Detections, columnDefinitions);

            // Classifications: create or clear table 
            columnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition(Constant.ClassificationColumns.ClassificationID, Timelapse.Constant.Sqlite.Integer + Timelapse.Constant.Sqlite.PrimaryKey),
                new ColumnDefinition(Constant.ClassificationColumns.Category, Constant.Sqlite.String),
                new ColumnDefinition(Constant.ClassificationColumns.Conf,  Constant.Sqlite.Real),
                new ColumnDefinition(Constant.ClassificationColumns.DetectionID, Timelapse.Constant.Sqlite.Integer), // Foreign key: ImageID
                new ColumnDefinition("FOREIGN KEY ( " + Constant.ClassificationColumns.DetectionID + " )", "REFERENCES " + Constant.DBTableNames.Detections + " ( " + Constant.ClassificationColumns.DetectionID + " ) " + " ON DELETE CASCADE "),
            };
            database.CreateTable(Constant.DBTableNames.Classifications, columnDefinitions);
        }

        public static void ClearDetectionTables(SQLiteWrapper database)
        {
            List<string> detectionTables = new List<string>
            {
                Constant.DBTableNames.ClassificationCategories,
                Constant.DBTableNames.Classifications,
                Constant.DBTableNames.DetectionCategories,
                Constant.DBTableNames.Detections
            };
            database.ClearRowsInTables(detectionTables);
        }

        // Populate the various Detection Database Tables from the detection data structure.
        public static void PopulateTables(Detector detector, FileDatabase fileDatabase, SQLiteWrapper detectionDB, string pathPrefixForTruncation)
        {
            // Updating many rows is made hugely more efficient if we create an index for File and Relative Path
            // as otherwise each update is in linear time to the table rows vs log time. 
            // Because we will not need these indexes later, we will drop them after the updates are done

            // Info Table: Populate
            List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>
            {
                new ColumnTuple(Constant.InfoColumns.InfoID, 1),
                new ColumnTuple(Constant.InfoColumns.Detector, detector.info.detector),
                new ColumnTuple(Constant.InfoColumns.DetectionCompletionTime, detector.info.detection_completion_time),
                new ColumnTuple(Constant.InfoColumns.Classifier, detector.info.classifier),
                new ColumnTuple(Constant.InfoColumns.ClassificationCompletionTime, detector.info.classification_completion_time)
            };
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>
            {
                columnsToUpdate
            };
            detectionDB.Insert(Constant.DBTableNames.Info, insertionStatements);

            // DetectionCategories:  Populate
            if (detector.detection_categories != null || detector.detection_categories.Count > 0)
            {
                bool emptyCategoryExists = false;
                insertionStatements = new List<List<ColumnTuple>>();
                foreach (KeyValuePair<string, string> detection_category in detector.detection_categories)
                {
                    if (detection_category.Key == Constant.DetectionValues.NoDetectionCategory)
                    {
                        emptyCategoryExists = true;
                    }
                    // Populate each detection category row
                    columnsToUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.DetectionCategoriesColumns.Category, detection_category.Key),
                        new ColumnTuple(Constant.DetectionCategoriesColumns.Label, detection_category.Value)
                    };
                    insertionStatements.Add(columnsToUpdate);
                }
                if (emptyCategoryExists == false)
                {
                    // If its not defined, include the category '0' for Empty i.e., no detections.
                    columnsToUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.DetectionCategoriesColumns.Category, Constant.DetectionValues.NoDetectionCategory),
                        new ColumnTuple(Constant.DetectionCategoriesColumns.Label, Constant.DetectionValues.NoDetectionLabel)
                    };
                    insertionStatements.Insert(0, columnsToUpdate);
                }
                detectionDB.Insert(Constant.DBTableNames.DetectionCategories, insertionStatements);
            }

            // ClassificationCategories:  Populate
            if (detector.classification_categories != null && detector.classification_categories.Count > 0)
            {
                insertionStatements = new List<List<ColumnTuple>>();
                foreach (KeyValuePair<string, string> classification_category in detector.classification_categories)
                {
                    // Populate each classification category row
                    columnsToUpdate = new List<ColumnTuple>
                    {
                        new ColumnTuple(Constant.ClassificationCategoriesColumns.Category, classification_category.Key),
                        new ColumnTuple(Constant.ClassificationCategoriesColumns.Label, classification_category.Value)
                    };
                    insertionStatements.Add(columnsToUpdate);
                }
                detectionDB.Insert(Constant.DBTableNames.ClassificationCategories, insertionStatements);
            }

            // Images and Detections:  Populate
            if (detector.images != null && detector.images.Count > 0)
            {
                int detectionIndex = 1;
                int classificationIndex = 1;
                List<List<ColumnTuple>> detectionInsertionStatements = new List<List<ColumnTuple>>();
                List<List<ColumnTuple>> classificationInsertionStatements = new List<List<ColumnTuple>>();

                // Get a data table containing the ID, RelativePath, and File
                // and create primary keys for the fields we will search for (for performance speedup)
                // We will use that to search for the file index.
                string query = Constant.Sqlite.Select + Constant.DatabaseColumn.ID + "," + Constant.DatabaseColumn.RelativePath + "," + Constant.DatabaseColumn.File + Constant.Sqlite.From + Constant.DatabaseTable.FileData;
                DataTable dataTable = detectionDB.GetDataTableFromSelect(query);
                dataTable.PrimaryKey = new DataColumn[] 
                {
                    dataTable.Columns[Constant.DatabaseColumn.File],
                    dataTable.Columns[Constant.DatabaseColumn.RelativePath],
                };

                int foocount = 0;
                foreach (image image in detector.images)
                {
                    // The truncation prefix is a prefix of the folder path that should be removed from the file path (unless its empty, of course)
                    // As well, detections whose path is in the prefix should not be read in, as they are outside of this sub-folder
                    // It occurs when the actual images were in a subfolder, where that subfolder was read in separately as a datafile
                    // That is, the .tdb file was located in an image subfolder, rather than in the root folder where the detections were done
                    string imageFile = String.Empty;
                    if (pathPrefixForTruncation == String.Empty)
                    {
                        imageFile = image.file;
                    }
                    else
                    {
                        if (image.file.StartsWith(pathPrefixForTruncation) == false)
                        {
                            // Skip images that start with the truncation string, as these are outside of the image set
                            // System.Diagnostics.Debug.Print("Skipping: " + image.file);
                            continue;
                        }
                        else
                        {
                            // Remove the trunctation prefex from the file path 
                            imageFile = image.file.Substring(pathPrefixForTruncation.Length);
                            //System.Diagnostics.Debug.Print("Using: " + image.file + " as " + imageFile);
                        }
                    }
                    string queryFileRelativePath = String.Format("{0} = '{1}' AND {2} = '{3}'",
                         Constant.DatabaseColumn.File,
                         Path.GetFileName(imageFile),
                         Constant.DatabaseColumn.RelativePath,
                         Path.GetDirectoryName(imageFile));

                    DataRow[] rows = dataTable.Select(queryFileRelativePath);
                    if (rows.Count() == 0)
                    {
                        // Couldn't find the image. This could happen if that image and its data was deleted.
                        // This isn't a bug, as all we would do is skip that image.
                        // System.Diagnostics.Debug.Print("Could not find: " + image.file);
                        continue;
                    }

                    // Get the image id from the image
                    // If we can't, just skip it (this should not happen)
                    if (Int32.TryParse(rows[0][0].ToString(), out int id))
                    {
                        image.imageID = id;
                    }
                    else
                    {
                        System.Diagnostics.Debug.Print("Invalid index: " + rows[0][0].ToString());
                        continue;
                    }
                    
                    // Populate the detections table per image.
                    // If there are no detections, we populate it with values that indicate that.
                    if (image.detections.Count == 0)
                    {
                        string bboxAsString = String.Empty;
                        List<ColumnTuple> detectionColumnsToUpdate = new List<ColumnTuple>()
                        {
                                new ColumnTuple(Constant.DetectionColumns.DetectionID, detectionIndex++),
                                new ColumnTuple(Constant.DetectionColumns.ImageID, image.imageID),
                                new ColumnTuple(Constant.DetectionColumns.Category, Constant.DetectionValues.NoDetectionCategory),
                                new ColumnTuple(Constant.DetectionColumns.Conf, 0),
                                new ColumnTuple(Constant.DetectionColumns.BBox, bboxAsString),
                        };
                        detectionInsertionStatements.Add(detectionColumnsToUpdate);
                    }
                    else
                    {
                        foreach (detection detection in image.detections)
                        {
                            // Populate each classification category row
                            string bboxAsString = (detection.bbox == null || detection.bbox.Length != 4)
                                ? String.Empty
                                : String.Format("{0}, {1}, {2}, {3}", detection.bbox[0], detection.bbox[1], detection.bbox[2], detection.bbox[3]);
                            detection.detectionID = detectionIndex;
                            List<ColumnTuple> detectionColumnsToUpdate = new List<ColumnTuple>()
                            {
                                new ColumnTuple(Constant.DetectionColumns.DetectionID, detection.detectionID),
                                new ColumnTuple(Constant.DetectionColumns.ImageID, image.imageID),
                                new ColumnTuple(Constant.DetectionColumns.Category, detection.category),
                                new ColumnTuple(Constant.DetectionColumns.Conf, detection.conf),
                                new ColumnTuple(Constant.DetectionColumns.BBox, bboxAsString),
                            };
                            // System.Diagnostics.Debug.Print("id:"+image.imageID.ToString());
                            detectionInsertionStatements.Add(detectionColumnsToUpdate);

                            // If the detection has some classification info, then add that to the classifications data table
                            foreach (Object[] classification in detection.classifications)
                            {
                                string category = (string)classification[0];
                                double conf = Double.Parse(classification[1].ToString());
                                // System.Diagnostics.Debug.Print(String.Format("{0} {1} {2}", detection.detectionID, category, conf));
                                List<ColumnTuple> classificationColumnsToUpdate = new List<ColumnTuple>()
                                {
                                    new ColumnTuple(Constant.ClassificationColumns.ClassificationID, classificationIndex),
                                    new ColumnTuple(Constant.ClassificationColumns.DetectionID, detection.detectionID),
                                    new ColumnTuple(Constant.ClassificationColumns.Category, (string)classification[0]),
                                    new ColumnTuple(Constant.ClassificationColumns.Conf, (float)Double.Parse(classification[1].ToString())),
                                };
                                classificationInsertionStatements.Add(classificationColumnsToUpdate);
                                classificationIndex++;
                            }
                            detectionIndex++;
                        }
                    }
                    foocount++;
                }
                detectionDB.Insert(Constant.DBTableNames.Detections, detectionInsertionStatements);
                detectionDB.Insert(Constant.DBTableNames.Classifications, classificationInsertionStatements);
                fileDatabase.IndexCreateForDetectionsAndClassifications();
                System.Diagnostics.Debug.Print("Files: " + foocount + " Detections: " + detectionInsertionStatements.Count() + " Classifications: " + classificationInsertionStatements.Count());
            }
        }
    }
}