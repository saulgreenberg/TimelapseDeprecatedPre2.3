using System;

namespace Timelapse.Detection
{
    public class DetectionSelections
    {
        public bool Enabled
        {
            get
            {
                return this.UseDetections;
            }
        }

        // Whether or not detections should be used
        public bool UseDetections { get; set; }
        public bool EmptyDetections { get; set; }
        public bool AllDetections { get; set; }
        public string DetectionCategory { get; set; }

        public double DetectionConfidenceThreshold1ForUI { get; set; }
        public double DetectionConfidenceThreshold2ForUI { get; set; }

        // Transform the confidence threshold as needed

        public Tuple<double, double> DetectionConfidenceThresholdForSelect
        {
            get
            {
                const double justAboveZero = 0.00001;
                double lowerBound;
                double upperBound;
                if (this.EmptyDetections)
                {
                    // For Empty category, we want to invert the confidence 
                    // e.g confidence of 1 is returned as confidence of 0
                    // But note that we actually return the confidence of the different threshold in this case, as normally #1 <= #2
                    // Doing so keeps that relationship after the inversion is done.
                    // We also swap the lower/uppder bound to keep one less than the other
                    // If Threshold2 is .99 in the UI for empty items, we invert that, but to just above 0
                    // so we capture all the non-zero items (i.e., all images with detections in that range) as otherwise it could
                    //  omit the rare image with a max detection between 0 and .01
                    lowerBound = (this.DetectionConfidenceThreshold2ForUI == 0.99) ? justAboveZero : 1.0 - this.DetectionConfidenceThreshold2ForUI;
                    upperBound = 1.0 - this.DetectionConfidenceThreshold1ForUI;

                }
                else if (this.AllDetections)
                {
                    // We don't want All detections to include images with no detections (i.e., Confidence range includes 0), so if we see a zero, we 
                    // alter that to just above zero.
                    lowerBound = this.DetectionConfidenceThreshold1ForUI == 0 ? justAboveZero : this.DetectionConfidenceThreshold1ForUI;
                    upperBound = this.DetectionConfidenceThreshold2ForUI == 0 ? justAboveZero : this.DetectionConfidenceThreshold2ForUI;
                }
                else
                {
                    lowerBound = this.DetectionConfidenceThreshold1ForUI;
                    upperBound = this.DetectionConfidenceThreshold2ForUI;
                }
                return new Tuple<double, double>(lowerBound, upperBound);
            }
        }

        public string CategoryCategory { get; set; }

        public double CategoryConfidenceThreshold1 { get; set; }
        public double CategoryConfidenceThreshold2 { get; set; }

        public DetectionSelections()
        {
            this.ClearAllDetectionsUses();
            // Set default: 0.8 - 1 seems like a reasonable starting confidence
            this.DetectionCategory = "1";
            this.DetectionConfidenceThreshold1ForUI = 0.8;
            this.DetectionConfidenceThreshold2ForUI = 1;

            this.CategoryConfidenceThreshold1 = 0.8;
            this.CategoryConfidenceThreshold2 = 1;
            this.CategoryCategory = "1";

            this.EmptyDetections = false;
        }

        // Bulk disabling of detection selection criteria
        public void ClearAllDetectionsUses()
        {
            this.UseDetections = false;
        }
    }
}
