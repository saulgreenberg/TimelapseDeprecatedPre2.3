using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelapse.Detection
{
    public class DetectionSelections
    {
        public bool Enabled {
            get
            {
                return this.UseCategoryCategory || this.UseCategoryConfidenceThreshold || this.UseDetectionCategory || this.UseDetectionConfidenceThreshold;
            }
        }
        public string DetectionCategory { get; set; }
        public bool UseDetectionCategory { get; set; }

        public double DetectionConfidenceThreshold { get; set; }
        public bool UseDetectionConfidenceThreshold { get; set; }

        public string CategoryCategory { get; set; }
        public bool UseCategoryCategory { get; set; }

        public double CategoryConfidenceThreshold { get; set; }
        public bool UseCategoryConfidenceThreshold { get; set; }

        public DetectionSelections()
        {
            this.DisableAllUses();
            this.DetectionCategory = "1";
            this.DetectionConfidenceThreshold = 0.95;
            this.CategoryConfidenceThreshold = 0.95;
            this.CategoryCategory = "1";
        }

        // Bulk setting of detection selection criteria
        public void SetCriteria (string detectionCategory, double detectionConfidence, string categoryCategory, double categoryConfidence)
        {
            this.DetectionCategory = detectionCategory;
            this.DetectionConfidenceThreshold = detectionConfidence;
            this.CategoryCategory = categoryCategory;
            this.CategoryConfidenceThreshold = categoryConfidence;
        }

        // Bulk disabling of detection selection criteria
        public void DisableAllUses()
        {
            this.UseDetectionCategory = false;
            this.UseDetectionConfidenceThreshold = false;
            this.UseCategoryCategory = false;
            this.UseCategoryConfidenceThreshold = false;
        }
    }
}
