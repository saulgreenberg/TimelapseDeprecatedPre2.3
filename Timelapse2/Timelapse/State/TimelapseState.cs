using System;
using System.Windows.Input;

namespace Timelapse.Util
{
    /// <summary>
    /// A class that tracks various states and flags. 
    /// While it inherits from TimelapseUserRegistrySettings (so that all variables can be accessed collectively), 
    /// these particular variables/methods are logically separate as they are only for run-time use and not stored in the registry
    /// </summary>
    public class TimelapseState : TimelapseUserRegistrySettings
    {
        #region Public Properties
        // The threshold used for calculating combined differences between images
        public byte DifferenceThreshold { get; set; } // The threshold used for calculating combined differences

        // Whether the FileNavigator slider is being dragged
        public bool FileNavigatorSliderDragging { get; set; }

        // Whether the mouse is over a counter control
        public string MouseOverCounter { get; set; }

        public DateTime MostRecentDragEvent { get; set; }

        public bool FirstTimeFileLoading { get; set; }

        public double BoundingBoxThresholdOveride { get; set; }

       
        #endregion

        #region Private (internal) variables 
        // These three variables are used for keeping track of repeated keys.
        // There is a bug in Key.IsRepeat in KeyEventArgs events, where AvalonDock always sets it as true
        // in a floating window. As a workaround, TimelapseWindow will set IsKeyRepeat to false whenever it detects a key up event.
        // keyRepeatCount and mostRecentKey are used for throttling, where keyRepeatCount is incremented everytime repeat is true and the same key is seen 
        private int keyRepeatCount;
        private KeyEventArgs mostRecentKey;
        private bool IsKeyRepeat { get; set; }
        #endregion

        #region Constructor
        public TimelapseState()
        {
            this.FirstTimeFileLoading = true;
            this.Reset();
        }
        #endregion

        #region Public Methods - Reset
        /// <summary>
        /// Reset various state variables
        /// </summary>
        public void Reset()
        {
            this.DifferenceThreshold = Constant.ImageValues.DifferenceThresholdDefault;
            this.FileNavigatorSliderDragging = false;
            this.MouseOverCounter = null;
            this.MostRecentDragEvent = DateTime.UtcNow - this.Throttles.DesiredIntervalBetweenRenders;
            this.BoundingBoxThresholdOveride = 1;
            this.ResetKeyRepeat();
        }
        #endregion

        #region Key Repeat methods
        /// <summary>
        /// Key Repeat: Reset 
        /// </summary>
        public void ResetKeyRepeat()
        {
            this.keyRepeatCount = 0;
            this.IsKeyRepeat = false;
            this.mostRecentKey = null;
        }

        /// <summary>
        /// KeyRepeat: Count of the numer of repeats for a key. Used in threshold determination
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public int GetKeyRepeatCount(KeyEventArgs key)
        {
            // check mostRecentKey for null as key delivery is not entirely deterministic
            // it's possible WPF will send the first key as a repeat if the user holds a key down or starts typing while the main window is opening
            // Note that we check the isrepeat from both the key event, and the IsKeyRepeat key that we track due to bugs in how AvalonDock returns erroneous IsRepeat values.
            if (key != null && key.IsRepeat && this.IsKeyRepeat && this.mostRecentKey != null && this.mostRecentKey.IsRepeat && (key.Key == this.mostRecentKey.Key))
            {
                ++this.keyRepeatCount;
            }
            else
            {
                this.keyRepeatCount = 0;
                this.IsKeyRepeat = true;
            }
            this.mostRecentKey = key;
            return this.keyRepeatCount;
        }
        #endregion
    }
}
