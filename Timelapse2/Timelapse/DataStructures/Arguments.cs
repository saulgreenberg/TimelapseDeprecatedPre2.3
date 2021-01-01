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
        public string Template { get; set; }
        public string RelativePath { get; set; } = String.Empty;

        public bool ConstrainToRelativePath { get; set; } 

        public const string templateFlag = "-template";
        public const string relativePathFlag = "-relativepath";
        public const string constrainToRelativePathFlag = "-constrainRelativePath";

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
                        this.ConstrainToRelativePath = bool.TryParse(arguments[index + 1], out bool result) ? result : false;
                        break;
                    default:
                        break;
                }
            };
        }
    }
}
