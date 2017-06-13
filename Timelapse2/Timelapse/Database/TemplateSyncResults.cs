using System;
using System.Collections.Generic;

namespace Timelapse.Database
{
    // This class will eventually hold the data labels that should be added/deleted/renamed in case of a mismatch
    // between the .ddb and the .tdb template tables
    public class TemplateSyncResults
    {
        // These  lists collect information about possible mismatches between the .tdb and the .ddb template, and what should eventually be added, deleted or renamed
        public Dictionary<string, string> DataLabelsInTemplateButNotImageDatabase { get; set; }
        public Dictionary<string, string> DataLabelsInImageButNotTemplateDatabase { get; set; }

        // These  lists collect information about what should eventually be added, deleted or renamed
        public List<String> DataLabelsToAdd { get; set; }
        public List<String> DataLabelsToDelete { get; set; }
        public List<KeyValuePair<string, string>> DataLabelsToRename { get; set; }

        // These lists collect error messages and warnings concerning control synchronization between the templates
        public List<string> ControlSynchronizationErrors { get; private set; }
        public List<string> ControlSynchronizationWarnings { get; private set; }

        // Singals whether or not to use the template found in the Image database instead of the Template database
        public bool UseTemplateDBTemplate { get; set; }

        public bool SyncRequiredAsDataLabelsDiffer
        {
            get
            {
                return this.DataLabelsInTemplateButNotImageDatabase.Count > 0 || this.DataLabelsInImageButNotTemplateDatabase.Count > 0;
            }
        }

        public bool SyncRequiredAsChoiceMenusDiffer { get; set; }

        public TemplateSyncResults()
        {
            this.DataLabelsInTemplateButNotImageDatabase = new Dictionary<string, string>();
            this.DataLabelsInImageButNotTemplateDatabase = new Dictionary<string, string>();

            this.DataLabelsToAdd = new List<String>();
            this.DataLabelsToDelete = new List<String>();
            this.DataLabelsToRename = new List<KeyValuePair<string, string>>();

            this.ControlSynchronizationErrors = new List<string>();
            this.ControlSynchronizationWarnings = new List<string>();

            this.UseTemplateDBTemplate = true;
            this.SyncRequiredAsChoiceMenusDiffer = false;
        }
    }
}
