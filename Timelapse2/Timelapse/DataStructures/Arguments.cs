using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Timelapse.DataStructures
{
    // Collects the valid arguments. All valid arguments are in the form of:
    // -flag1 value -flag2 value etc., where flags are case-insensitive
    public class Arguments
    {
        // argument strings
        private const string templateFlag = "-template";
        private const string relativePathFlag = "-relativepath";
        private const string constrainToRelativePathFlag = "-constrainrelativepath";

        private bool constrainToRelativePath;

        // The full Timelape template path
        public string Template { get; set; }

        // The relative path for starting timelapse
        public string RelativePath { get; set; } = String.Empty;

        // Constrain all database actions to the relative path and its subfolders
        // if ConstrainToRelativePath is true, the user is contrained to select folders that
        // are either the relative path or subfolders of it.
        public bool ConstrainToRelativePath {
            get
            {
                // if relativePath is empty, we shouldn't constrain to it
                return (constrainToRelativePath && !String.IsNullOrWhiteSpace(this.RelativePath));
            }
            set
            {
                constrainToRelativePath = value; 
            } 
        }

        public Arguments(string[] arguments)
        {
            if (arguments == null)
            { 
                return; 
            }
            // If the argument exists, assign it
            // Note that we start at 1, as the first element of the array is the name of the executing program
            for (int index = 1; index < arguments.Length; index += 2)
            {
                switch (arguments[index].ToLower())
                {
                    case templateFlag:
                        this.Template = @arguments[index + 1];
                        break;
                    case relativePathFlag:
                        this.RelativePath = arguments[index + 1];
                        break;
                    case constrainToRelativePathFlag:
                        // we need to convert the string arguement to a bool. 
                        this.constrainToRelativePath = bool.TryParse(arguments[index + 1], out bool result) && result;
                        break;
                    default:
                        break;
                }
            };
        }
    }
}
