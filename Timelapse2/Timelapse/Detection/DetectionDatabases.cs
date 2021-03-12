﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse.Detection
{
    public static class DetectionDatabases
    {
        // IMMEDIATE Check that image and classifications use foreign keys
        #region Public: Create all Detection Database Tables
        public static void CreateOrRecreateTablesAndColumns(SQLiteWrapper database)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            List<SchemaColumnDefinition> columnDefinitions;
            // Create the various tables used to hold detection data

            // Info: Create or clear table 
            if (database.TableExists(Constant.DBTables.Info))
            {
                // all the data tables were previously created. 
                // So just clear their contents as there is no need to create them
                List<string> tableList = new List<string>
                {
                   Constant.DBTables.Info,
                   Constant.DBTables.DetectionCategories,
                   Constant.DBTables.ClassificationCategories,
                   Constant.DBTables.Detections,
                   Constant.DBTables.Classifications
                };
                database.DeleteAllRowsInTables(tableList);
                return;
            }
            columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.InfoColumns.InfoID, Timelapse.Sql.IntegerType + Timelapse.Sql.PrimaryKey), // Primary Key
                new SchemaColumnDefinition(Constant.InfoColumns.Detector,  Sql.StringType),
                new SchemaColumnDefinition(Constant.InfoColumns.DetectionCompletionTime,  Sql.StringType),
                new SchemaColumnDefinition(Constant.InfoColumns.Classifier,  Sql.StringType),
                new SchemaColumnDefinition(Constant.InfoColumns.ClassificationCompletionTime,  Sql.StringType)
            };
            database.CreateTable(Constant.DBTables.Info, columnDefinitions);

            // DetectionCategories: create or clear table 
            columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DetectionCategoriesColumns.Category,  Sql.StringType + Timelapse.Sql.PrimaryKey), // Primary Key
                new SchemaColumnDefinition(Constant.DetectionCategoriesColumns.Label,  Sql.StringType),
            };
            database.CreateTable(Constant.DBTables.DetectionCategories, columnDefinitions);

            // ClassificationCategories: create or clear table 
            columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.ClassificationCategoriesColumns.Category,  Sql.StringType + Timelapse.Sql.PrimaryKey), // Primary Key
                new SchemaColumnDefinition(Constant.ClassificationCategoriesColumns.Label,  Sql.StringType),
            };
            database.CreateTable(Constant.DBTables.ClassificationCategories, columnDefinitions);

            // Detections: create or clear table 
            columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.DetectionColumns.DetectionID, Timelapse.Sql.IntegerType + Timelapse.Sql.PrimaryKey),
                new SchemaColumnDefinition(Constant.DetectionColumns.Category,  Sql.StringType),
                new SchemaColumnDefinition(Constant.DetectionColumns.Conf,  Sql.Real),
                new SchemaColumnDefinition(Constant.DetectionColumns.BBox,  Sql.StringType), // Will need to parse it into new new double[4]
                new SchemaColumnDefinition(Constant.DetectionColumns.ImageID, Timelapse.Sql.IntegerType), // Foreign key: ImageID
                new SchemaColumnDefinition("FOREIGN KEY ( " + Constant.DetectionColumns.ImageID + " )", "REFERENCES " + Constant.DBTables.FileData + " ( " + Constant.DetectionColumns.ImageID + " ) " + " ON DELETE CASCADE "),
            };
            database.CreateTable(Constant.DBTables.Detections, columnDefinitions);

            // Classifications: create or clear table 
            columnDefinitions = new List<SchemaColumnDefinition>
            {
                new SchemaColumnDefinition(Constant.ClassificationColumns.ClassificationID, Timelapse.Sql.IntegerType + Timelapse.Sql.PrimaryKey),
                new SchemaColumnDefinition(Constant.ClassificationColumns.Category, Sql.StringType),
                new SchemaColumnDefinition(Constant.ClassificationColumns.Conf,  Sql.Real),
                new SchemaColumnDefinition(Constant.ClassificationColumns.DetectionID, Timelapse.Sql.IntegerType), // Foreign key: ImageID
                new SchemaColumnDefinition("FOREIGN KEY ( " + Constant.ClassificationColumns.DetectionID + " )", "REFERENCES " + Constant.DBTables.Detections + " ( " + Constant.ClassificationColumns.DetectionID + " ) " + " ON DELETE CASCADE "),
            };
            database.CreateTable(Constant.DBTables.Classifications, columnDefinitions);
        }
        #endregion

        #region Public: Clear Detection Tables
        public static void ClearDetectionTables(SQLiteWrapper database)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(database, nameof(database));

            List<string> detectionTables = new List<string>
            {
                Constant.DBTables.ClassificationCategories,
                Constant.DBTables.Classifications,
                Constant.DBTables.DetectionCategories,
                Constant.DBTables.Detections
            };
            database.DeleteAllRowsInTables(detectionTables);
        }
        #endregion

        #region Public: Populate Detection Tables
        // Populate the various Detection Database Tables from the detection data structure.
        public static void PopulateTables(Detector detector, FileDatabase fileDatabase, SQLiteWrapper detectionDB, string pathPrefixForTruncation)
        {
            // Check the arguments for null 
            ThrowIf.IsNullArgument(detector, nameof(detector));
            ThrowIf.IsNullArgument(fileDatabase, nameof(fileDatabase));
            ThrowIf.IsNullArgument(detectionDB, nameof(detectionDB));
            ThrowIf.IsNullArgument(pathPrefixForTruncation, nameof(pathPrefixForTruncation));


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
            detectionDB.Insert(Constant.DBTables.Info, insertionStatements);

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
                detectionDB.Insert(Constant.DBTables.DetectionCategories, insertionStatements);
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
                detectionDB.Insert(Constant.DBTables.ClassificationCategories, insertionStatements);
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
                string query = Sql.Select + Constant.DatabaseColumn.ID + Sql.Comma + Constant.DatabaseColumn.RelativePath + Sql.Comma + Constant.DatabaseColumn.File + Sql.From + Constant.DBTables.FileData;
                DataTable dataTable = detectionDB.GetDataTableFromSelect(query);
                dataTable.PrimaryKey = new DataColumn[]
                {
                    dataTable.Columns[Constant.DatabaseColumn.File],
                    dataTable.Columns[Constant.DatabaseColumn.RelativePath],
                };

                int fileCount = 0;
                foreach (image image in detector.images)
                {
                    if (image.detections == null)
                    {
                        // The json file may actualy report some detections as null rather than an empty list, in which case we just skip it.
                        continue;
                    }
                    // The truncation prefix is a prefix of the folder path that should be removed from the file path (unless its empty, of course)
                    // As well, detections whose path is in the prefix should not be read in, as they are outside of this sub-folder
                    // It occurs when the actual images were in a subfolder, where that subfolder was read in separately as a datafile
                    // That is, the .tdb file was located in an image subfolder, rather than in the root folder where the detections were done
                    string imageFile = String.Empty;
                    if (string.IsNullOrEmpty(pathPrefixForTruncation))
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
                            // System.Diagnostics.Debug.Print("Using: " + image.file + " as " + imageFile);
                        }
                    }

                    // Form: FILE = Filename AND RELATIVEPATH = RelativePath
                    string queryFileRelativePath =
                         Constant.DatabaseColumn.File + Sql.Equal + Sql.Quote(Path.GetFileName(imageFile)) +
                         Sql.And +
                         Constant.DatabaseColumn.RelativePath + Sql.Equal + Sql.Quote(Path.GetDirectoryName(imageFile));

                    DataRow[] rows = dataTable.Select(queryFileRelativePath);
                    if (rows.Length == 0)
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
                    bool noDetectionsIncluded = true;
                    if (image.detections.Count > 0)
                    {
                        foreach (detection detection in image.detections)
                        {
                            if (detection.conf < Constant.DetectionValues.MinimumDetectionValue)
                            {
                                // Timelapse enforces a minimum detection confidence. That is, any value less than the MinimumDetectionValue 
                                // is automatically thrown away
                                continue;
                            }

                            // Populate each classification category row
                            string bboxAsString = (detection.bbox == null || detection.bbox.Length != 4)
                                ? String.Empty
                                : String.Format("{0}, {1}, {2}, {3}", detection.bbox[0], detection.bbox[1], detection.bbox[2], detection.bbox[3]);
                            detection.detectionID = detectionIndex;
                            noDetectionsIncluded = false;

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
                                // Timelapse also enforces a minimum recognition confidence. That is, any value less than the MinimumRecognitoinValue 
                                // is automatically thrown away. Note that this means that the confidence probabilities may not sum to 1
                                if (conf < Constant.DetectionValues.MinimumRecognitionValue)
                                {
                                    continue;
                                }
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
                    // If there are no detections, we populate it with values that indicate that.
                    if (image.detections.Count == 0 || noDetectionsIncluded)
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
                    fileCount++;
                }
                detectionDB.Insert(Constant.DBTables.Detections, detectionInsertionStatements);
                detectionDB.Insert(Constant.DBTables.Classifications, classificationInsertionStatements);
                fileDatabase.IndexCreateForDetectionsAndClassifications();
                // System.Diagnostics.Debug.Print("Files: " + fileCount + " Detections: " + detectionInsertionStatements.Count() + " Classifications: " + classificationInsertionStatements.Count());
                if (dataTable != null)
                {
                    dataTable.Dispose();
                }
            }
        }
        #endregion
    }
}