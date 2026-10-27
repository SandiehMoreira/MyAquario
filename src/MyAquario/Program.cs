using System;
using System.Threading;
using System.Windows.Forms;

namespace MyAquario
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 3 && args[0] == "--snapshot")
            {
                Snapshot.Run(args[1], args[2]);
                return;
            }

            using var mutex = new Mutex(true, "MyAquario_SingleInstance_v1", out bool first);
            if (!first)
            {
                MessageBox.Show("O MyAquario já está aberto (veja o ícone perto do relógio).", "MyAquario");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => Log.Error(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("FATAL: " + e.ExceptionObject);

            Application.Run(new AquarioApp());
        }
    }
}
