using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Timelapse.Util
{
    // Various forms for printing out trace infromation containing a message and a stack trace of the method names 
    public static class TraceDebug
    {
        // Print a message and stack trace to a file
        public static void PrintStackTraceToFile(string message)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(Path.Combine(Util.GlobalReferences.MainWindow.FolderPath, Constant.File.TraceFile), true))
            {
                file.WriteLine(GetMethodNameStack(message, 5));
                file.WriteLine("----");
            }
        }

        // Insert these call into the beginning of a method name with the TRACE flag set in properties
        [Conditional("TRACE")]
        // Option to print various failure messagesfor debugging
        public static void PrintMessage(string message)
        {
            Debug.Print("PrintFailure: " + message);
        }

        [Conditional("TRACE")]
        public static void PrintStackTrace(int level)
        {
            Debug.Print(GetMethodNameStack(String.Empty, level));
        }

        [Conditional("TRACE")]
        public static void PrintStackTrace(string message)
        {
            Debug.Print(GetMethodNameStack(message, 1));
        }

        // Option to print various failure messagesfor debugging
        public static void PrintStackTrace(string message, int level)
        {
            Debug.Print(GetMethodNameStack(message, level));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        // Return the order and number of calls on a method, i.e., to illustrate the method calling stack.
        // The optional message string can be anything you want included in the output.
        // The optional level is the depth of the stack that should be printed 
        // (1 returns the current method name; 2 adds the caller name of that method, etc.)
        private static string GetMethodNameStack(string message = "", int level = 1)
        {
            StackTrace st = new StackTrace(true);
            StackFrame sf;
            string methodStack = String.Empty;
            for (int i = 1; i <= level; i++)
            {
                sf = st.GetFrame(i);
                methodStack += Path.GetFileName(sf.GetFileName()) + ": ";
                methodStack += sf.GetMethod().Name;
                if (i < level)
                {
                    methodStack += " <- ";
                }
            }
            methodStack += ": " + message;
            return methodStack;
        }
    }
}