using System;
using System.Windows.Forms;

namespace CornerBreak.Tests
{
    public static class NativeTestProgram
    {
        [STAThread]
        public static int Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                int count = NativeCoreTests.Run();
                Console.WriteLine("PASS: " + count + " native core assertions.");
                count = NativeUiTests.Run();
                Console.WriteLine("PASS: " + count + " native UI assertions.");
                count = NativeFloatingTests.Run();
                Console.WriteLine("PASS: " + count + " native floating UI assertions.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
