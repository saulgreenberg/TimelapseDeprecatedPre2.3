using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timelapse.Enums;

namespace Timelapse.Detection
{
    public class DetectionSelections
    {
        public bool Enabled
        {
            get
            {
                return this.UseCategoryCategory || this.UseCategoryConfidenceThreshold || this.UseDetectionCategory || this.UseDetectionConfidenceThreshold;
            }
        }

        public bool UseDetectionCategory { get; set; }
        public string DetectionCategory { get; set; }

        public bool UseDetectionConfidenceThreshold { get; set; }
        public double DetectionConfidenceThreshold1 { get; set; }
        public double DetectionConfidenceThreshold2 { get; set; }
        public ComparisonEnum DetectionComparison { get; set; }

        public bool UseCategoryCategory { get; set; }
        public string CategoryCategory { get; set; }

        public bool UseCategoryConfidenceThreshold { get; set; }
        public double CategoryConfidenceThreshold1 { get; set; }
        public double CategoryConfidenceThreshold2 { get; set; }
        public ComparisonEnum CategoryComparison { get; set; }
        public DetectionSelections()
        {
            this.ClearAllDetectionsUses();
            // Set default: Greater than .8
            this.DetectionCategory = "1";
            this.DetectionConfidenceThreshold1 = 0.8;
            this.DetectionConfidenceThreshold2 = 1;
            this.DetectionComparison = ComparisonEnum.Between;

            this.CategoryComparison = ComparisonEnum.Between;
            this.CategoryConfidenceThreshold1 = 0.8;
            this.CategoryConfidenceThreshold2 = 1;
            this.CategoryCategory = "1";
        }

        // Bulk disabling of detection selection criteria
        public void ClearAllDetectionsUses()
        {
            this.UseDetectionCategory = false;
            this.UseDetectionConfidenceThreshold = false;
            this.UseCategoryCategory = false;
            this.UseCategoryConfidenceThreshold = false;
        }
    }
}
