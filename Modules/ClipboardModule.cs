using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FinalBot.Modules
{
    public static class ClipboardModule
    {
        public static string GetClipboardText()
        {
            try 
            {
                string text = "";
                Thread thread = new Thread(() => {
                    text = Clipboard.GetText();
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
                return text;
            }
            catch 
            {
                return "";
            }
        }
    }
}
