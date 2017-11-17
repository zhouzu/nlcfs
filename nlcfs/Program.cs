using DokanNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlcfs
{
  class Program
  {
    public static Client client;
    static void Main(string[] args)
    {
      try
      {
        client = new Client();
        
        client.Start();
        var mirror = new Mirror("C:");
        mirror.Mount("M:\\", DokanOptions.DebugMode, 1);
        
        Console.ReadLine();
        Console.WriteLine("Success");
      }
      catch (DokanException ex)
      {
        Console.WriteLine("Error: " + ex.Message);
      }
    }
  }
}
