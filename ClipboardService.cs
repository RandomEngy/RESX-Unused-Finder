using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ResxUnusedFinder
{
    public static class ClipboardService
    {
        public static bool SetText(string text)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (COMException)
                {
                    // retry
                }
            }

            MessageBox.Show("Unable to copy text. Please try again later.");
            return false;
        }
    }
}
