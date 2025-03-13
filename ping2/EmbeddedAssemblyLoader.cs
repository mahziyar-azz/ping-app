using ping2;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;


public static class Program
{
    [STAThread]
    //public static void Main2()
    //{
    //    AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

    //    // Replace 'YourNamespace.Program.Main()' with your app's entry point
    //    Application.Run(new Form1());
    //}

    private static Assembly OnResolveAssembly(object sender, ResolveEventArgs e)
    {
        var thisAssembly = Assembly.GetExecutingAssembly();

        // Get the name of the assembly being requested
        var assemblyName = new AssemblyName(e.Name);
        var dllName = assemblyName.Name + ".dll";

        // Find the embedded resource
        var resourceName = thisAssembly.GetManifestResourceNames().FirstOrDefault(s => s.EndsWith(dllName));
        if (resourceName == null) return null;

        // Load the embedded DLL into memory
        using (var stream = thisAssembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null) return null;
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, bytes.Length);
            return Assembly.Load(bytes);
        }
    }
}
